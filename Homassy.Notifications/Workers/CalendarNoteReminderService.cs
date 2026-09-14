using Homassy.Data.Context;
using Homassy.Data.Enums;
using Homassy.Data.Extensions;
using Homassy.Notifications.Configuration;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Turns the reminder on a family day note (#60) into a push and an inbox row for every member of
/// that family.
/// </summary>
/// <remarks>
/// A note carries one reminder instant, not a lead time, because a note is about a day and has no
/// start time to lead from - the client computes the moment in the user's own zone and the column
/// stores it in UTC.
/// <para>
/// Duplicate suppression is a claim-then-push, the same shape
/// <see cref="ExternalCalendarReminderService"/> uses: <c>ReminderSentAt</c> is stamped and
/// committed <em>before</em> the notifications go out, so a crash or a second instance can only
/// ever skip a reminder, never repeat one. Rescheduling clears the stamp, which is what re-arms a
/// reminder that has already fired.
/// </para>
/// <para>
/// The claim is a conditional <c>UPDATE ... WHERE "ReminderSentAt" IS NULL</c> rather than a
/// tracked write, and it is the affected-row count that decides who won. That distinction matters:
/// <c>CalendarNote</c> carries no concurrency token, so a plain <c>SaveChanges</c> would succeed in
/// <em>both</em> instances of a rolling deploy and push the same reminder to the family twice. The
/// external-calendar worker gets the same guarantee from a unique index on its dispatch table; this
/// one has no such table, so the predicate is the guard.
/// </para>
/// <para>
/// A reminder whose moment was missed is still delivered inside the catch-up window. Past that it
/// is claimed and dropped rather than sent: "this was two weeks ago" is worse than silence, and
/// leaving it unclaimed would make it fire on every single cycle forever.
/// </para>
/// </remarks>
public sealed class CalendarNoteReminderService : PeriodicWorkerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FamilyPushNotifier _notifier;
    private readonly CalendarNoteWorkerSchedule _settings;

    public CalendarNoteReminderService(
        IServiceScopeFactory scopeFactory,
        FamilyPushNotifier notifier,
        IOptions<NotificationWorkerSettings> workerSettings)
        : base(workerSettings.Value.CalendarNoteReminder)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
        _settings = workerSettings.Value.CalendarNoteReminder;
    }

    protected override string WorkerName => "Calendar note reminder service";

    protected override async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        // The filtered index on (ReminderAt) covers exactly this predicate. Read untracked: the
        // claim below is a conditional UPDATE, not a tracked write, so nothing here is saved through
        // the change tracker.
        var due = await context.CalendarNotes
            .AsNoTracking()
            .Where(n => n.ReminderAt != null
                        && n.ReminderSentAt == null
                        && n.ReminderAt <= nowUtc)
            .OrderBy(n => n.ReminderAt)
            .Select(n => new DueNote(n.Id, n.PublicId, n.FamilyId, n.Title, n.Date, n.ReminderAt!.Value))
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
            return;

        var staleBefore = nowUtc - _settings.CatchUpWindow;

        foreach (var note in due)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            // Claim first, and only on the row that is still unclaimed. Everything below is
            // best-effort delivery on a note this instance now owns.
            var claimed = await context.CalendarNotes
                .Where(n => n.Id == note.Id && n.ReminderSentAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(n => n.ReminderSentAt, nowUtc),
                    cancellationToken);

            if (claimed == 0)
            {
                Log.Debug("Calendar note reminder {NoteId} was already claimed elsewhere", note.PublicId);
                continue;
            }

            if (note.ReminderAt < staleBefore)
            {
                Log.Information(
                    "Dropping stale calendar note reminder {NoteId} (due {DueAt:u}, now {NowAt:u})",
                    note.PublicId, note.ReminderAt, nowUtc);
                continue;
            }

            try
            {
                await NotifyAsync(context, note.FamilyId, note.Title, note.Date, nowUtc, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to send the reminder for calendar note {NoteId}", note.PublicId);
            }
        }
    }

    /// <summary>
    /// Sends the reminder to every member of the family, one call per recipient: how many days away
    /// the note is depends on the reader's own timezone, so the envelope differs per person.
    /// </summary>
    private async Task NotifyAsync(
        HomassyDbContext context,
        int familyId,
        string title,
        DateOnly noteDate,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var recipients = await _notifier.GetRecipientsAsync(context, familyId, [], cancellationToken);
        if (recipients.Count == 0)
            return;

        foreach (var recipient in recipients)
        {
            var daysUntil = DaysUntil(noteDate, nowUtc, recipient.TimeZone);

            await _notifier.DispatchAsync(
                context,
                [recipient],
                [NotificationEnvelopes.CalendarNoteReminder(title, daysUntil)],
                "/calendar",
                cancellationToken);
        }

        Log.Information(
            "Calendar note reminder \"{Title}\" sent to {Count} member(s) of family {FamilyId}",
            title, recipients.Count, familyId);
    }

    /// <summary>
    /// Whole days from "today where the reader is" to the note's day. Never negative: a reminder
    /// that arrives on the note's own day (or, through the catch-up window, just after it) reads as
    /// "today".
    /// </summary>
    private static int DaysUntil(DateOnly noteDate, DateTime nowUtc, UserTimeZone timeZone)
    {
        var today = DateOnly.FromDateTime(ToLocal(nowUtc, timeZone));
        var days = noteDate.DayNumber - today.DayNumber;
        return days < 0 ? 0 : days;
    }

    /// <summary>
    /// A due note, read untracked. Only the fields the dispatch needs — the row itself is written
    /// by the conditional claim, never through this projection.
    /// </summary>
    private sealed record DueNote(
        int Id,
        Guid PublicId,
        int FamilyId,
        string Title,
        DateOnly Date,
        DateTime ReminderAt);

    private static DateTime ToLocal(DateTime utc, UserTimeZone timeZone)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone.ToTimeZoneId());
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unknown timezone {TimeZone}; falling back to UTC for calendar note reminders", timeZone);
            return utc;
        }
    }
}

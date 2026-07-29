using Homassy.API.Context;
using Homassy.API.Functions;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Pushes the reminder attached to a family day note. The reminder is one absolute instant
/// (<c>CalendarNote.ReminderAt</c>, derived API-side from the note's date and the author's timezone), so every
/// member of the family is notified at the same moment.
/// <para>
/// Polls every minute rather than every five: a note reminder is a wall-clock promise ("08:00"), and a
/// five-minute loop would turn that into "08:00–08:05".
/// </para>
/// <para>
/// Duplicate suppression is a claim-then-push. The claim is a single conditional UPDATE
/// (<c>SET ReminderSentAt = now WHERE Id = @id AND ReminderSentAt IS NULL</c>) committed <em>before</em> the
/// push goes out, so a crash, a restart inside the catch-up window, or two replicas racing can only ever skip a
/// send — never repeat one. Never restructure this into read-then-push-then-save.
/// </para>
/// </summary>
public sealed class CalendarNoteReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FamilyPushNotifier _notifier;

    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A reminder whose instant has already passed is still delivered inside this window, so a deploy or a
    /// short outage does not silently swallow it. Anything older is dropped as stale — a "today" reminder
    /// arriving hours late is worse than not arriving. Shared with <see cref="CalendarNoteFunctions"/> so the
    /// write-time staleness check and this delivery window cannot diverge.
    /// </summary>
    private static readonly TimeSpan CatchUpWindow = CalendarNoteFunctions.ReminderCatchUpWindow;

    /// <summary>Per-cycle cap. At one cycle a minute this still clears 3000 notes inside the catch-up window.</summary>
    private const int MaxNotesPerCycle = 200;

    /// <summary>
    /// A reminder left unclaimed this long can never be delivered (the service was down past the catch-up
    /// window), so it is retired to keep the pending-reminder index from growing without bound.
    /// </summary>
    private static readonly TimeSpan StaleRetirementAge = TimeSpan.FromDays(1);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(6);
    private DateTime _nextSweepUtc = DateTime.MinValue;

    public CalendarNoteReminderService(IServiceScopeFactory scopeFactory, FamilyPushNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Calendar note reminder service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in calendar note reminder service");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("Calendar note reminder service stopped");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var windowStart = nowUtc - CatchUpWindow;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        // Matches the IX_CalendarNotes_PendingReminder partial index (plus IsDeleted = false from the global
        // query filter), so this is an index scan over pending reminders only. Ordering by ReminderAt — not Id —
        // is what keeps families fair under MaxNotesPerCycle; don't change it.
        var due = await context.CalendarNotes
            .AsNoTracking()
            .Where(n => n.ReminderAt != null
                     && n.ReminderSentAt == null
                     && n.ReminderAt <= nowUtc
                     && n.ReminderAt > windowStart)
            .OrderBy(n => n.ReminderAt)
            .Take(MaxNotesPerCycle)
            .Select(n => new DueNote(n.Id, n.PublicId, n.FamilyId, n.Title, n.Content, n.Date))
            .ToListAsync(cancellationToken);

        // Grouped by family so recipients are resolved once per family per cycle — several notes easily come
        // due at the same 08:00.
        foreach (var family in due.GroupBy(n => n.FamilyId))
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                await ProcessFamilyAsync(context, family.Key, family.ToList(), nowUtc, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending calendar note reminders for family {FamilyId}", family.Key);
            }
        }

        await RetireStaleRemindersAsync(context, nowUtc, cancellationToken);
    }

    private async Task ProcessFamilyAsync(
        HomassyDbContext context,
        int familyId,
        List<DueNote> notes,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // Empty exclude list: the author is notified too. They set the reminder for themselves as much as for
        // anyone, which is why this differs from the activity monitors (those report someone else's action).
        var recipients = await _notifier.GetRecipientsAsync(context, familyId, [], cancellationToken);

        if (recipients.Count == 0)
        {
            // Deliberately not claimed: stamping a false "sent" for a family with push disabled would also mean
            // that enabling push a minute later misses a reminder that is still inside the catch-up window.
            Log.Debug("Family {FamilyId} has {Count} due calendar note reminder(s) but no push recipients",
                familyId, notes.Count);
            return;
        }

        foreach (var note in notes)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var claimed = await context.CalendarNotes
                .Where(n => n.Id == note.Id && n.ReminderSentAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReminderSentAt, nowUtc), cancellationToken);

            if (claimed == 0)
            {
                Log.Debug("Calendar note {NotePublicId} reminder was already claimed elsewhere", note.PublicId);
                continue;
            }

            await _notifier.DispatchAsync(context, recipients,
                language =>
                [
                    PushNotificationContentService.GetCalendarNoteReminderContent(
                        language, note.Title, note.Content, note.Date)
                ],
                "/calendar", cancellationToken);

            Log.Information(
                "Calendar note reminder sent to {Count} member(s) of family {FamilyId} for note {NotePublicId}",
                recipients.Count, familyId, note.PublicId);
        }
    }

    /// <summary>
    /// Retires reminders that fell out of the catch-up window while the service was down. There is no marker
    /// table to prune — the dedup state lives on the note row and dies with it — but an unclaimed past reminder
    /// would otherwise sit in the pending-reminder index forever, re-examined every minute.
    /// </summary>
    private async Task RetireStaleRemindersAsync(HomassyDbContext context, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (nowUtc < _nextSweepUtc)
            return;

        _nextSweepUtc = nowUtc + SweepInterval;
        var cutoff = nowUtc - StaleRetirementAge;

        var retired = await context.CalendarNotes
            .Where(n => n.ReminderAt != null && n.ReminderSentAt == null && n.ReminderAt < cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReminderSentAt, nowUtc), cancellationToken);

        if (retired > 0)
            Log.Information("Retired {Count} undeliverable calendar note reminder(s)", retired);
    }

    private sealed record DueNote(int Id, Guid PublicId, int FamilyId, string Title, string? Content, DateOnly Date);
}

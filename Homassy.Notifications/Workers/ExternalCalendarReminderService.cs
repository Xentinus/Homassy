using Homassy.API.Context;
using Homassy.API.Entities.Family;
using Homassy.API.Enums;
using Homassy.API.Extensions;
using Homassy.API.Functions;
using Homassy.API.Models.ExternalCalendar;
using Homassy.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json;

namespace Homassy.Notifications.Workers;

/// <summary>
/// Turns events from the synced external (iCal) calendars into push reminders. Each calendar carries its
/// own lead times (see <see cref="FamilyExternalCalendar.ReminderLeadTimesJson"/>); every family member
/// with push notifications enabled is reminded once per occurrence and lead time.
/// <para>
/// The cache is refreshed hourly by <c>ExternalCalendarSyncService</c> in Homassy.API, but reminders are
/// evaluated every minute so an "at start" reminder is not up to an hour late. That also means a moved or
/// deleted event is only reflected after the next sync.
/// </para>
/// <para>
/// Duplicate suppression is a claim-then-push: the <see cref="ExternalCalendarReminderDispatch"/> row is
/// written and committed *before* the push goes out, so a lost race on the unique index (or a re-sync, or
/// a restart inside the catch-up window) can only ever skip a send, never repeat one.
/// </para>
/// </summary>
public sealed class ExternalCalendarReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly FamilyPushNotifier _notifier;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A trigger time that has already passed is still delivered inside this window, so a deploy or a
    /// short outage does not silently swallow a reminder. Anything older is dropped as stale — nobody
    /// wants "starts in 15 minutes" hours after the fact.
    /// </summary>
    private static readonly TimeSpan CatchUpWindow = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan MarkerRetention = TimeSpan.FromDays(30);
    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(6);
    private DateTime _nextPruneUtc = DateTime.MinValue;

    /// <summary>Matches <see cref="ExternalCalendarReminderDispatch.EventUid"/>'s column length.</summary>
    private const int MaxEventUidLength = 512;

    public ExternalCalendarReminderService(IServiceScopeFactory scopeFactory, FamilyPushNotifier notifier)
    {
        _scopeFactory = scopeFactory;
        _notifier = notifier;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("External calendar reminder service started");

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
                Log.Error(ex, "Error in external calendar reminder service");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        Log.Information("External calendar reminder service stopped");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomassyDbContext>();

        var calendars = await context.FamilyExternalCalendars
            .AsNoTracking()
            .Where(c => c.IsEnabled && c.ReminderLeadTimesJson != null && c.CachedEventsJson != null)
            .Select(c => new CalendarState(
                c.Id,
                c.PublicId,
                c.FamilyId,
                c.ReminderLeadTimesJson!,
                c.AllDayNotifyTime,
                c.CachedEventsJson!))
            .ToListAsync(cancellationToken);

        foreach (var calendar in calendars)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                await ProcessCalendarAsync(context, calendar, nowUtc, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing reminders for external calendar {CalendarId}", calendar.PublicId);
            }
        }

        await PruneDispatchesAsync(context, nowUtc, cancellationToken);
    }

    private async Task ProcessCalendarAsync(
        HomassyDbContext context,
        CalendarState calendar,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var leadTimes = ExternalCalendarFunctions.ParseReminderLeadTimes(calendar.ReminderLeadTimesJson);
        if (leadTimes.Count == 0)
            return;

        var events = DeserializeEvents(calendar);
        if (events.Count == 0)
            return;

        var recipients = await _notifier.GetRecipientsAsync(context, calendar.FamilyId, [], cancellationToken);
        if (recipients.Count == 0)
            return;

        var due = CollectDueReminders(calendar, events, leadTimes, recipients, nowUtc);
        if (due.Count == 0)
            return;

        var alreadySent = await LoadDispatchedKeysAsync(context, calendar.Id, due, cancellationToken);

        foreach (var reminder in due)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            var uid = TruncateUid(reminder.Event.Uid);
            var key = (reminder.Recipient.Id, uid, reminder.OccurrenceKey, reminder.LeadTimeMinutes);

            // Also collapses duplicates inside this batch: the same occurrence can appear more than once
            // in a feed that repeats a UID across overlapping RDATE/RRULE definitions.
            if (!alreadySent.Add(key))
                continue;

            var marker = new ExternalCalendarReminderDispatch
            {
                ExternalCalendarId = calendar.Id,
                UserId = reminder.Recipient.Id,
                EventUid = uid,
                OccurrenceKey = reminder.OccurrenceKey,
                LeadTimeMinutes = reminder.LeadTimeMinutes,
                SentAt = DateTime.UtcNow
            };

            context.ExternalCalendarReminderDispatches.Add(marker);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                // Another instance claimed this reminder first. Detach so the shared context stays usable
                // for the remaining calendars.
                context.Entry(marker).State = EntityState.Detached;
                Log.Debug(ex,
                    "Calendar reminder for event {EventUid} / user {UserId} was already claimed elsewhere",
                    uid, reminder.Recipient.Id);
                continue;
            }

            await _notifier.DispatchAsync(context, [reminder.Recipient],
                language =>
                [
                    PushNotificationContentService.GetCalendarEventReminderContent(
                        language, reminder.Event.Title, reminder.LeadTimeMinutes, reminder.Event.IsAllDay)
                ],
                "/calendar", cancellationToken);

            Log.Information(
                "Calendar reminder sent to user {UserId} for event \"{EventTitle}\" ({LeadTime} min lead) on calendar {CalendarId}",
                reminder.Recipient.Id, reminder.Event.Title, reminder.LeadTimeMinutes, calendar.PublicId);
        }
    }

    /// <summary>
    /// Expands the cached events into the (recipient, occurrence, lead time) triples whose trigger instant
    /// falls inside the current window. Trigger times are per recipient because an all-day event is
    /// anchored to the calendar's notify time in the recipient's own timezone.
    /// </summary>
    private static List<DueReminder> CollectDueReminders(
        CalendarState calendar,
        List<CachedICalEvent> events,
        List<int> leadTimes,
        List<RecipientInfo> recipients,
        DateTime nowUtc)
    {
        var candidates = FilterToHorizon(events, leadTimes, nowUtc);
        if (candidates.Count == 0)
            return [];

        var due = new List<DueReminder>();

        foreach (var recipient in recipients)
        {
            var timeZone = ResolveTimeZone(recipient.TimeZone);

            foreach (var ev in candidates)
            {
                foreach (var leadTime in leadTimes)
                {
                    var triggerUtc = ComputeTriggerUtc(ev, leadTime, calendar.AllDayNotifyTime, timeZone);
                    if (triggerUtc == null)
                        continue;

                    if (triggerUtc > nowUtc || triggerUtc <= nowUtc - CatchUpWindow)
                        continue;

                    due.Add(new DueReminder(recipient, ev, leadTime, OccurrenceKeyOf(ev)));
                }
            }
        }

        return due;
    }

    /// <summary>
    /// Narrows the (up to 14 months of) expanded occurrences down to the ones that could plausibly be due,
    /// so the per-recipient trigger maths runs over a handful of events rather than the whole feed. The
    /// two-day slack absorbs every timezone offset without needing to know the recipient's zone yet.
    /// </summary>
    private static List<CachedICalEvent> FilterToHorizon(List<CachedICalEvent> events, List<int> leadTimes, DateTime nowUtc)
    {
        var slack = TimeSpan.FromDays(2);
        var from = nowUtc - CatchUpWindow - slack;
        var to = nowUtc + TimeSpan.FromMinutes(leadTimes.Max()) + slack;

        return events
            .Where(ev => !string.IsNullOrWhiteSpace(ev.Uid))
            .Where(ev =>
            {
                // Events cached before StartUtc existed land on DateTime.MinValue here and drop out; the
                // next hourly sync backfills them.
                var approximateStart = ev.IsAllDay ? ev.Start.Date : ev.StartUtc;
                return approximateStart >= from && approximateStart <= to;
            })
            .ToList();
    }

    /// <summary>
    /// The instant a reminder should fire, or null if the occurrence cannot be scheduled.
    /// A timed event's lead time is absolute, so it is identical for every recipient; an all-day event has
    /// no meaningful time of day, so it is anchored to the calendar's notify time in the recipient's zone
    /// and then shifted back by the lead time (1 day before 08:00 → the previous day at 08:00).
    /// </summary>
    private static DateTime? ComputeTriggerUtc(
        CachedICalEvent ev,
        int leadTimeMinutes,
        TimeOnly allDayNotifyTime,
        TimeZoneInfo timeZone)
    {
        if (ev.IsAllDay)
        {
            var local = ev.Start.Date.Add(allDayNotifyTime.ToTimeSpan()).AddMinutes(-leadTimeMinutes);
            return ToUtc(local, timeZone);
        }

        if (ev.StartUtc == default)
            return null;

        return DateTime.SpecifyKind(ev.StartUtc, DateTimeKind.Utc).AddMinutes(-leadTimeMinutes);
    }

    private static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        // A spring-forward gap contains no such local instant; step onto the first one that exists rather
        // than letting ConvertTimeToUtc throw.
        while (timeZone.IsInvalidTime(local))
            local = local.AddMinutes(1);

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    /// <summary>
    /// Identifies the occurrence inside its series. A moved occurrence produces a different key, so it has
    /// no dispatch marker and re-arms itself; the marker left behind by the old time simply ages out.
    /// </summary>
    private static DateTime OccurrenceKeyOf(CachedICalEvent ev) =>
        DateTime.SpecifyKind(ev.IsAllDay ? ev.Start.Date : ev.StartUtc, DateTimeKind.Unspecified);

    private static async Task<HashSet<(int UserId, string EventUid, DateTime OccurrenceKey, int LeadTimeMinutes)>>
        LoadDispatchedKeysAsync(
            HomassyDbContext context,
            int calendarId,
            List<DueReminder> due,
            CancellationToken cancellationToken)
    {
        var uids = due.Select(d => TruncateUid(d.Event.Uid)).Distinct().ToList();

        var sent = await context.ExternalCalendarReminderDispatches
            .AsNoTracking()
            .Where(d => d.ExternalCalendarId == calendarId && uids.Contains(d.EventUid))
            .Select(d => new { d.UserId, d.EventUid, d.OccurrenceKey, d.LeadTimeMinutes })
            .ToListAsync(cancellationToken);

        return sent
            .Select(d => (d.UserId, d.EventUid, d.OccurrenceKey, d.LeadTimeMinutes))
            .ToHashSet();
    }

    /// <summary>Markers only matter while their occurrence can still re-trigger; the rest are swept out.</summary>
    private async Task PruneDispatchesAsync(HomassyDbContext context, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (nowUtc < _nextPruneUtc)
            return;

        _nextPruneUtc = nowUtc + PruneInterval;
        var cutoff = nowUtc - MarkerRetention;

        var removed = await context.ExternalCalendarReminderDispatches
            .Where(d => d.SentAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
            Log.Information("Pruned {Count} expired calendar reminder marker(s)", removed);
    }

    private static List<CachedICalEvent> DeserializeEvents(CalendarState calendar)
    {
        try
        {
            return JsonSerializer.Deserialize<List<CachedICalEvent>>(calendar.CachedEventsJson, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to deserialize cached events for calendar {CalendarId}", calendar.PublicId);
            return [];
        }
    }

    private static TimeZoneInfo ResolveTimeZone(UserTimeZone timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone.ToTimeZoneId());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unknown timezone {TimeZone}; falling back to UTC for calendar reminders", timeZone);
            return TimeZoneInfo.Utc;
        }
    }

    private static string TruncateUid(string uid) =>
        uid.Length <= MaxEventUidLength ? uid : uid[..MaxEventUidLength];

    private sealed record CalendarState(
        int Id,
        Guid PublicId,
        int FamilyId,
        string ReminderLeadTimesJson,
        TimeOnly AllDayNotifyTime,
        string CachedEventsJson);

    private sealed record DueReminder(
        RecipientInfo Recipient,
        CachedICalEvent Event,
        int LeadTimeMinutes,
        DateTime OccurrenceKey);
}

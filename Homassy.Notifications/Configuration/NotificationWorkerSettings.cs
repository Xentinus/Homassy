using System.ComponentModel.DataAnnotations;

namespace Homassy.Notifications.Configuration;

/// <summary>
/// How often each background worker runs, and how it backs off when it fails (#95).
/// </summary>
/// <remarks>
/// Every worker used to hardcode its interval as a <c>readonly TimeSpan</c> field and a second,
/// different value for its error delay. Changing "run automations every minute instead of every
/// five" was a code change, an image rebuild and a deploy, and exercising a reminder locally meant
/// waiting out an interval chosen for production.
/// <para>
/// Bound from the <c>NotificationWorkers</c> configuration section. Every default here is the
/// value that was compiled in before, so an instance that configures nothing behaves exactly as it
/// did.
/// </para>
/// </remarks>
public sealed class NotificationWorkerSettings : IValidatableObject
{
    /// <summary>Configuration section this binds from.</summary>
    public const string SectionName = "NotificationWorkers";

    /// <summary>Unread family-chat messages, the shortest interval of the nine.</summary>
    public FamilyChatWorkerSchedule FamilyChatNotification { get; set; } = new()
    {
        IntervalSeconds = 10,
        ErrorBackoffSeconds = 60
    };

    /// <summary>External calendar event reminders.</summary>
    public ExternalCalendarWorkerSchedule ExternalCalendarReminder { get; set; } = new()
    {
        IntervalSeconds = 60,
        ErrorBackoffSeconds = 60
    };

    /// <summary>Reminders on the family calendar's day notes (#60).</summary>
    public CalendarNoteWorkerSchedule CalendarNoteReminder { get; set; } = new()
    {
        IntervalSeconds = 60,
        ErrorBackoffSeconds = 60
    };

    /// <summary>Family join requests and the decisions on them.</summary>
    public WorkerSchedule FamilyJoinRequestMonitor { get; set; } = new()
    {
        IntervalSeconds = 60,
        ErrorBackoffSeconds = 60
    };

    /// <summary>Aggregated inventory activity.</summary>
    public ActivityMonitorWorkerSchedule InventoryActivityMonitor { get; set; } = new()
    {
        IntervalSeconds = 300,
        ErrorBackoffSeconds = 60
    };

    /// <summary>Aggregated shopping-list activity.</summary>
    public ActivityMonitorWorkerSchedule ShoppingListActivityMonitor { get; set; } = new()
    {
        IntervalSeconds = 300,
        ErrorBackoffSeconds = 60
    };

    /// <summary>Due and low-stock item automations.</summary>
    public WorkerSchedule ItemAutomationWorker { get; set; } = new()
    {
        IntervalSeconds = 300,
        ErrorBackoffSeconds = 300
    };

    /// <summary>The Monday weekly-summary email sweep.</summary>
    public WorkerSchedule EmailWeeklySummary { get; set; } = new()
    {
        IntervalSeconds = 3600,
        ErrorBackoffSeconds = 300
    };

    /// <summary>Expiring-product pushes and the notification retention sweep.</summary>
    public WorkerSchedule PushNotificationScheduler { get; set; } = new()
    {
        IntervalSeconds = 3600,
        ErrorBackoffSeconds = 300
    };

    private IEnumerable<(string Name, WorkerSchedule Schedule)> All()
    {
        yield return (nameof(FamilyChatNotification), FamilyChatNotification);
        yield return (nameof(ExternalCalendarReminder), ExternalCalendarReminder);
        yield return (nameof(CalendarNoteReminder), CalendarNoteReminder);
        yield return (nameof(FamilyJoinRequestMonitor), FamilyJoinRequestMonitor);
        yield return (nameof(InventoryActivityMonitor), InventoryActivityMonitor);
        yield return (nameof(ShoppingListActivityMonitor), ShoppingListActivityMonitor);
        yield return (nameof(ItemAutomationWorker), ItemAutomationWorker);
        yield return (nameof(EmailWeeklySummary), EmailWeeklySummary);
        yield return (nameof(PushNotificationScheduler), PushNotificationScheduler);
    }

    /// <summary>
    /// Validates every schedule, because <c>ValidateDataAnnotations</c> only looks at the
    /// top-level properties of the options object and would never see a <c>[Range]</c> on a
    /// nested one. Paired with <c>ValidateOnStart</c>, so a zero or negative interval stops the
    /// container rather than producing a worker that spins on an empty table.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, schedule) in All())
        {
            if (schedule is null)
            {
                yield return new ValidationResult($"{SectionName}:{name} is missing.", [name]);
                continue;
            }

            var results = new List<ValidationResult>();
            Validator.TryValidateObject(schedule, new ValidationContext(schedule), results, validateAllProperties: true);

            foreach (var result in results)
            {
                yield return new ValidationResult(
                    $"{SectionName}:{name} - {result.ErrorMessage}",
                    result.MemberNames.Select(m => $"{name}.{m}"));
            }

            if (schedule.MaxErrorBackoffSeconds < schedule.ErrorBackoffSeconds)
            {
                yield return new ValidationResult(
                    $"{SectionName}:{name} - MaxErrorBackoffSeconds must not be below ErrorBackoffSeconds.",
                    [$"{name}.{nameof(WorkerSchedule.MaxErrorBackoffSeconds)}"]);
            }
        }
    }
}

/// <summary>
/// One worker's cadence. The polling interval and the error backoff are deliberately separate
/// settings: the first is how often there is work worth looking for, the second is how long to
/// stay away from a database or a push endpoint that just failed.
/// </summary>
public class WorkerSchedule
{
    /// <summary>
    /// How often the worker runs. The lower bound is a second - anything below that is a hot loop
    /// with extra steps, and no work here is worth doing more than once a second.
    /// </summary>
    [Range(1, 86_400)]
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// How long to wait after the first failed cycle. Doubles on each consecutive failure up to
    /// <see cref="MaxErrorBackoffSeconds"/>, and resets on the first cycle that succeeds.
    /// </summary>
    [Range(1, 86_400)]
    public int ErrorBackoffSeconds { get; set; } = 60;

    /// <summary>
    /// The ceiling the backoff doubles towards. An hour, because a worker that has been failing
    /// for an hour is waiting on something a retry will not fix - and it still has to come back,
    /// because the thing it was waiting on does eventually come back.
    /// </summary>
    [Range(1, 86_400)]
    public int MaxErrorBackoffSeconds { get; set; } = 3_600;

    /// <summary>
    /// Random delay before the first tick, so nine workers do not all sweep the database in the
    /// same instant when the container starts. Capped at the interval: a ten-second worker must
    /// not be held back by a jitter window meant for an hourly one.
    /// </summary>
    [Range(0, 300)]
    public int StartupJitterSeconds { get; set; } = 10;

    public TimeSpan Interval => TimeSpan.FromSeconds(IntervalSeconds);

    public TimeSpan ErrorBackoff => TimeSpan.FromSeconds(ErrorBackoffSeconds);

    public TimeSpan MaxErrorBackoff => TimeSpan.FromSeconds(MaxErrorBackoffSeconds);

    /// <summary>The jitter window, never longer than one interval.</summary>
    public TimeSpan StartupJitter => TimeSpan.FromSeconds(Math.Min(StartupJitterSeconds, IntervalSeconds));
}

/// <summary>The two activity monitors, which also decide when a burst of edits counts as over.</summary>
public sealed class ActivityMonitorWorkerSchedule : WorkerSchedule
{
    /// <summary>
    /// How long a session has to be idle before its activity is aggregated and sent. Notifying
    /// sooner means notifying in the middle of someone's edits.
    /// </summary>
    [Range(1, 86_400)]
    public int SessionTimeoutSeconds { get; set; } = 300;

    public TimeSpan SessionTimeout => TimeSpan.FromSeconds(SessionTimeoutSeconds);
}

/// <summary>The chat worker, which holds a message back briefly in case the reader is looking.</summary>
public sealed class FamilyChatWorkerSchedule : WorkerSchedule
{
    /// <summary>
    /// How long after a message before it is worth notifying about. Short enough that a missed
    /// message still arrives promptly, long enough that a reader who is already in the chat is not
    /// pushed a message they are looking at.
    /// </summary>
    [Range(1, 3_600)]
    public int GraceWindowSeconds { get; set; } = 15;

    public TimeSpan GraceWindow => TimeSpan.FromSeconds(GraceWindowSeconds);
}

/// <summary>The day-note reminder worker (#60).</summary>
public sealed class CalendarNoteWorkerSchedule : WorkerSchedule
{
    /// <summary>
    /// How far back a reminder whose moment was missed is still sent. Covers a restart or a slow
    /// cycle; past this the note is marked as sent without a push, because "this was two weeks ago"
    /// is worse than nothing.
    /// </summary>
    [Range(1, 1_440)]
    public int CatchUpWindowMinutes { get; set; } = 60;

    public TimeSpan CatchUpWindow => TimeSpan.FromMinutes(CatchUpWindowMinutes);
}

/// <summary>The calendar reminder worker and its four windows.</summary>
public sealed class ExternalCalendarWorkerSchedule : WorkerSchedule
{
    /// <summary>
    /// How far back a reminder whose moment was missed is still sent. Covers a restart or a slow
    /// cycle; past this the reminder is dropped rather than delivered late enough to confuse.
    /// </summary>
    [Range(1, 1_440)]
    public int CatchUpWindowMinutes { get; set; } = 15;

    /// <summary>How long the "already sent" markers are kept before they are pruned.</summary>
    [Range(1, 365)]
    public int MarkerRetentionDays { get; set; } = 30;

    /// <summary>How often the marker prune runs. Far less often than the sweep itself.</summary>
    [Range(1, 168)]
    public int PruneIntervalHours { get; set; } = 6;

    /// <summary>
    /// Extra window on the event query, to absorb every timezone offset without knowing the
    /// recipient's zone at query time.
    /// </summary>
    [Range(1, 30)]
    public int LookaheadSlackDays { get; set; } = 2;

    public TimeSpan CatchUpWindow => TimeSpan.FromMinutes(CatchUpWindowMinutes);

    public TimeSpan MarkerRetention => TimeSpan.FromDays(MarkerRetentionDays);

    public TimeSpan PruneInterval => TimeSpan.FromHours(PruneIntervalHours);

    public TimeSpan LookaheadSlack => TimeSpan.FromDays(LookaheadSlackDays);
}

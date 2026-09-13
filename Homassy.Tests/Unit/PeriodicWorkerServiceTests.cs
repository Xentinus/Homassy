extern alias NotificationsProject;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using NotificationsProject::Homassy.Notifications.Configuration;
using NotificationsProject::Homassy.Notifications.Workers;

namespace Homassy.Tests.Unit;

/// <summary>
/// The loop every notification worker runs (#95): that the interval comes from configuration,
/// that repeated failures back off progressively and recovery resets that, and that a
/// misconfigured interval is refused rather than turned into a hot loop.
/// </summary>
public class PeriodicWorkerServiceTests
{
    // -------------------------------------------------------------------------
    // The interval comes from configuration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Interval_ComesFromConfiguration()
    {
        var worker = new CountingWorker(new WorkerSchedule
        {
            IntervalSeconds = 1,
            StartupJitterSeconds = 0
        });

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        // Three ticks of a one-second interval fit comfortably; three ticks of the old
        // five-minute default would not produce even one.
        await worker.WaitForCyclesAsync(3, TimeSpan.FromSeconds(10));

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.Cycles >= 3, $"expected at least 3 cycles, saw {worker.Cycles}");
    }

    [Fact]
    public async Task RunOnStartup_RunsBeforeTheFirstTick()
    {
        var worker = new CountingWorker(
            new WorkerSchedule { IntervalSeconds = 3_600, StartupJitterSeconds = 0 },
            runOnStartup: true);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        // An hourly worker: the only cycle that can have happened is the startup one.
        await worker.WaitForCyclesAsync(1, TimeSpan.FromSeconds(5));

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, worker.Cycles);
    }

    [Fact]
    public async Task WithoutRunOnStartup_WaitsForTheFirstTick()
    {
        var worker = new CountingWorker(
            new WorkerSchedule { IntervalSeconds = 3_600, StartupJitterSeconds = 0 });

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(200);

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, worker.Cycles);
    }

    [Fact]
    public async Task StartupJitter_DelaysTheFirstCycle()
    {
        var worker = new CountingWorker(
            new WorkerSchedule { IntervalSeconds = 3_600, StartupJitterSeconds = 300 },
            runOnStartup: true);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(200);

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        // The window is capped at one interval, so a jitter of up to 300s is in play here and
        // 200ms is nowhere near it. Without jitter every worker would have swept immediately.
        Assert.Equal(0, worker.Cycles);
    }

    [Fact]
    public void StartupJitter_IsNeverLongerThanOneInterval()
    {
        var schedule = new WorkerSchedule { IntervalSeconds = 10, StartupJitterSeconds = 300 };

        Assert.Equal(TimeSpan.FromSeconds(10), schedule.StartupJitter);
    }

    // -------------------------------------------------------------------------
    // Backoff grows on consecutive failures and resets on recovery
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Backoff_GrowsOnConsecutiveFailures()
    {
        var worker = new FailingWorker(new WorkerSchedule
        {
            IntervalSeconds = 1,
            ErrorBackoffSeconds = 1,
            MaxErrorBackoffSeconds = 3_600,
            StartupJitterSeconds = 0
        }, failures: 3, runOnStartup: true);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        // 1s + 2s of backoff before the third attempt, against a 1s interval: the failures are
        // what paces this, not the tick.
        await worker.WaitForCyclesAsync(3, TimeSpan.FromSeconds(20));

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, worker.Attempts);
        Assert.True(worker.GapBeforeAttempt3 > worker.GapBeforeAttempt2,
            $"backoff did not grow: {worker.GapBeforeAttempt2} then {worker.GapBeforeAttempt3}");
    }

    [Fact]
    public async Task Backoff_IsCappedAtTheConfiguredMaximum()
    {
        var worker = new FailingWorker(new WorkerSchedule
        {
            IntervalSeconds = 1,
            ErrorBackoffSeconds = 1,
            // One second of ceiling, so the second failure cannot wait the two it would double to.
            MaxErrorBackoffSeconds = 1,
            StartupJitterSeconds = 0
        }, failures: 5, runOnStartup: true);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await worker.WaitForCyclesAsync(4, TimeSpan.FromSeconds(20));

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.GapBeforeAttempt4 < TimeSpan.FromSeconds(4),
            $"backoff exceeded its cap: {worker.GapBeforeAttempt4}");
    }

    [Fact]
    public async Task Backoff_ResetsAfterASuccessfulCycle()
    {
        var worker = new FailingWorker(new WorkerSchedule
        {
            IntervalSeconds = 1,
            ErrorBackoffSeconds = 1,
            MaxErrorBackoffSeconds = 3_600,
            StartupJitterSeconds = 0
        }, failures: 1, runOnStartup: true);

        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);

        // One failure, then success: attempt 3 must come one interval after attempt 2, not one
        // doubled backoff after it.
        await worker.WaitForCyclesAsync(3, TimeSpan.FromSeconds(20));

        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        Assert.True(worker.GapBeforeAttempt3 < TimeSpan.FromSeconds(3),
            $"backoff was not reset by the successful cycle: {worker.GapBeforeAttempt3}");
    }

    // -------------------------------------------------------------------------
    // A misconfigured schedule is refused at startup
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-300)]
    public void Validation_RejectsANonPositiveInterval(int intervalSeconds)
    {
        var settings = new NotificationWorkerSettings
        {
            ItemAutomationWorker = new WorkerSchedule { IntervalSeconds = intervalSeconds }
        };

        var results = Validate(settings);

        Assert.Contains(results, r => r.ErrorMessage!.Contains("ItemAutomationWorker"));
    }

    [Fact]
    public void Validation_RejectsAMaximumBackoffBelowTheFirstOne()
    {
        var settings = new NotificationWorkerSettings
        {
            EmailWeeklySummary = new WorkerSchedule
            {
                IntervalSeconds = 3_600,
                ErrorBackoffSeconds = 600,
                MaxErrorBackoffSeconds = 60
            }
        };

        var results = Validate(settings);

        Assert.Contains(results, r => r.ErrorMessage!.Contains("MaxErrorBackoffSeconds"));
    }

    [Fact]
    public void Validation_AcceptsTheDefaults()
    {
        Assert.Empty(Validate(new NotificationWorkerSettings()));
    }

    [Fact]
    public void Defaults_MatchTheValuesThatUsedToBeCompiledIn()
    {
        var settings = new NotificationWorkerSettings();

        Assert.Equal(TimeSpan.FromSeconds(10), settings.FamilyChatNotification.Interval);
        Assert.Equal(TimeSpan.FromMinutes(1), settings.ExternalCalendarReminder.Interval);
        Assert.Equal(TimeSpan.FromMinutes(1), settings.FamilyJoinRequestMonitor.Interval);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.InventoryActivityMonitor.Interval);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.ShoppingListActivityMonitor.Interval);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.ItemAutomationWorker.Interval);
        Assert.Equal(TimeSpan.FromHours(1), settings.EmailWeeklySummary.Interval);
        Assert.Equal(TimeSpan.FromHours(1), settings.PushNotificationScheduler.Interval);

        Assert.Equal(TimeSpan.FromMinutes(1), settings.FamilyChatNotification.ErrorBackoff);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.ItemAutomationWorker.ErrorBackoff);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.PushNotificationScheduler.ErrorBackoff);

        Assert.Equal(TimeSpan.FromSeconds(15), settings.FamilyChatNotification.GraceWindow);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.InventoryActivityMonitor.SessionTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.ShoppingListActivityMonitor.SessionTimeout);
        Assert.Equal(TimeSpan.FromMinutes(15), settings.ExternalCalendarReminder.CatchUpWindow);
        Assert.Equal(TimeSpan.FromDays(30), settings.ExternalCalendarReminder.MarkerRetention);
        Assert.Equal(TimeSpan.FromHours(6), settings.ExternalCalendarReminder.PruneInterval);
        Assert.Equal(TimeSpan.FromDays(2), settings.ExternalCalendarReminder.LookaheadSlack);
    }

    private static List<ValidationResult> Validate(NotificationWorkerSettings settings)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true);
        return results;
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private class CountingWorker : PeriodicWorkerService
    {
        private readonly bool _runOnStartup;
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _target = int.MaxValue;

        public CountingWorker(WorkerSchedule schedule, bool runOnStartup = false) : base(schedule)
        {
            _runOnStartup = runOnStartup;
        }

        public int Cycles { get; private set; }

        protected override string WorkerName => "Test worker";

        protected override bool RunOnStartup => _runOnStartup;

        protected override Task DoWorkAsync(CancellationToken cancellationToken)
        {
            Cycles++;
            // Signalled before the body runs, because the body is what throws in FailingWorker.
            if (Cycles >= _target) _reached.TrySetResult();
            OnCycle();
            return Task.CompletedTask;
        }

        protected virtual void OnCycle() { }

        /// <summary>Waits until the worker has run <paramref name="cycles"/> cycles, or gives up.</summary>
        public async Task WaitForCyclesAsync(int cycles, TimeSpan timeout)
        {
            _target = cycles;
            if (Cycles >= cycles) return;
            await Task.WhenAny(_reached.Task, Task.Delay(timeout));
        }
    }

    /// <summary>Throws on its first <c>failures</c> cycles, then succeeds, recording the gaps.</summary>
    private sealed class FailingWorker : CountingWorker
    {
        private readonly int _failures;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private TimeSpan _previous;

        public FailingWorker(WorkerSchedule schedule, int failures, bool runOnStartup = false)
            : base(schedule, runOnStartup)
        {
            _failures = failures;
        }

        public int Attempts { get; private set; }

        public TimeSpan GapBeforeAttempt2 { get; private set; }

        public TimeSpan GapBeforeAttempt3 { get; private set; }

        public TimeSpan GapBeforeAttempt4 { get; private set; }

        protected override void OnCycle()
        {
            Attempts++;
            var now = _clock.Elapsed;
            var gap = now - _previous;
            _previous = now;

            switch (Attempts)
            {
                case 2: GapBeforeAttempt2 = gap; break;
                case 3: GapBeforeAttempt3 = gap; break;
                case 4: GapBeforeAttempt4 = gap; break;
            }

            if (Attempts <= _failures)
            {
                throw new InvalidOperationException($"deliberate failure {Attempts}");
            }
        }
    }
}

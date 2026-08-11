using AwesomeAssertions;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Models.Internal;
using Monica.Framework.Seeder.Services.Support;
using Monica.Modules;
using Monica.Tool.Extensions;
using Xunit;

namespace Test.Monica.Framework.Seeder;

public sealed class SeederStateTests
{
    [Fact]
    public void GetSnapshot_WhenAttemptsProgress_ShouldCaptureRunAndAttemptTimings()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var state = CreateState(timeProvider);

        state.GetSnapshot().Status.Should().Be(SeederRunStatus.Waiting);
        state.MarkSchedulerStarted();
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        state.MarkRunning(typeof(StateSeeder), 1);
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        state.MarkAttemptFailed(
            typeof(StateSeeder),
            new InvalidOperationException("outer", new ApplicationException("inner")));
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        state.MarkRunning(typeof(StateSeeder), 2);
        timeProvider.Advance(TimeSpan.FromSeconds(4));
        state.MarkAttemptSucceeded(typeof(StateSeeder));
        state.MarkSucceeded(typeof(StateSeeder));
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        state.MarkSchedulerCompleted();

        var snapshot = state.GetSnapshot();
        snapshot.Status.Should().Be(SeederRunStatus.Succeeded);
        snapshot.Duration.Should().Be(TimeSpan.FromSeconds(9));
        snapshot.TotalCount.Should().Be(1);
        snapshot.CompletedCount.Should().Be(1);
        snapshot.SucceededCount.Should().Be(1);
        var seeder = snapshot.Seeders.Should().ContainSingle().Which;
        seeder.Duration.Should().Be(TimeSpan.FromSeconds(7));
        seeder.Attempts.Should().Be(2);
        seeder.AttemptHistory.Should().HaveCount(2);
        seeder.AttemptHistory[0].Status.Should().Be(SeederAttemptStatus.Failed);
        seeder.AttemptHistory[0].Duration.Should().Be(TimeSpan.FromSeconds(2));
        seeder.AttemptHistory[0].ErrorMessage.Should().Contain("outer").And.Contain("inner");
        seeder.AttemptHistory[1].Status.Should().Be(SeederAttemptStatus.Succeeded);
        seeder.AttemptHistory[1].Duration.Should().Be(TimeSpan.FromSeconds(4));
        seeder.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void GetSnapshot_WhenUtcClockMovesBackward_ShouldKeepMonotonicDurations()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var state = CreateState(timeProvider);
        state.MarkSchedulerStarted();
        state.MarkRunning(typeof(StateSeeder), 1);

        timeProvider.AdvanceMonotonic(TimeSpan.FromSeconds(3));
        timeProvider.AdjustUtc(TimeSpan.FromHours(-2));
        state.MarkAttemptSucceeded(typeof(StateSeeder));
        state.MarkSucceeded(typeof(StateSeeder));
        state.MarkSchedulerCompleted();

        var snapshot = state.GetSnapshot();
        snapshot.Duration.Should().Be(TimeSpan.FromSeconds(3));
        var seeder = snapshot.Seeders.Should().ContainSingle().Which;
        seeder.Duration.Should().Be(TimeSpan.FromSeconds(3));
        seeder.AttemptHistory.Should().ContainSingle().Which.Duration.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void GetSnapshot_WhenFailureMessageIsLarge_ShouldExposeBoundedDiagnostics()
    {
        var state = CreateState(TimeProvider.System);
        var exception = new InvalidOperationException(new string('x', 4_096));
        state.MarkSchedulerStarted();
        state.MarkRunning(typeof(StateSeeder), 1);
        state.MarkAttemptFailed(typeof(StateSeeder), exception);
        state.MarkFailed(typeof(StateSeeder), exception);
        state.MarkSchedulerCompleted();

        var seeder = state.GetSnapshot().Seeders.Should().ContainSingle().Which;

        seeder.ErrorMessage.Should().HaveLength(2_048);
        seeder.AttemptHistory.Should().ContainSingle().Which.ErrorMessage.Should().HaveLength(2_048);
    }

    [Fact]
    public void MarkFailFastTriggered_ShouldAtomicallyExposeFailedTriggerAndAbortingRun()
    {
        var state = CreateState(TimeProvider.System);
        state.MarkSchedulerStarted();
        state.MarkRunning(typeof(StateSeeder), 1);

        state.MarkFailFastTriggered(
            typeof(StateSeeder),
            new InvalidOperationException("Expected fail-fast failure."));

        var aborting = state.GetSnapshot();
        aborting.Status.Should().Be(SeederRunStatus.Aborting);
        aborting.FailFastTriggerSeederTypeName.Should().Be(typeof(StateSeeder).GetCleanFullName());
        aborting.IsCompleted.Should().BeFalse();
        aborting.Seeders.Should().ContainSingle().Which.Status.Should().Be(SeederStatus.Failed);

        state.MarkSchedulerAborted();
        var aborted = state.GetSnapshot();
        aborted.Status.Should().Be(SeederRunStatus.Aborted);
        aborted.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void MarkSchedulerCancelled_WhenSchedulerIsWaiting_ShouldCancelEveryPendingSeeder()
    {
        var state = CreateState(TimeProvider.System);

        state.MarkSchedulerCancelled();

        var snapshot = state.GetSnapshot();
        snapshot.Status.Should().Be(SeederRunStatus.Cancelled);
        snapshot.StartedAtUtc.Should().BeNull();
        snapshot.CompletedAtUtc.Should().NotBeNull();
        snapshot.IsCompleted.Should().BeTrue();
        var seeder = snapshot.Seeders.Should().ContainSingle().Which;
        seeder.Status.Should().Be(SeederStatus.Cancelled);
        seeder.Attempts.Should().Be(0);
        seeder.StartedAtUtc.Should().BeNull();
        seeder.CompletedAtUtc.Should().NotBeNull();
        seeder.ErrorType.Should().Be(typeof(OperationCanceledException).GetCleanFullName());
        seeder.ErrorMessage.Should().Be("Seeder execution was cancelled before it could start.");
        seeder.AttemptHistory.Should().BeEmpty();
    }

    [Fact]
    public void GetSnapshot_WhenNestedGenericExceptionFails_ShouldExposeCleanTypeIdentity()
    {
        var state = CreateState(TimeProvider.System);
        var exception = new NestedDiagnosticException<Dictionary<string, List<int?>>>("Expected failure.");
        state.MarkSchedulerStarted();
        state.MarkRunning(typeof(StateSeeder), 1);
        state.MarkAttemptFailed(typeof(StateSeeder), exception);
        state.MarkFailed(typeof(StateSeeder), exception);
        state.MarkSchedulerCompleted();

        var seeder = state.GetSnapshot().Seeders.Should().ContainSingle().Which;
        var expectedTypeName = exception.GetType().GetCleanFullName();

        seeder.ErrorType.Should().Be(expectedTypeName);
        seeder.ErrorType.Should().NotContain("+").And.NotContain("`");
        seeder.AttemptHistory.Should().ContainSingle().Which.ErrorType.Should().Be(expectedTypeName);
    }

    private static SeederState CreateState(TimeProvider timeProvider)
    {
        var graph = SeederGraph.Create([typeof(StateSeeder)], new ModuleSeederOption());
        return new SeederState(graph, timeProvider);
    }

    private sealed class StateSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NestedDiagnosticException<T>(string message) : Exception(message);

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
            _timestamp += duration.Ticks;
        }

        public void AdvanceMonotonic(TimeSpan duration) => _timestamp += duration.Ticks;

        public void AdjustUtc(TimeSpan duration) => _utcNow += duration;
    }
}

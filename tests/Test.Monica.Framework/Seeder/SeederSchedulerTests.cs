using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.Execution;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Annotations;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Models.Internal;
using Monica.Framework.Seeder.Services.Support;
using Monica.Modules;
using Monica.Testing.Hosting;
using Monica.Tool.Extensions;
using Xunit;

namespace Test.Monica.Framework.Seeder;

public sealed class SeederSchedulerTests
{
    [Fact]
    public async Task RunAsync_WhenConcurrencyIsOne_ShouldStartReadySeedersInFullTypeNameOrder()
    {
        var recorder = new ExecutionRecorder();
        await using var harness = CreateHarness(
            [typeof(OrderingCSeeder), typeof(OrderingASeeder), typeof(OrderingBSeeder)],
            new ModuleSeederOption { MaxConcurrency = 1 },
            services => services.AddSingleton(recorder));

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        recorder.Events.Should().Equal("A", "B", "C");
    }

    [Fact]
    public async Task RunAsync_WhenSeveralSeedersAreReady_ShouldRespectConcurrencyBound()
    {
        var gate = new ConcurrencyGate(requiredConcurrency: 2);
        await using var harness = CreateHarness(
            [typeof(ConcurrencyASeeder), typeof(ConcurrencyBSeeder), typeof(ConcurrencyCSeeder)],
            new ModuleSeederOption { MaxConcurrency = 2 },
            services => services.AddSingleton(gate));

        var run = harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);
        await gate.RequiredConcurrencyReached.WaitAsync(TestContext.Current.CancellationToken);

        gate.MaxConcurrency.Should().Be(2);
        gate.Release();
        await run;
        harness.State.GetSnapshot().Seeders.Should().OnlyContain(static seeder => seeder.Status == SeederStatus.Succeeded);
    }

    [Fact]
    public async Task RunAsync_WhenExclusiveSeederIsReady_ShouldApplyBarrierBeforeAndAfterIt()
    {
        var gate = new ExclusiveBarrierGate();
        await using var harness = CreateHarness(
            [typeof(BarrierAConcurrentSeeder), typeof(BarrierBExclusiveSeeder), typeof(BarrierCConcurrentSeeder)],
            new ModuleSeederOption { MaxConcurrency = 3 },
            services => services.AddSingleton(gate));

        var run = harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);
        await gate.ConcurrentStarted.WaitAsync(TestContext.Current.CancellationToken);
        gate.Events.Should().Equal("A:start");

        gate.ReleaseConcurrent();
        await gate.ExclusiveStarted.WaitAsync(TestContext.Current.CancellationToken);
        gate.Events.Should().Equal("A:start", "A:end", "B:start");

        gate.ReleaseExclusive();
        await run;
        gate.Events.Should().Equal("A:start", "A:end", "B:start", "B:end", "C:start", "C:end");
    }

    [Fact]
    public async Task RunAsync_WhenRetriesAreEnabled_ShouldUseFreshScopeForEveryAttempt()
    {
        var attempts = new RetryAttemptRecorder();
        var retryDelay = new ImmediateRetryDelay();
        await using var harness = CreateHarness(
            [typeof(RetryingSeeder)],
            new ModuleSeederOption(),
            services =>
            {
                services.AddSingleton(attempts);
                services.AddScoped<AttemptScope>();
            },
            retryDelay);

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        attempts.ScopeIds.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        retryDelay.DelayCount.Should().Be(2);
        harness.State.GetSnapshot().Seeders.Should().ContainSingle().Which.Should().Match<SeederExecutionSnapshot>(
            static seeder => seeder.Status == SeederStatus.Succeeded && seeder.Attempts == 3);
        var history = harness.State.GetSnapshot().Seeders.Single().AttemptHistory;
        history.Select(static attempt => attempt.Status).Should().Equal(
            SeederAttemptStatus.Failed,
            SeederAttemptStatus.Failed,
            SeederAttemptStatus.Succeeded);
        harness.Logger.Entries.Count(static entry => entry.Level == LogLevel.Warning).Should().Be(2);
        harness.Logger.Entries.Should().NotContain(static entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RunAsync_WhenDependencyFails_ShouldBlockDependentsAndContinueIndependentBranches()
    {
        var recorder = new ExecutionRecorder();
        await using var harness = CreateHarness(
            [typeof(FailingRootSeeder), typeof(BlockedDependentSeeder), typeof(IndependentSeeder)],
            new ModuleSeederOption { MaxConcurrency = 2 },
            services => services.AddSingleton(recorder));

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        recorder.Events.Should().ContainSingle().Which.Should().Be("independent");
        var snapshot = harness.State.GetSnapshot();
        GetStatus<FailingRootSeeder>(snapshot).Should().Be(SeederStatus.Failed);
        GetStatus<BlockedDependentSeeder>(snapshot).Should().Be(SeederStatus.Blocked);
        GetStatus<IndependentSeeder>(snapshot).Should().Be(SeederStatus.Succeeded);
        snapshot.Status.Should().Be(SeederRunStatus.CompletedWithFailures);
        harness.Logger.Entries.Count(static entry => entry.Level == LogLevel.Error).Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_WhenOptionalFailFastSeederExhaustsRetries_ShouldAbortAndKeepHostTokenActive()
    {
        var retryDelay = new ImmediateRetryDelay();
        await using var harness = CreateHarness(
            [typeof(FailFastARootSeeder), typeof(FailFastBDependentSeeder), typeof(FailFastZIndependentSeeder)],
            new ModuleSeederOption { MaxConcurrency = 1 },
            retryDelay: retryDelay);
        using var hostCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        await harness.Scheduler.RunAsync(hostCancellation.Token);

        hostCancellation.IsCancellationRequested.Should().BeFalse();
        retryDelay.DelayCount.Should().Be(1);
        var snapshot = harness.State.GetSnapshot();
        snapshot.Status.Should().Be(SeederRunStatus.Aborted);
        snapshot.FailFastTriggerSeederTypeName.Should().Be(typeof(FailFastARootSeeder).GetCleanFullName());
        GetStatus<FailFastARootSeeder>(snapshot).Should().Be(SeederStatus.Failed);
        GetStatus<FailFastBDependentSeeder>(snapshot).Should().Be(SeederStatus.Blocked);
        GetStatus<FailFastZIndependentSeeder>(snapshot).Should().Be(SeederStatus.Cancelled);
        snapshot.Seeders.Single(seeder =>
            seeder.SeederTypeName == typeof(FailFastARootSeeder).GetCleanFullName())
            .AttemptHistory.Select(static attempt => attempt.Status)
            .Should().Equal(SeederAttemptStatus.Failed, SeederAttemptStatus.Failed);
        harness.Logger.Entries.Count(static entry => entry.Level == LogLevel.Warning).Should().Be(1);
        harness.Logger.Entries.Count(static entry => entry.Level == LogLevel.Error).Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_WhenFailFastCancelsInFlightSeeder_ShouldExposeAbortingUntilItDrains()
    {
        var gate = new FailFastDrainGate();
        await using var harness = CreateHarness(
            [
                typeof(AbortATriggerSeeder),
                typeof(AbortBDrainingSeeder),
                typeof(AbortCQuickCancellingSeeder),
                typeof(AbortDDependentSeeder)
            ],
            new ModuleSeederOption { MaxConcurrency = 3 },
            services => services.AddSingleton(gate));

        var run = harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);
        await gate.CancellationObserved.WaitAsync(TestContext.Current.CancellationToken);

        run.IsCompleted.Should().BeFalse();
        var aborting = harness.State.GetSnapshot();
        aborting.Status.Should().Be(SeederRunStatus.Aborting);
        aborting.FailFastTriggerSeederTypeName.Should().Be(typeof(AbortATriggerSeeder).GetCleanFullName());
        var triggerWhileDraining = aborting.Seeders.Single(seeder =>
            seeder.SeederTypeName == typeof(AbortATriggerSeeder).GetCleanFullName());
        triggerWhileDraining.Status.Should().Be(SeederStatus.Failed);
        triggerWhileDraining.CompletedAtUtc.Should().NotBeNull();
        GetStatus<AbortDDependentSeeder>(aborting).Should().Be(SeederStatus.Blocked);
        await WaitFor.UntilAsync(
            _ => Task.FromResult(
                GetStatus<AbortCQuickCancellingSeeder>(harness.State.GetSnapshot()) == SeederStatus.Cancelled),
            cancellationToken: TestContext.Current.CancellationToken);
        var quickWhileDraining = harness.State.GetSnapshot().Seeders.Single(seeder =>
            seeder.SeederTypeName == typeof(AbortCQuickCancellingSeeder).GetCleanFullName());
        quickWhileDraining.CompletedAtUtc.Should().NotBeNull();

        gate.AllowDrain();
        await run;
        var aborted = harness.State.GetSnapshot();
        aborted.Status.Should().Be(SeederRunStatus.Aborted);
        GetStatus<AbortBDrainingSeeder>(aborted).Should().Be(SeederStatus.Cancelled);
        aborted.Seeders.Single(seeder =>
                seeder.SeederTypeName == typeof(AbortATriggerSeeder).GetCleanFullName())
            .Duration.Should().Be(triggerWhileDraining.Duration);
        aborted.Seeders.Single(seeder =>
                seeder.SeederTypeName == typeof(AbortCQuickCancellingSeeder).GetCleanFullName())
            .Duration.Should().Be(quickWhileDraining.Duration);
    }

    [Fact]
    public async Task RunAsync_WhenFailFastFailuresCompleteTogether_ShouldChooseLowestFullTypeName()
    {
        var gate = new SynchronousCompletionFailureGate(requiredParticipants: 2);
        await using var harness = CreateHarness(
            [typeof(SimultaneousBFailureSeeder), typeof(SimultaneousAFailureSeeder)],
            new ModuleSeederOption { MaxConcurrency = 2 },
            services => services.AddSingleton(gate));

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = harness.State.GetSnapshot();
        snapshot.FailFastTriggerSeederTypeName.Should().Be(typeof(SimultaneousAFailureSeeder).GetCleanFullName());
        snapshot.FailedCount.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_WhenFailFastSeederFailsSynchronously_ShouldNotStartLaterReadySeeders()
    {
        var recorder = new ExecutionRecorder();
        await using var harness = CreateHarness(
            [typeof(SynchronousFailFastASeeder), typeof(SynchronousFailFastBSeeder)],
            new ModuleSeederOption { MaxConcurrency = 2 },
            services => services.AddSingleton(recorder));

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        harness.State.GetSnapshot().Status.Should().Be(SeederRunStatus.Aborted);
        recorder.Events.Should().BeEmpty();
        GetStatus<SynchronousFailFastBSeeder>(harness.State.GetSnapshot()).Should().Be(SeederStatus.Cancelled);
    }

    [Fact]
    public async Task RunAsync_WhenCancellationCallbackThrows_ShouldStillDrainAndPublishAbortedState()
    {
        var gate = new ThrowingCancellationGate();
        await using var harness = CreateHarness(
            [typeof(ThrowingCancellationATriggerSeeder), typeof(ThrowingCancellationBDrainingSeeder)],
            new ModuleSeederOption { MaxConcurrency = 2 },
            services => services.AddSingleton(gate));

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        harness.State.GetSnapshot().Status.Should().Be(SeederRunStatus.Aborted);
        harness.Logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("error while signalling cancellation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WhenUnrequestedCancellationFails_ShouldRetainTheActualException()
    {
        await using var harness = CreateHarness(
            [typeof(DiagnosticCancellationSeeder)],
            new ModuleSeederOption());

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        var seeder = harness.State.GetSnapshot().Seeders.Should().ContainSingle().Which;
        seeder.Status.Should().Be(SeederStatus.Failed);
        seeder.ErrorType.Should().Be(typeof(DiagnosticCancellationException).GetCleanFullName());
        seeder.ErrorMessage.Should().Contain("outer cancellation").And.Contain("inner reason");
    }

    [Fact]
    public async Task RunAsync_WhenSeederFailsTerminally_ShouldLogStructuredMetadataOnce()
    {
        await using var harness = CreateHarness(
            [typeof(FailingRootSeeder)],
            new ModuleSeederOption());

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        var error = harness.Logger.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error).Which;
        error.Exception.Should().BeOfType<InvalidOperationException>();
        error.Properties["SeederTypeName"].Should().Be(typeof(FailingRootSeeder).GetCleanFullName());
        error.Properties["Attempt"].Should().Be(1);
        error.Properties["MaxAttempts"].Should().Be(1);
        error.Properties["FailureBehavior"].Should().Be(SeederFailureBehavior.ContinueAndRecord);
    }

    [Fact]
    public async Task RunAsync_WhenLowerNamedSeederFailsDuringAbortDrain_ShouldKeepInitialTrigger()
    {
        var gate = new TriggerSelectionGate();
        await using var harness = CreateHarness(
            [typeof(TriggerSelectionALateFailureSeeder), typeof(TriggerSelectionBInitialFailureSeeder)],
            new ModuleSeederOption { MaxConcurrency = 2 },
            services => services.AddSingleton(gate));

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = harness.State.GetSnapshot();
        snapshot.FailFastTriggerSeederTypeName.Should().Be(
            typeof(TriggerSelectionBInitialFailureSeeder).GetCleanFullName());
        snapshot.FailedCount.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_WhenCancellationIsRequested_ShouldCancelRunningAndPendingSeeders()
    {
        var gate = new CancellationGate();
        await using var harness = CreateHarness(
            [typeof(CancellationABlockingSeeder), typeof(CancellationBPendingSeeder)],
            new ModuleSeederOption { MaxConcurrency = 1 },
            services => services.AddSingleton(gate));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Scheduler.RunAsync(cancellation.Token);
        await gate.Started.WaitAsync(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await run;

        var snapshot = harness.State.GetSnapshot();
        GetStatus<CancellationABlockingSeeder>(snapshot).Should().Be(SeederStatus.Cancelled);
        GetStatus<CancellationBPendingSeeder>(snapshot).Should().Be(SeederStatus.Cancelled);
        snapshot.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WhenSeederThrowsUnrequestedCancellation_ShouldTreatItAsFailure()
    {
        var attempts = new CancellationAttemptRecorder();
        var retryDelay = new ImmediateRetryDelay();
        await using var harness = CreateHarness(
            [typeof(SelfCancellingSeeder)],
            new ModuleSeederOption(),
            services => services.AddSingleton(attempts),
            retryDelay);

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        attempts.Count.Should().Be(3);
        retryDelay.DelayCount.Should().Be(2);
        GetStatus<SelfCancellingSeeder>(harness.State.GetSnapshot()).Should().Be(SeederStatus.Failed);
    }

    private static SeederStatus GetStatus<TSeeder>(SeederStateSnapshot snapshot)
    {
        return snapshot.Seeders.Single(seeder => seeder.SeederTypeName == typeof(TSeeder).GetCleanFullName()).Status;
    }

    private static SchedulerHarness CreateHarness(
        Type[] seederTypes,
        ModuleSeederOption options,
        Action<IServiceCollection>? configure = null,
        ISeederRetryDelay? retryDelay = null)
    {
        var graph = SeederGraph.Create(seederTypes, options);
        var services = new ServiceCollection();
        var logger = new RecordingLogger<SeederScheduler>();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(graph);
        services.AddSingleton<SeederState>();
        services.AddSingleton<IOptions<ModuleSeederOption>>(Options.Create(options));
        services.AddSingleton<ISeederRetryDelay>(retryDelay ?? new ImmediateRetryDelay());
        services.AddSingleton<SeederScheduler>();
        services.AddSingleton<ILogger<SeederScheduler>>(logger);
        services.AddScoped<IExecutionPipeline, PassThroughExecutionPipeline>();
        foreach (var seederType in seederTypes)
        {
            services.AddTransient(seederType);
        }

        configure?.Invoke(services);
        return new SchedulerHarness(services.BuildServiceProvider(validateScopes: true), logger);
    }

    private sealed class SchedulerHarness(
        ServiceProvider services,
        RecordingLogger<SeederScheduler> logger) : IAsyncDisposable
    {
        public SeederScheduler Scheduler { get; } = services.GetRequiredService<SeederScheduler>();
        public SeederState State { get; } = services.GetRequiredService<SeederState>();
        public RecordingLogger<SeederScheduler> Logger { get; } = logger;

        public ValueTask DisposeAsync() => services.DisposeAsync();
    }

    private sealed class PassThroughExecutionPipeline : IExecutionPipeline
    {
        public Task<TResult> ExecuteAsync<TInput, TResult>(
            ExecutionDescriptor descriptor,
            TInput input,
            object? target,
            ExecutionDelegate<TResult> terminal,
            CancellationToken cancellationToken = default,
            ExecutionFeatureCollection? features = null)
        {
            descriptor.TransactionMode.Should().Be(ExecutionTransactionMode.Automatic);
            return terminal();
        }

        public Task ExecuteAsync<TInput>(
            ExecutionDescriptor descriptor,
            TInput input,
            object? target,
            Func<Task> terminal,
            CancellationToken cancellationToken = default,
            ExecutionFeatureCollection? features = null)
        {
            descriptor.TransactionMode.Should().Be(ExecutionTransactionMode.Automatic);
            return terminal();
        }
    }

    private sealed class ImmediateRetryDelay : ISeederRetryDelay
    {
        private int _delayCount;

        public int DelayCount => Volatile.Read(ref _delayCount);

        public Task DelayAsync(
            int failedAttempt,
            ModuleSeederOption options,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _delayCount);
            return Task.CompletedTask;
        }
    }

    private sealed class ExecutionRecorder
    {
        private readonly object _sync = new();
        private readonly List<string> _events = [];

        public IReadOnlyList<string> Events
        {
            get
            {
                lock (_sync)
                {
                    return _events.ToArray();
                }
            }
        }

        public void Record(string value)
        {
            lock (_sync)
            {
                _events.Add(value);
            }
        }
    }

    private sealed class OrderingASeeder(ExecutionRecorder recorder) : RecordingSeeder(recorder, "A");
    private sealed class OrderingBSeeder(ExecutionRecorder recorder) : RecordingSeeder(recorder, "B");
    private sealed class OrderingCSeeder(ExecutionRecorder recorder) : RecordingSeeder(recorder, "C");

    private abstract class RecordingSeeder(ExecutionRecorder recorder, string value) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken)
        {
            recorder.Record(value);
            return Task.CompletedTask;
        }
    }

    private sealed class ConcurrencyGate(int requiredConcurrency)
    {
        private readonly TaskCompletionSource _requiredConcurrencyReached =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _maxConcurrency;

        public Task RequiredConcurrencyReached => _requiredConcurrencyReached.Task;
        public int MaxConcurrency => Volatile.Read(ref _maxConcurrency);

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            if (active >= requiredConcurrency)
            {
                _requiredConcurrencyReached.TrySetResult();
            }

            try
            {
                await _release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public void Release() => _release.TrySetResult();

        private void UpdateMaximum(int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maxConcurrency);
                if (value <= current || Interlocked.CompareExchange(ref _maxConcurrency, value, current) == current)
                {
                    return;
                }
            }
        }
    }

    private sealed class ConcurrencyASeeder(ConcurrencyGate gate) : ConcurrentSeeder(gate);
    private sealed class ConcurrencyBSeeder(ConcurrencyGate gate) : ConcurrentSeeder(gate);
    private sealed class ConcurrencyCSeeder(ConcurrencyGate gate) : ConcurrentSeeder(gate);

    private abstract class ConcurrentSeeder(ConcurrencyGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.ExecuteAsync(cancellationToken);
    }

    private sealed class ExclusiveBarrierGate
    {
        private readonly ExecutionRecorder _recorder = new();
        private readonly TaskCompletionSource _concurrentStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _exclusiveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseConcurrent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseExclusive = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ConcurrentStarted => _concurrentStarted.Task;
        public Task ExclusiveStarted => _exclusiveStarted.Task;
        public IReadOnlyList<string> Events => _recorder.Events;

        public async Task RunAAsync(CancellationToken cancellationToken)
        {
            _recorder.Record("A:start");
            _concurrentStarted.TrySetResult();
            await _releaseConcurrent.Task.WaitAsync(cancellationToken);
            _recorder.Record("A:end");
        }

        public async Task RunBAsync(CancellationToken cancellationToken)
        {
            _recorder.Record("B:start");
            _exclusiveStarted.TrySetResult();
            await _releaseExclusive.Task.WaitAsync(cancellationToken);
            _recorder.Record("B:end");
        }

        public Task RunCAsync()
        {
            _recorder.Record("C:start");
            _recorder.Record("C:end");
            return Task.CompletedTask;
        }

        public void ReleaseConcurrent() => _releaseConcurrent.TrySetResult();
        public void ReleaseExclusive() => _releaseExclusive.TrySetResult();
    }

    private sealed class BarrierAConcurrentSeeder(ExclusiveBarrierGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.RunAAsync(cancellationToken);
    }

    [SeederPolicy(ExecutionMode = SeederExecutionMode.Exclusive)]
    private sealed class BarrierBExclusiveSeeder(ExclusiveBarrierGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.RunBAsync(cancellationToken);
    }

    private sealed class BarrierCConcurrentSeeder(ExclusiveBarrierGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.RunCAsync();
    }

    private sealed class AttemptScope
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class RetryAttemptRecorder
    {
        private readonly object _sync = new();
        private readonly List<Guid> _scopeIds = [];

        public IReadOnlyList<Guid> ScopeIds
        {
            get
            {
                lock (_sync)
                {
                    return _scopeIds.ToArray();
                }
            }
        }

        public int Record(Guid scopeId)
        {
            lock (_sync)
            {
                _scopeIds.Add(scopeId);
                return _scopeIds.Count;
            }
        }
    }

    [SeederPolicy(MaxAttempts = 3)]
    private sealed class RetryingSeeder(AttemptScope scope, RetryAttemptRecorder recorder) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken)
        {
            var attempt = recorder.Record(scope.Id);
            return attempt < 3
                ? Task.FromException(new InvalidOperationException($"Failure {attempt}"))
                : Task.CompletedTask;
        }
    }

    private sealed class FailingRootSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("Expected failure."));
    }

    [SeederDependsOn<FailingRootSeeder>]
    private sealed class BlockedDependentSeeder(ExecutionRecorder recorder) : RecordingSeeder(recorder, "blocked");

    private sealed class IndependentSeeder(ExecutionRecorder recorder) : RecordingSeeder(recorder, "independent");

    private sealed class CancellationGate
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class CancellationABlockingSeeder(CancellationGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.WaitAsync(cancellationToken);
    }

    private sealed class CancellationBPendingSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CancellationAttemptRecorder
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Record() => Interlocked.Increment(ref _count);
    }

    [SeederPolicy(MaxAttempts = 3)]
    private sealed class SelfCancellingSeeder(CancellationAttemptRecorder attempts) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken)
        {
            attempts.Record();
            return Task.FromCanceled(new CancellationToken(canceled: true));
        }
    }

    [SeederPolicy(
        Criticality = SeederCriticality.Optional,
        FailureBehavior = SeederFailureBehavior.FailFast,
        MaxAttempts = 2)]
    private sealed class FailFastARootSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("Expected fail-fast failure."));
    }

    [SeederPolicy(Criticality = SeederCriticality.Optional)]
    [SeederDependsOn<FailFastARootSeeder>]
    private sealed class FailFastBDependentSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailFastZIndependentSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailFastDrainGate
    {
        private readonly TaskCompletionSource _drainersStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cancellationObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allowDrain =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _drainers;

        public Task CancellationObserved => _cancellationObserved.Task;

        public async Task FailAsync()
        {
            await _drainersStarted.Task;
            throw new InvalidOperationException("Expected fail-fast failure.");
        }

        public async Task DrainAsync(CancellationToken cancellationToken)
        {
            RegisterDrainer();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _cancellationObserved.TrySetResult();
                await _allowDrain.Task;
                throw;
            }
        }

        public async Task CancelQuicklyAsync(CancellationToken cancellationToken)
        {
            RegisterDrainer();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public void AllowDrain() => _allowDrain.TrySetResult();

        private void RegisterDrainer()
        {
            if (Interlocked.Increment(ref _drainers) == 2)
            {
                _drainersStarted.TrySetResult();
            }
        }
    }

    [SeederPolicy(FailureBehavior = SeederFailureBehavior.FailFast)]
    private sealed class AbortATriggerSeeder(FailFastDrainGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.FailAsync();
    }

    private sealed class AbortBDrainingSeeder(FailFastDrainGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.DrainAsync(cancellationToken);
    }

    private sealed class AbortCQuickCancellingSeeder(FailFastDrainGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.CancelQuicklyAsync(cancellationToken);
    }

    [SeederDependsOn<AbortATriggerSeeder>]
    private sealed class AbortDDependentSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SynchronousCompletionFailureGate(int requiredParticipants)
    {
        // Continuations intentionally execute inline. The second participant completes both seeder tasks before
        // control returns to the scheduler, creating a deterministic concurrent completion cohort.
        private readonly TaskCompletionSource _failure = new();
        private int _participants;

        public Task FailAsync()
        {
            if (Interlocked.Increment(ref _participants) == requiredParticipants)
            {
                _failure.SetException(new InvalidOperationException("Expected simultaneous failure."));
            }

            return _failure.Task;
        }
    }

    [SeederPolicy(FailureBehavior = SeederFailureBehavior.FailFast)]
    private sealed class SimultaneousAFailureSeeder(SynchronousCompletionFailureGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.FailAsync();
    }

    [SeederPolicy(FailureBehavior = SeederFailureBehavior.FailFast)]
    private sealed class SimultaneousBFailureSeeder(SynchronousCompletionFailureGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.FailAsync();
    }

    [SeederPolicy(FailureBehavior = SeederFailureBehavior.FailFast)]
    private sealed class SynchronousFailFastASeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("Expected fail-fast failure."));
    }

    private sealed class SynchronousFailFastBSeeder(ExecutionRecorder recorder) : RecordingSeeder(recorder, "started");

    private sealed class ThrowingCancellationGate
    {
        private readonly TaskCompletionSource _registered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task FailAfterRegistrationAsync()
        {
            await _registered.Task;
            throw new InvalidOperationException("Expected fail-fast failure.");
        }

        public async Task WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(static () =>
                throw new InvalidOperationException("Expected cancellation callback failure."));
            _registered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    [SeederPolicy(FailureBehavior = SeederFailureBehavior.FailFast)]
    private sealed class ThrowingCancellationATriggerSeeder(ThrowingCancellationGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.FailAfterRegistrationAsync();
    }

    private sealed class ThrowingCancellationBDrainingSeeder(ThrowingCancellationGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.WaitForCancellationAsync(cancellationToken);
    }

    private sealed class DiagnosticCancellationSeeder : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => Task.FromException(
            new DiagnosticCancellationException(
                "outer cancellation",
                new InvalidOperationException("inner reason")));
    }

    private sealed class DiagnosticCancellationException(string message, Exception innerException)
        : OperationCanceledException(message, innerException);

    private sealed class TriggerSelectionGate
    {
        private readonly TaskCompletionSource _lateSeederStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task FailAfterCancellationAsync(CancellationToken cancellationToken)
        {
            _lateSeederStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                throw new InvalidOperationException("Late failure during abort drain.", exception);
            }
        }

        public async Task FailInitiallyAsync()
        {
            await _lateSeederStarted.Task;
            throw new InvalidOperationException("Initial fail-fast failure.");
        }
    }

    [SeederPolicy(FailureBehavior = SeederFailureBehavior.FailFast)]
    private sealed class TriggerSelectionALateFailureSeeder(TriggerSelectionGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.FailAfterCancellationAsync(cancellationToken);
    }

    [SeederPolicy(FailureBehavior = SeederFailureBehavior.FailFast)]
    private sealed class TriggerSelectionBInitialFailureSeeder(TriggerSelectionGate gate) : ISeeder
    {
        public Task SeedAsync(CancellationToken cancellationToken) => gate.FailInitiallyAsync();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly object _sync = new();
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_sync)
                {
                    return _entries.ToArray();
                }
            }
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => RecordingScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            lock (_sync)
            {
                _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
            }
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class RecordingScope : IDisposable
    {
        public static RecordingScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

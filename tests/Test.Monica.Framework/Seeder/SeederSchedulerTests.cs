using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core.Execution;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Annotations;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Models.Internal;
using Monica.Framework.Seeder.Services.Support;
using Monica.Modules;
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
    public async Task RunAsync_WhenSeederReturnsCancellation_ShouldNotRetryIt()
    {
        var attempts = new CancellationAttemptRecorder();
        var retryDelay = new ImmediateRetryDelay();
        await using var harness = CreateHarness(
            [typeof(SelfCancellingSeeder)],
            new ModuleSeederOption(),
            services => services.AddSingleton(attempts),
            retryDelay);

        await harness.Scheduler.RunAsync(TestContext.Current.CancellationToken);

        attempts.Count.Should().Be(1);
        retryDelay.DelayCount.Should().Be(0);
        GetStatus<SelfCancellingSeeder>(harness.State.GetSnapshot()).Should().Be(SeederStatus.Cancelled);
    }

    private static SeederStatus GetStatus<TSeeder>(SeederStateSnapshot snapshot)
    {
        return snapshot.Seeders.Single(seeder => seeder.SeederTypeName == typeof(TSeeder).FullName).Status;
    }

    private static SchedulerHarness CreateHarness(
        Type[] seederTypes,
        ModuleSeederOption options,
        Action<IServiceCollection>? configure = null,
        ISeederRetryDelay? retryDelay = null)
    {
        var graph = SeederGraph.Create(seederTypes, options);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(graph);
        services.AddSingleton<SeederState>();
        services.AddSingleton<IOptions<ModuleSeederOption>>(Options.Create(options));
        services.AddSingleton<ISeederRetryDelay>(retryDelay ?? new ImmediateRetryDelay());
        services.AddSingleton<SeederScheduler>();
        services.AddScoped<IExecutionPipeline, PassThroughExecutionPipeline>();
        foreach (var seederType in seederTypes)
        {
            services.AddTransient(seederType);
        }

        configure?.Invoke(services);
        return new SchedulerHarness(services.BuildServiceProvider(validateScopes: true));
    }

    private sealed class SchedulerHarness(ServiceProvider services) : IAsyncDisposable
    {
        public SeederScheduler Scheduler { get; } = services.GetRequiredService<SeederScheduler>();
        public SeederState State { get; } = services.GetRequiredService<SeederState>();

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
}

using AwesomeAssertions;
using Monica.Core.Execution;
using Monica.Profiling.ExecutionTiming.Abstractions;
using Monica.Profiling.ExecutionTiming.Services.Behaviors;
using Xunit;

namespace Test.Monica.Profiling.ExecutionTiming;

public sealed class ExecutionTimingBehaviorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenTerminalSucceeds_ShouldReturnResultAndDisposeTimingScope()
    {
        var timingFactory = new TrackingExecutionTimingFactory();
        var behavior = new ExecutionTimingBehavior<string, int>(timingFactory);
        var context = CreateContext();

        var result = await behavior.ExecuteAsync(context, () => Task.FromResult(42));

        result.Should().Be(42);
        timingFactory.OperationKey.Should().Be(context.Descriptor.OperationKey);
        timingFactory.DisplayName.Should().Be(context.Descriptor.DisplayName);
        timingFactory.InvocationId.Should().Be(context.InvocationId);
        timingFactory.Description.Should().Be(
            $"{context.Descriptor.Point.Value}: {typeof(ExecutionTimingBehaviorTests).FullName}");
        timingFactory.Recorder.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTerminalThrows_ShouldDisposeTimingScopeAndRethrowSameException()
    {
        var timingFactory = new TrackingExecutionTimingFactory();
        var behavior = new ExecutionTimingBehavior<string, int>(timingFactory);
        var expected = new InvalidOperationException("terminal failed");

        var action = () => behavior.ExecuteAsync(
            CreateContext(),
            () => Task.FromException<int>(expected));

        (await action.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(expected);
        timingFactory.Recorder.DisposeCount.Should().Be(1);
    }

    private static ExecutionContext<string> CreateContext()
    {
        var descriptor = ExecutionDescriptor.ForMethod<string, int>(
            new ExecutionPoint("test.execution-timing"),
            typeof(ExecutionTimingBehaviorTests),
            entryMethod: null,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
        return new ExecutionContext<string>(
            descriptor,
            "input",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class TrackingExecutionTimingFactory : IExecutionTimingFactory
    {
        public TrackingExecutionTimingRecorder Recorder { get; } = new();

        public string? OperationKey { get; private set; }

        public string? DisplayName { get; private set; }

        public Guid? InvocationId { get; private set; }

        public string? Description { get; private set; }

        public IExecutionTimingRecorder CreateRecorder(string name, string? description = null)
        {
            throw new NotSupportedException();
        }

        public IExecutionTimingRecorder BeginScope(string name, string? description = null)
        {
            throw new NotSupportedException();
        }

        public IExecutionTimingRecorder BeginInvocation(
            string operationKey,
            string displayName,
            Guid invocationId,
            string? description = null)
        {
            OperationKey = operationKey;
            DisplayName = displayName;
            InvocationId = invocationId;
            Description = description;
            return Recorder;
        }
    }

    private sealed class TrackingExecutionTimingRecorder : IExecutionTimingRecorder
    {
        public int DisposeCount { get; private set; }

        public bool EnableLogging { get; set; }

#pragma warning disable CS0618
        public bool EnableMemoryTracking { get; set; }
#pragma warning restore CS0618

        public string? Description { get; set; }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}

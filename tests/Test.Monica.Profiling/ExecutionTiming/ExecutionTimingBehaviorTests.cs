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
        timingFactory.Name.Should().Be(context.Descriptor.OperationName);
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
        var descriptor = new ExecutionDescriptor(
            new ExecutionPoint("test.execution-timing"),
            "test operation",
            typeof(ExecutionTimingBehaviorTests),
            entryMethod: null,
            typeof(string),
            typeof(int),
            isBusinessOperation: true,
            isLongRunning: false);
        return new ExecutionContext<string>(
            descriptor,
            "input",
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private sealed class TrackingExecutionTimingFactory : IExecutionTimingFactory
    {
        public TrackingExecutionTimingRecorder Recorder { get; } = new();

        public string? Name { get; private set; }

        public string? Description { get; private set; }

        public IExecutionTimingRecorder CreateRecorder(string name, string? description = null)
        {
            throw new NotSupportedException();
        }

        public IExecutionTimingRecorder BeginScope(string name, string? description = null)
        {
            Name = name;
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

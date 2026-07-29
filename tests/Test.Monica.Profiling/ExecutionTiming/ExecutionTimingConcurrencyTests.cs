using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Monica.Profiling.ExecutionTiming.Services;
using Xunit;

namespace Test.Monica.Profiling.ExecutionTiming;

public sealed class ExecutionTimingConcurrencyTests
{
    [Fact]
    public void IdenticalConcurrentOperations_ShouldRemainIndependentlyVisibleUntilEachInvocationEnds()
    {
        const string operationKey = "test.concurrent-operation";
        const string displayName = "Concurrent operation";
        var collector = new ExecutionTimingCollector();
        var coordinator = new InlineExecutionTimingCoordinator(collector);
        var factory = new ExecutionTimingFactory(coordinator, NullLogger<ExecutionTimingFactory>.Instance);
        var firstInvocationId = Guid.NewGuid();
        var secondInvocationId = Guid.NewGuid();

        using var first = factory.BeginInvocation(operationKey, displayName, firstInvocationId);
        using var second = factory.BeginInvocation(operationKey, displayName, secondInvocationId);

        coordinator.GetRunningOperations().Keys.Should().BeEquivalentTo([firstInvocationId, secondInvocationId]);

        first.Dispose();

        coordinator.GetRunningOperations().Should().ContainSingle()
            .Which.Key.Should().Be(secondInvocationId);

        second.Dispose();

        coordinator.GetRunningOperations().Should().BeEmpty();
        var statistics = coordinator.GetStatistics(operationKey);
        statistics.Should().NotBeNull();
        statistics!.OperationKey.Should().Be(operationKey);
        statistics.DisplayName.Should().Be(displayName);
        statistics.ExecutionCount.Should().Be(2);
    }
}

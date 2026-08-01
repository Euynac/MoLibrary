using AwesomeAssertions;
using Monica.EventBus.Kafka.Providers.ConfluentKafka;
using Xunit;

namespace Test.Monica.EventBus.Kafka.Providers.ConfluentKafka;

public class KafkaEventBusSubscriptionHostedServiceTests
{
    [Fact]
    public async Task TopicConsumerTryStart_WhenStopWasRequestedFirst_ShouldNotRunConsumer()
    {
        var executionCount = 0;
        var consumer = CreateConsumer(_ =>
        {
            Interlocked.Increment(ref executionCount);
            return Task.CompletedTask;
        });

        await consumer.StopAsync().WaitAsync(TestContext.Current.CancellationToken);
        var started = consumer.TryStart();

        started.Should().BeFalse();
        Volatile.Read(ref executionCount).Should().Be(0);
    }

    [Fact]
    public async Task TopicConsumerStopAsync_WhenConsumerIsRunning_ShouldCancelAwaitAndShareShutdown()
    {
        var consumerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseConsumer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionCount = 0;
        var consumer = CreateConsumer(async activeConsumer =>
        {
            using var registration = activeConsumer.CancellationToken.Register(
                () => cancellationObserved.TrySetResult());
            Interlocked.Increment(ref executionCount);
            consumerStarted.TrySetResult();
            await releaseConsumer.Task;
        });

        consumer.TryStart().Should().BeTrue();
        await consumerStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var stopTask = consumer.StopAsync();
        var repeatedStopTask = consumer.StopAsync();
        await cancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);
        var stoppedBeforeConsumerCompleted = stopTask.IsCompleted;

        releaseConsumer.TrySetResult();
        await stopTask.WaitAsync(TestContext.Current.CancellationToken);

        repeatedStopTask.Should().BeSameAs(stopTask);
        stoppedBeforeConsumerCompleted.Should().BeFalse();
        Volatile.Read(ref executionCount).Should().Be(1);
    }

    [Fact]
    public async Task TopicConsumerRegistryStopAccepting_WhenShutdownRacesWithCreation_ShouldOwnEveryConsumer()
    {
        var consumerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new KafkaEventBusSubscriptionHostedService.TopicConsumerRegistry();
        var activeConsumer = CreateConsumer(async consumer =>
        {
            consumerStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, consumer.CancellationToken);
        });

        registry.TryAddAndStart(activeConsumer).Should().BeTrue();
        await consumerStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        registry.StopAccepting();
        var drainedConsumers = registry.Drain();
        var lateConsumer = CreateConsumer(_ => Task.CompletedTask);
        registry.TryAddAndStart(lateConsumer).Should().BeFalse();

        await lateConsumer.StopAsync().WaitAsync(TestContext.Current.CancellationToken);
        foreach (var consumer in drainedConsumers)
        {
            await consumer.StopAsync().WaitAsync(TestContext.Current.CancellationToken);
        }

        drainedConsumers.Should().ContainSingle().Which.Should().BeSameAs(activeConsumer);
        registry.Drain().Should().BeEmpty();
    }

    private static KafkaEventBusSubscriptionHostedService.TopicConsumer CreateConsumer(
        Func<KafkaEventBusSubscriptionHostedService.TopicConsumer, Task> consumeAsync)
    {
        return new KafkaEventBusSubscriptionHostedService.TopicConsumer(
            "orders",
            typeof(object),
            CancellationToken.None,
            consumeAsync);
    }
}

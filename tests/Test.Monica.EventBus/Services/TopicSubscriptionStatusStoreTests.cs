using AwesomeAssertions;
using System.Text.Json;
using Monica.EventBus.Abstractions;
using Monica.EventBus.Models;
using Monica.EventBus.Services;
using Xunit;

namespace Test.Monica.EventBus.Services;

public sealed class TopicSubscriptionStatusStoreTests
{
    [Fact]
    public void ReportState_OnUnknownTopic_CreatesEntryStartingFromSubscribing()
    {
        var store = new TopicSubscriptionStatusStore();

        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Healthy);

        var status = store.Get(null, "orders");
        status.Should().NotBeNull();
        status!.TopicName.Should().Be("orders");
        status.Provider.Should().Be(EventBusProviderKind.Dapr);
        status.State.Should().Be(TopicSubscriptionRuntimeState.Healthy);
    }

    [Fact]
    public void Get_WithDifferentServiceKeys_ShouldIsolateEntries()
    {
        var store = new TopicSubscriptionStatusStore();

        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Healthy);
        store.ReportState(
            "payments", "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Recovering);

        store.Get(null, "orders")!.State.Should().Be(TopicSubscriptionRuntimeState.Healthy);
        store.Get("payments", "orders")!.State.Should().Be(TopicSubscriptionRuntimeState.Recovering);
        store.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public void ReportState_Recovering_IncrementsCountersAndHealthyResetsConsecutiveFailures()
    {
        var store = new TopicSubscriptionStatusStore();

        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Recovering);
        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Recovering);
        var recovering = store.Get(null, "orders")!;
        recovering.ConsecutiveFailures.Should().Be(2);
        recovering.RecoveryCount.Should().Be(2);

        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Healthy);
        var healthy = store.Get(null, "orders")!;
        healthy.ConsecutiveFailures.Should().Be(0);
        healthy.RecoveryCount.Should().Be(2);
    }

    [Fact]
    public void ReportState_WithException_RecordsLastError()
    {
        var store = new TopicSubscriptionStatusStore();

        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Failed,
            "supervisor terminated", new InvalidOperationException("boom"));

        var status = store.Get(null, "orders")!;
        status.State.Should().Be(TopicSubscriptionRuntimeState.Failed);
        status.LastErrorMessage.Should().Be("supervisor terminated");
        status.LastErrorException.Should().Contain("boom");
        status.LastErrorAt.Should().NotBeNull();
        status.RecentErrors.Should().ContainSingle()
            .Which.Message.Should().Be("supervisor terminated");
    }

    [Fact]
    public void ReportError_DoesNotChangeStateButRecordsError()
    {
        var store = new TopicSubscriptionStatusStore();
        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Healthy);

        store.ReportError(null, "orders", "malformed message", new JsonException("bad payload"));

        var status = store.Get(null, "orders")!;
        status.State.Should().Be(TopicSubscriptionRuntimeState.Healthy);
        status.MessageErrorCount.Should().Be(1);
        status.LastErrorMessage.Should().Be("malformed message");
        status.LastErrorException.Should().Contain("bad payload");
    }

    [Fact]
    public void ReportError_OnUnknownTopic_CreatesEntryInSubscribingState()
    {
        var store = new TopicSubscriptionStatusStore();

        store.ReportError("keyed", "orders", "handler failed");

        var status = store.Get("keyed", "orders")!;
        status.State.Should().Be(TopicSubscriptionRuntimeState.Subscribing);
        status.MessageErrorCount.Should().Be(1);
    }

    [Fact]
    public void RecentErrors_ShouldBeBoundedToTenEntries()
    {
        var store = new TopicSubscriptionStatusStore();

        for (var i = 1; i <= 12; i++)
        {
            store.ReportError(null, "orders", $"error {i}");
        }

        var status = store.Get(null, "orders")!;
        status.MessageErrorCount.Should().Be(12);
        status.RecentErrors.Should().HaveCount(10);
        status.RecentErrors.First().Message.Should().Be("error 3");
        status.RecentErrors.Last().Message.Should().Be("error 12");
    }

    [Fact]
    public void ReportState_RaisesStatusChangedWithSnapshot()
    {
        var store = new TopicSubscriptionStatusStore();
        var received = new List<TopicSubscriptionStatus>();
        store.StatusChanged += status => received.Add(status);

        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Healthy);

        received.Should().ContainSingle();
        received[0].TopicName.Should().Be("orders");
        received[0].State.Should().Be(TopicSubscriptionRuntimeState.Healthy);
    }

    [Fact]
    public void StatusChanged_WhenHandlerThrows_ShouldNotDisruptReporting()
    {
        var store = new TopicSubscriptionStatusStore();
        store.StatusChanged += _ => throw new InvalidOperationException("observer bug");

        var act = () => store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Healthy);

        act.Should().NotThrow();
        store.Get(null, "orders")!.State.Should().Be(TopicSubscriptionRuntimeState.Healthy);
    }

    [Fact]
    public void ReportMessageProcessed_IncrementsCounterWithoutRaisingStatusChanged()
    {
        var store = new TopicSubscriptionStatusStore();
        store.ReportState(
            null, "orders", EventBusProviderKind.Dapr, TopicSubscriptionRuntimeState.Healthy);
        var events = 0;
        store.StatusChanged += _ => events++;

        store.ReportMessageProcessed(null, "orders");
        store.ReportMessageProcessed(null, "orders");

        var status = store.Get(null, "orders")!;
        status.ProcessedMessages.Should().Be(2);
        status.LastMessageReceivedAt.Should().NotBeNull();
        events.Should().Be(0);
    }

    [Fact]
    public void ReportMessageProcessed_OnUnknownTopic_CreatesEntryInSubscribingState()
    {
        var store = new TopicSubscriptionStatusStore();

        store.ReportMessageProcessed(null, "orders");

        var status = store.Get(null, "orders")!;
        status.State.Should().Be(TopicSubscriptionRuntimeState.Subscribing);
        status.ProcessedMessages.Should().Be(1);
    }

    [Fact]
    public async Task ReportMessageProcessed_IsThreadSafeUnderConcurrentCalls()
    {
        var store = new TopicSubscriptionStatusStore();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1_000; i++)
            {
                store.ReportMessageProcessed("keyed", "orders");
            }
        })));

        store.Get("keyed", "orders")!.ProcessedMessages.Should().Be(4_000);
    }
}

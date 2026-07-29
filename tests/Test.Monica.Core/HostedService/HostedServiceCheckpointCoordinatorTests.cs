using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.HostedService.Services.Support;
using Monica.Core.ObservableInstance.Services;
using Monica.Modules;
using Xunit;

namespace Test.Monica.Core.HostedService;

public sealed class HostedServiceCheckpointCoordinatorTests
{
    [Fact]
    public async Task WaitForCheckpointAsync_WithMultipleInstances_ShouldRequireKeyOrInstanceIdentity()
    {
        var first = new TestHostedService("one");
        var second = new TestHostedService("two");
        var registry = new HostedServiceRegistry();
        registry.Publish([first, second]);
        var coordinator = new HostedServiceCheckpointCoordinator(registry);
        using var firstObserver = coordinator.Observe(first);
        using var secondObserver = coordinator.Observe(second);

        var ambiguous = () => coordinator.WaitForCheckpointAsync<TestHostedService>("ready");

        await ambiguous.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Select one by service key or instance ID*");

        var keyedWait = coordinator.WaitForCheckpointAsync<TestHostedService>(
            "one",
            "ready",
            cancellationToken: TestContext.Current.CancellationToken);
        coordinator.SignalCheckpoint(first, "ready");
        await keyedWait;

        var instanceWait = coordinator.WaitForCheckpointAsync(
            second.RuntimeInfo.InstanceId,
            "ready",
            cancellationToken: TestContext.Current.CancellationToken);
        coordinator.SignalCheckpoint(second, "ready");
        await instanceWait;
    }

    private sealed class TestHostedService : IHostedService, IMoHostedService
    {
        public TestHostedService(string serviceKey)
        {
            ServiceKey = serviceKey;
            var observableRegistry = new ObservableInstanceRegistry(
                Options.Create(new ModuleObservableInstanceOption()));
            var tracker = observableRegistry.Register($"checkpoint-service-{Guid.NewGuid():N}", registration =>
            {
                registration.InstanceName = ServiceName;
                registration.InstanceType = GetType();
                registration.InstanceKey = serviceKey;
            });
            RuntimeInfo = new HostedServiceRuntimeInfo(tracker);
        }

        public string ServiceName => nameof(TestHostedService);

        public string? ServiceKey { get; }

        public string? ServiceGroupId => "Tests";

        public int MaxHistorySize => 100;

        public TimeSpan? HeartbeatInterval => null;

        public HostedServiceRuntimeInfo RuntimeInfo { get; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

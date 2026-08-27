using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity.Extensions;
using Monica.JobScheduler.ServiceDiscovery.Providers;
using Monica.JobScheduler.UI.UIJobScheduler.Abstractions;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Models;
using Monica.StateStore.Abstractions;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.UI.Modules;

public sealed class ModuleJobSchedulerServiceDiscoveryInsightTests
{
    [Fact]
    public async Task AddJobSchedulerServiceDiscoveryInsight_ShouldBridgeRegistryInstancesIntoWorkerViews()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJobScheduler(static options => options.ProjectName = "owner-a")
                .UseInMemoryStore()
                .UseSchedulerScope("insight-tests");
            monica.AddServiceDiscovery()
                .AsStandalone()
                .UseMemoryStorage();
            monica.AddJobSchedulerServiceDiscoveryInsight();
        });
        await using var host = builder.Build();

        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IJobSchedulerWorkerInsightProvider>();
        provider.Should().BeOfType<ServiceDiscoveryWorkerInsightProvider>();
        (await provider.GetWorkerInstancesAsync(TestContext.Current.CancellationToken))
            .Should()
            .BeEmpty("nothing has registered yet");

        // Seed the registry exactly the way RegistrationStateManager persists heartbeats; timestamps are chosen
        // around the default thresholds (healthy < 22.5 s, TTL < 46 s).
        var stateStore = scope.ServiceProvider.GetRequiredKeyedService<IStateStore>(nameof(ModuleServiceDiscovery));
        var now = DateTime.UtcNow;
        await stateStore.SaveStateAsync(
            "reg:app-a:instance-fresh",
            CreateInstance("app-a", "instance-fresh", "SystemService", "owner-a", now.AddSeconds(-2), leader: true),
            TestContext.Current.CancellationToken,
            TimeSpan.FromMinutes(5));
        await stateStore.SaveStateAsync(
            "reg:app-a:instance-late",
            CreateInstance("app-a", "instance-late", "SystemService", "owner-a", now.AddSeconds(-30)),
            TestContext.Current.CancellationToken,
            TimeSpan.FromMinutes(5));
        await stateStore.SaveStateAsync(
            "reg:app-b:instance-gone",
            CreateInstance("app-b", "instance-gone", "FlightService", "owner-b", now.AddMinutes(-10)),
            TestContext.Current.CancellationToken,
            TimeSpan.FromMinutes(5));

        var workers = await provider.GetWorkerInstancesAsync(TestContext.Current.CancellationToken);

        workers.Select(static worker => worker.InstanceId)
            .Should()
            .Equal("instance-fresh", "instance-late", "instance-gone");
        var fresh = workers.Single(worker => worker.InstanceId == "instance-fresh");
        fresh.AppId.Should().Be("app-a");
        fresh.AppName.Should().Be("SystemService");
        fresh.ProjectName.Should().Be("owner-a");
        fresh.IsLeader.Should().BeTrue();
        fresh.Status.Should().Be(JobSchedulerWorkerInstanceStatus.Online);
        fresh.ListeningAddresses.Should().Equal("http://localhost:5000", "https://localhost:5001");
        workers.Single(worker => worker.InstanceId == "instance-late").Status
            .Should().Be(JobSchedulerWorkerInstanceStatus.Unhealthy);
        workers.Single(worker => worker.InstanceId == "instance-gone").Status
            .Should().Be(JobSchedulerWorkerInstanceStatus.Offline);
    }

    private static InstanceState CreateInstance(
        string appId,
        string instanceId,
        string appName,
        string projectName,
        DateTime lastHeartbeat,
        bool leader = false) => new()
    {
        AppId = appId,
        InstanceId = instanceId,
        AppName = appName,
        ProjectName = projectName,
        LastHeartbeatTime = lastHeartbeat,
        RegistrationTime = lastHeartbeat.AddHours(-1),
        IsLeader = leader,
        Metadata = new Dictionary<string, string>
        {
            [IServiceDiscoveryClientInfo.LISTENING_ADDRESS_METADATA_KEY] =
                "http://localhost:5000;https://localhost:5001"
        }
    };
}

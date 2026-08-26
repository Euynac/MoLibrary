using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Extensions;
using Monica.JobScheduler.Providers;
using Monica.JobScheduler.Services;
using Monica.JobScheduler.Services.Support;
using Monica.Modules;
using Xunit;

namespace Test.Monica.JobScheduler.Modules;

public sealed class ModuleJobSchedulerCompositionTests
{
    [Fact]
    public void AddJobScheduler_WithoutStoreOrScope_ShouldFailFinalization()
    {
        var builder = WebApplication.CreateBuilder();

        var act = () => builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddJobScheduler();
        });

        act.Should().Throw<ModuleRegistrationException>()
            .Which.Message.Should().Contain("scheduler-store").And.Contain("scheduler-scope");
    }

    [Fact]
    public void AddJobScheduler_WithInMemoryStoreAndScope_ShouldRegisterBothPlanesForEveryHost()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options =>
            {
                options.ExcludeDefault();
                options.Add(typeof(ModuleJobScheduler).Assembly);
            });
            monica.AddJobScheduler(options => options.ProjectName = "owner-a")
                .UseInMemoryStore()
                .UseSchedulerScope("composition-tests");
        });
        using var host = builder.Build();

        host.Services.GetRequiredService<JobSchedulerRuntimeState>().Should().NotBeNull();
        host.Services.GetServices<IHostedService>()
            .Should().Contain(service => service is JobSchedulingHostedService)
            .And.Contain(service => service is JobExecutionWorkerHostedService);
        host.Services.GetRequiredService<JobSchedulingHostedService>().Should().NotBeNull();
        host.Services.GetRequiredService<JobExecutionWorkerHostedService>().Should().NotBeNull();
    }
}

using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Extensions;
using Xunit;

namespace Test.Monica.Core.Modularity;

public sealed class MonicaHostOwnershipTests
{
    [Fact]
    public void Build_TwoHosts_ShouldKeepCompositionStateIndependent()
    {
        using var firstHost = BuildHost("first-host");
        using var secondHost = BuildHost("second-host");

        var firstApplication = firstHost.Services.GetRequiredService<MonicaApplication>();
        var secondApplication = secondHost.Services.GetRequiredService<MonicaApplication>();
        var firstModule = GetOwnershipModule(firstApplication);
        var secondModule = GetOwnershipModule(secondApplication);
        var firstOptions = firstHost.Services.GetRequiredService<IOptions<HostOwnershipModuleOption>>().Value;
        var secondOptions = secondHost.Services.GetRequiredService<IOptions<HostOwnershipModuleOption>>().Value;

        firstApplication.Should().NotBeSameAs(secondApplication);
        firstApplication.Modules.Should().NotBeSameAs(secondApplication.Modules);
        firstApplication.Application.Should().NotBeSameAs(secondApplication.Application);
        firstApplication.ModuleSystem.Should().NotBeSameAs(secondApplication.ModuleSystem);

        firstApplication.Application.ProjectName.Should().Be("first-host");
        secondApplication.Application.ProjectName.Should().Be("second-host");
        firstApplication.ModuleSystem.DefaultApiGroupName.Should().Be("first-host-api");
        secondApplication.ModuleSystem.DefaultApiGroupName.Should().Be("second-host-api");

        firstModule.Should().NotBeSameAs(secondModule);
        firstModule.Option.HostName.Should().Be("first-host");
        secondModule.Option.HostName.Should().Be("second-host");
        firstOptions.Should().NotBeSameAs(secondOptions);
        firstOptions.HostName.Should().Be("first-host");
        secondOptions.HostName.Should().Be("second-host");

        firstHost.Services.GetRequiredService<IMonicaApplicationOptions>()
            .Should().BeSameAs(firstApplication.Application);
        secondHost.Services.GetRequiredService<IMonicaApplicationOptions>()
            .Should().BeSameAs(secondApplication.Application);
    }

    [Fact]
    public void Dispose_OneHost_ShouldLeaveTheOtherHostOperational()
    {
        var firstHost = BuildHost("first-host");
        using var secondHost = BuildHost("second-host");

        try
        {
            var firstApplication = firstHost.Services.GetRequiredService<MonicaApplication>();
            var secondApplication = secondHost.Services.GetRequiredService<MonicaApplication>();
            var firstResource = firstHost.Services.GetRequiredService<HostOwnedResource>();
            var secondResource = secondHost.Services.GetRequiredService<HostOwnedResource>();

            firstHost.Dispose();

            firstResource.IsDisposed.Should().BeTrue();
            secondResource.IsDisposed.Should().BeFalse();

            Action accessDisposedComposition = () => _ = firstApplication.Modules.Logger;
            accessDisposedComposition.Should().Throw<ObjectDisposedException>();

            secondApplication.Modules.Logger.Should().NotBeNull();
            secondApplication.Application.ProjectName.Should().Be("second-host");
            GetOwnershipModule(secondApplication).Option.HostName.Should().Be("second-host");
        }
        finally
        {
            firstHost.Dispose();
        }
    }

    [Fact]
    public async Task Build_WhenHostsAreComposedInParallel_ShouldPreserveEveryHostBoundary()
    {
        const int hostCount = 8;
        var hostTasks = Enumerable.Range(0, hostCount)
            .Select(index => Task.Run(
                () => BuildHost($"parallel-host-{index}"),
                TestContext.Current.CancellationToken))
            .ToArray();
        var hosts = await Task.WhenAll(hostTasks);

        try
        {
            var applications = hosts
                .Select(host => host.Services.GetRequiredService<MonicaApplication>())
                .ToArray();
            var moduleRegistries = applications
                .Select(application => application.Modules)
                .ToArray();

            applications.Distinct().Should().HaveCount(hostCount);
            moduleRegistries.Distinct().Should().HaveCount(hostCount);

            for (var index = 0; index < hostCount; index++)
            {
                var expectedHostName = $"parallel-host-{index}";
                var application = applications[index];
                var options = hosts[index].Services
                    .GetRequiredService<IOptions<HostOwnershipModuleOption>>()
                    .Value;

                application.Application.ProjectName.Should().Be(expectedHostName);
                application.ModuleSystem.DefaultApiGroupName.Should().Be($"{expectedHostName}-api");
                GetOwnershipModule(application).Option.HostName.Should().Be(expectedHostName);
                options.HostName.Should().Be(expectedHostName);
            }
        }
        finally
        {
            foreach (var host in hosts)
            {
                host.Dispose();
            }
        }
    }

    private static IHost BuildHost(string hostName)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureApplication(options => options.ProjectName = hostName);
            monica.ConfigureModuleSystem(options => options.DefaultApiGroupName = $"{hostName}-api");
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(MonicaHostOwnershipTests).Assembly));
            monica.AddModule<HostOwnershipModule, HostOwnershipModuleOption, HostOwnershipModuleGuide>(
                options => options.HostName = hostName);
        });

        return builder.Build();
    }

    private static HostOwnershipModule GetOwnershipModule(MonicaApplication application)
    {
        return application.Modules.RuntimeSnapshots
            .Select(snapshot => snapshot.ModuleInstance)
            .OfType<HostOwnershipModule>()
            .Single();
    }
}

[ModuleKey("Test.Monica.Core.HostOwnership")]
public sealed class HostOwnershipModule(HostOwnershipModuleOption option)
    : ModuleBase<HostOwnershipModule, HostOwnershipModuleOption, HostOwnershipModuleGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<HostOwnedResource>();
    }
}

public sealed class HostOwnershipModuleGuide
    : ModuleGuide<HostOwnershipModule, HostOwnershipModuleOption, HostOwnershipModuleGuide>;

public sealed class HostOwnershipModuleOption : ModuleOptions<HostOwnershipModule>
{
    public string HostName { get; set; } = string.Empty;
}

public sealed class HostOwnedResource : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

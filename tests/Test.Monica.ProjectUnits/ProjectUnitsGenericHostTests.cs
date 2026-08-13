using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.ProjectUnits.Abstractions;
using Xunit;

namespace Test.Monica.ProjectUnits;

public sealed class ProjectUnitsGenericHostTests
{
    [Fact]
    public void Build_WhenUsingGenericHost_ShouldRetainProjectUnitDiscoveryServices()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddProjectUnits();
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        host.Services.GetRequiredService<IProjectUnitCatalog>().Should().NotBeNull();
        application.Modules.RuntimeSnapshots.Should().ContainSingle(snapshot =>
            snapshot.ModuleType == typeof(ModuleProjectUnits)
            && snapshot.IsWebModule
            && !snapshot.RequiresWebHost);
    }

    [Fact]
    public async Task Resolve_Concurrently_ShouldPublishOneFullyConnectedCatalog()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(ProjectUnitCatalogTests).Assembly));
            monica.AddProjectUnits();
        });

        using var host = builder.Build();
        var resolutions = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(
                () => host.Services.GetRequiredService<IProjectUnitCatalog>(),
                TestContext.Current.CancellationToken))
            .ToArray();
        var catalogs = await Task.WhenAll(resolutions);

        catalogs.Should().OnlyContain(catalog => ReferenceEquals(catalog, catalogs[0]));
        catalogs[0]
            .FindByFullName(typeof(ProjectUnitCatalogTests.CompleteDomainService).FullName)!
            .DependencyUnits.Should().ContainSingle(dependency =>
                dependency.Type == typeof(ProjectUnitCatalogTests.DependencyDomainService));
    }
}

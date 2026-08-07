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
}

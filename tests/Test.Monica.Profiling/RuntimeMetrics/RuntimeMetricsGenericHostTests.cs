using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.Profiling.RuntimeMetrics.Facades;
using Xunit;

namespace Test.Monica.Profiling.RuntimeMetrics;

public sealed class RuntimeMetricsGenericHostTests
{
    [Fact]
    public void Build_WhenUsingGenericHost_ShouldRetainRuntimeMetricsServices()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddRuntimeMetrics();
        });

        using var host = builder.Build();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RuntimeMetricsFacade>().Should().NotBeNull();
        application.Modules.RuntimeSnapshots.Should().ContainSingle(snapshot =>
            snapshot.ModuleType == typeof(ModuleRuntimeMetrics)
            && snapshot.IsWebModule
            && !snapshot.RequiresWebHost);
    }
}

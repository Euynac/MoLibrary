using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.Profiling.UIMemoryAnalysis.State;
using Monica.Profiling.UIRuntimeMetrics.State;
using Xunit;

namespace Test.Monica.Profiling.Modules;

public sealed class ProfilingUIModuleTests
{
    [Fact]
    public void Build_WhenRuntimeMetricsUIIsEnabled_ShouldResolveItsPageFactory()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddRuntimeMetricsUI();
        });

        using var application = builder.Build();
        using var scope = application.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<RuntimeMetricsPageStateFactory>().Should().NotBeNull();
    }

    [Fact]
    public void Build_WhenMemoryAnalysisUIIsEnabled_ShouldResolveItsPageFactory()
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(static options => options.ExcludeDefault());
            monica.AddMemoryAnalysisUI();
        });

        using var application = builder.Build();
        using var scope = application.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<MemoryAnalysisPageStateFactory>().Should().NotBeNull();
    }
}

using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core;
using Monica.Core.Modularity.Extensions;
using Monica.Modules;
using Monica.Profiling.ExecutionTiming.Abstractions;
using Monica.Profiling.ExecutionTiming.Models;
using Xunit;

namespace Test.Monica.Profiling.ExecutionTiming;

public sealed class ExecutionTimingGenericHostTests
{
    [Theory]
    [InlineData(ExecutionTimingAggregationMode.Inline)]
    [InlineData(ExecutionTimingAggregationMode.BackgroundBatch)]
    public async Task StartAsync_WhenUsingGenericHost_ShouldRetainNonWebTimingCapabilities(
        ExecutionTimingAggregationMode aggregationMode)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddMonica(monica => monica.AddExecutionTiming(options =>
        {
            options.AggregationMode = aggregationMode;
            options.BackgroundFlushInterval = TimeSpan.FromMilliseconds(10);
        }));

        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        var factory = host.Services.GetRequiredService<IExecutionTimingFactory>();
        var query = host.Services.GetRequiredService<IExecutionTimingQuery>();
        var application = host.Services.GetRequiredService<MonicaApplication>();

        using (factory.BeginScope("test.generic-host", "Generic Host timing"))
        {
        }

        query.GetStatistics("test.generic-host").Should().Match<ExecutionTimingStatistics>(statistics =>
            statistics.ExecutionCount == 1 && statistics.DisplayName == "test.generic-host");
        application.Modules.RuntimeSnapshots.Should().ContainSingle(snapshot =>
            snapshot.ModuleType == typeof(ModuleExecutionTiming) && snapshot.IsDowngradedFromWebModule);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MapMonica_WhenUsingWebHost_ShouldUseTheSharedMinimalApiSwitch(bool enableMinimalApi)
    {
        var builder = WebApplication.CreateBuilder();
        builder.AddMonica(monica => monica.AddExecutionTiming(options =>
            options.EnableMinimalApi = enableMinimalApi));

        await using var application = builder.Build();
        application.UseMonica();
        application.MapMonica();

        var routes = ((IEndpointRouteBuilder)application).DataSources
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(static endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        if (enableMinimalApi)
        {
            routes.Should().Contain("/execution-timing/statistics");
            routes.Should().Contain("/execution-timing/running");
        }
        else
        {
            routes.Should().NotContain("/execution-timing/statistics");
            routes.Should().NotContain("/execution-timing/running");
        }
    }
}

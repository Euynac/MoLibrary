using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using Xunit;

namespace Test.Monica.HealthCheck.Modules;

public sealed class ModuleHealthCheckTests
{
    [Fact]
    public async Task ProbeEndpoints_InProduction_ShouldUseIndependentScopesAndMinimalResponses()
    {
        await using var application = await StartApplicationAsync();
        using var client = application.GetTestClient();

        using var readinessResponse = await client.GetAsync(
            "/health",
            TestContext.Current.CancellationToken);
        using var livenessResponse = await client.GetAsync(
            "/alive",
            TestContext.Current.CancellationToken);

        readinessResponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await readinessResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Be(nameof(HealthStatus.Unhealthy));
        readinessResponse.Headers.CacheControl.Should().NotBeNull();
        readinessResponse.Headers.CacheControl!.NoStore.Should().BeTrue();

        livenessResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await livenessResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Be(nameof(HealthStatus.Healthy));

        var endpoints = application.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(static source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is "/health" or "/alive")
            .ToArray();

        endpoints.Should().HaveCount(2);
        endpoints.All(endpoint =>
            endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null &&
            endpoint.Metadata.GetMetadata<MonicaEndpointMetadata>()?.Kind == MonicaEndpointKind.HealthProbe)
            .Should().BeTrue();
    }

    [Fact]
    public void ValidateOptions_WhenEnabledPathsMatch_ShouldRejectAmbiguousProbeRouting()
    {
        var module = new ModuleHealthCheck();
        var options = new ModuleHealthCheckOption
        {
            ReadinessPath = "/probe",
            LivenessPath = "/probe"
        };

        var act = () => module.ValidateOptions(options, profileName: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be distinct*");
    }

    private static async Task<WebApplication> StartApplicationAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        builder.AddMonica(monica =>
        {
            monica.ConfigureTypeDiscovery(options => options
                .ExcludeDefault()
                .Add(typeof(ModuleHealthCheck).Assembly));
            monica.AddHealthCheck();
        });
        builder.Services.AddHealthChecks()
            .AddCheck(
                "test.readiness-failure",
                static () => HealthCheckResult.Unhealthy("Not ready"),
                tags: [global::Monica.HealthCheck.HealthCheckTags.Ready]);

        var application = builder.Build();
        application.UseMonica();
        application.MapMonica();
        await application.StartAsync(TestContext.Current.CancellationToken);
        return application;
    }
}

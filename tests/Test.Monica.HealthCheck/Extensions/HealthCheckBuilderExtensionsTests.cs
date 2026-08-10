using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Monica.HealthCheck.Extensions;
using Xunit;

namespace Test.Monica.HealthCheck.Extensions;

public sealed class HealthCheckBuilderExtensionsTests
{
    [Fact]
    public void AddMonicaReadinessCheck_WhenAdditionalTagsOverlap_ShouldRegisterStableTagsOnce()
    {
        using var provider = new ServiceCollection()
            .AddHealthChecks()
            .AddMonicaReadinessCheck<HealthyTestCheck>(
                "test.ready",
                tags: ["scheduler", "READY", "monica"])
            .Services
            .BuildServiceProvider();

        var registration = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value
            .Registrations
            .Should()
            .ContainSingle()
            .Which;

        registration.Tags.Should().BeEquivalentTo(
            global::Monica.HealthCheck.HealthCheckTags.Ready,
            global::Monica.HealthCheck.HealthCheckTags.Monica,
            "scheduler");
    }

    private sealed class HealthyTestCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}

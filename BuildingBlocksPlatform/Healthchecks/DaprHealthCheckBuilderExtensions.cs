using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Healthchecks;

public static class DaprHealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddDaprHealthChecks(this IHealthChecksBuilder builder) =>
        builder.AddCheck<DaprHealthCheck>("dapr");
}
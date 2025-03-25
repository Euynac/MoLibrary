using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.RegisterCentre.Healthchecks;

public static class DaprHealthCheckBuilderExtensions
{
    public static IHealthChecksBuilder AddDaprHealthChecks(this IHealthChecksBuilder builder) =>
        builder.AddCheck<DaprHealthCheck>("dapr");
}
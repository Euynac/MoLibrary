using Microsoft.AspNetCore.Builder;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Extensions;

/// <summary>
/// Provides endpoint-convention helpers for Monica-owned web endpoints.
/// </summary>
public static class MonicaEndpointConventionExtensions
{
    /// <summary>
    /// Marks an endpoint as Monica-owned and applies the global Monica endpoint port constraint when configured.
    /// </summary>
    /// <typeparam name="TBuilder">Endpoint convention builder type.</typeparam>
    /// <param name="builder">Endpoint convention builder to configure.</param>
    /// <param name="kind">The Monica endpoint category.</param>
    /// <returns>The same endpoint convention builder.</returns>
    public static TBuilder WithMonicaEndpoint<TBuilder>(
        this TBuilder builder,
        MonicaEndpointKind kind = MonicaEndpointKind.MinimalApi)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(new MonicaEndpointMetadata(kind));
        if (kind == MonicaEndpointKind.MinimalApi)
        {
            builder.WithMetadata(MonicaMinimalApiMetadata.Instance);
        }

        var port = Mo.ModuleSystem.MonicaEndpointPort;
        if (port is not null)
        {
            builder.RequireHost($"*:{port.Value}");
        }

        return builder;
    }
}

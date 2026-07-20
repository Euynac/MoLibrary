using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.JsonSerialization.Abstractions;

namespace Monica.Core.JsonSerialization.Extensions;

/// <summary>
/// Provides registration-time access to the JSON options owned by the current Monica host.
/// </summary>
public static class JsonSerializationServiceCollectionExtensions
{
    /// <summary>
    /// Gets the finalized Monica JSON options already registered in the supplied service collection.
    /// </summary>
    /// <param name="services">The current host service collection.</param>
    /// <returns>The host-owned serializer options instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the JSON module has not run before the caller.</exception>
    public static JsonSerializerOptions GetMonicaJsonSerializerOptions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var descriptor = services.LastOrDefault(static descriptor =>
            descriptor.ServiceType == typeof(IJsonSerializerOptionsProvider));

        if (descriptor?.ImplementationInstance is IJsonSerializerOptionsProvider provider)
        {
            return provider.SerializerOptions;
        }

        throw new InvalidOperationException(
            "Monica JSON serializer options are unavailable. Declare a dependency on ModuleJsonSerialization before reading them during module registration.");
    }
}

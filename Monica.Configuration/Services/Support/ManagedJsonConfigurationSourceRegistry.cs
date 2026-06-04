using Microsoft.Extensions.Configuration;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Stores Monica-managed JSON source registrations in the shared configuration builder properties bag.
/// </summary>
internal static class ManagedJsonConfigurationSourceRegistry
{
    private const string PROPERTY_KEY = "Monica.Configuration.ManagedJsonSources";
    private static readonly object SYNC = new();
    private static readonly Dictionary<IConfiguration, List<ManagedJsonConfigurationSourceRegistration>> RUNTIME_REGISTRATIONS =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Adds a Monica-managed JSON source registration.
    /// </summary>
    public static void Add(IConfigurationBuilder builder, ManagedJsonConfigurationSourceRegistration registration)
    {
        GetOrCreate(builder).Add(registration);
        if (builder is not IConfiguration configuration)
        {
            return;
        }

        lock (SYNC)
        {
            if (!RUNTIME_REGISTRATIONS.TryGetValue(configuration, out var registrations))
            {
                registrations = [];
                RUNTIME_REGISTRATIONS[configuration] = registrations;
            }

            registrations.Add(registration);
        }
    }

    /// <summary>
    /// Reads Monica-managed JSON source registrations.
    /// </summary>
    public static IReadOnlyList<ManagedJsonConfigurationSourceRegistration> Get(IConfiguration configuration)
    {
        lock (SYNC)
        {
            return RUNTIME_REGISTRATIONS.TryGetValue(configuration, out var registrations)
                ? registrations.ToArray()
                : [];
        }
    }

    private static List<ManagedJsonConfigurationSourceRegistration> GetOrCreate(IConfigurationBuilder builder)
    {
        if (builder.Properties.TryGetValue(PROPERTY_KEY, out var value)
            && value is List<ManagedJsonConfigurationSourceRegistration> registrations)
        {
            return registrations;
        }

        registrations = [];
        builder.Properties[PROPERTY_KEY] = registrations;
        return registrations;
    }
}

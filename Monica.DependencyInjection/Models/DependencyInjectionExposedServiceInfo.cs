namespace Monica.DependencyInjection.Models;

/// <summary>
/// Describes one service identity exposed by Monica conventional registration.
/// </summary>
public sealed class DependencyInjectionExposedServiceInfo
{
    /// <summary>
    /// Gets the full service type name.
    /// </summary>
    public required string ServiceType { get; init; }

    /// <summary>
    /// Gets the compact service type display name.
    /// </summary>
    public required string ServiceTypeDisplayName { get; init; }

    /// <summary>
    /// Gets whether the exposed service is keyed.
    /// </summary>
    public required bool IsKeyedService { get; init; }

    /// <summary>
    /// Gets the service key display text when the exposed service is keyed.
    /// </summary>
    public string? ServiceKey { get; init; }
}

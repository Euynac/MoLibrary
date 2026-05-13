using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Projection;

namespace Monica.Configuration.Services;

/// <summary>
/// Coordinates reloads for the active Monica configuration provider.
/// </summary>
internal sealed class ConfigurationProviderReloadCoordinator(MonicaConfigurationProviderAccessor accessor)
    : IConfigurationReloadCoordinator
{
    /// <inheritdoc />
    public Task ReloadAsync(CancellationToken cancellationToken)
    {
        return accessor.Provider?.ReloadAsync(cancellationToken) ?? Task.CompletedTask;
    }
}

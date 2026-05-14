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
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (accessor.Provider is null)
        {
            return;
        }

        await accessor.Provider.ReloadAsync(cancellationToken);
    }
}

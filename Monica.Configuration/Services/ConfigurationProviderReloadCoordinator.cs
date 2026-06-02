using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Projection;
using Microsoft.Extensions.Configuration;

namespace Monica.Configuration.Services;

/// <summary>
/// Coordinates reloads for the active Microsoft configuration root and Monica projection provider.
/// </summary>
internal sealed class ConfigurationProviderReloadCoordinator(
    IConfiguration configuration,
    MonicaConfigurationProviderAccessor accessor)
    : IConfigurationReloadCoordinator
{
    /// <inheritdoc />
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (configuration is IConfigurationRoot root)
        {
            root.Reload();
            return;
        }

        if (accessor.Provider is null)
        {
            return;
        }

        await accessor.Provider.ReloadAsync(cancellationToken);
    }
}

using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Projection;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Coordinates reloads for the active Microsoft configuration root and Monica projection provider.
/// </summary>
internal sealed class ConfigurationProviderReloadCoordinator(
    ConfigurationRuntimeContext runtimeContext,
    MonicaConfigurationProviderAccessor accessor,
    ConfigurationRuntimeSnapshotLock runtimeSnapshotLock)
    : IConfigurationReloadCoordinator
{
    /// <inheritdoc />
    public async Task ReloadMonicaProjectionAsync(CancellationToken cancellationToken)
    {
        if (accessor.Provider is not { } provider)
        {
            return;
        }

        await runtimeSnapshotLock.ExecuteAsync(provider.ReloadAsync, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReloadMonicaProjectionAsync(string definitionKey, long? minimumVersion, CancellationToken cancellationToken)
    {
        if (accessor.Provider is not { } provider)
        {
            return;
        }

        await runtimeSnapshotLock.ExecuteAsync(
            token => provider.ReloadDefinitionAsync(definitionKey, minimumVersion, token),
            cancellationToken);
    }

    /// <inheritdoc />
    public long? GetLoadedMonicaProjectionVersion(string definitionKey)
    {
        return accessor.Provider?.GetLoadedVersion(definitionKey);
    }

    /// <inheritdoc />
    public async Task ReloadRuntimeConfigurationAsync(CancellationToken cancellationToken)
    {
        await runtimeSnapshotLock.ExecuteAsync(async token =>
        {
            if (runtimeContext.Root is { } root)
            {
                foreach (var provider in root.Providers)
                {
                    if (ReferenceEquals(provider, accessor.Provider))
                    {
                        continue;
                    }

                    provider.Load();
                }
            }

            if (accessor.Provider is { } monicaProvider)
            {
                await monicaProvider.ReloadAsync(token);
            }
        }, cancellationToken);
    }
}

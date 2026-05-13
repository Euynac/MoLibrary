using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Default mutation service that validates, routes, writes, and reloads configuration values.
/// </summary>
internal sealed class ConfigurationMutationService(
    IConfigurationDefinitionRegistry definitionRegistry,
    IEnumerable<IConfigurationValueSource> sources,
    ConfigurationValidationCoordinator validationCoordinator,
    IConfigurationReloadCoordinator reloadCoordinator,
    IEnumerable<IConfigurationChangeBroadcaster> broadcasters)
    : IConfigurationMutationService
{
    /// <inheritdoc />
    public async Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken)
    {
        var definition = definitionRegistry.GetRequired(request.DefinitionKey);
        validationCoordinator.Validate(definition, request);
        var source = ResolveSource(request.TargetSourceKey);
        var result = await source.MutateAsync(request, cancellationToken);
        await reloadCoordinator.ReloadAsync(cancellationToken);

        var notification = new ConfigurationChangeNotification
        {
            NotificationId = Guid.NewGuid().ToString("N"),
            DefinitionKey = result.DefinitionKey,
            LogicalPath = result.LogicalPath,
            SourceKey = source.Descriptor.SourceKey,
            Version = result.NewVersion,
            ChangedTime = result.ModifiedTime
        };

        foreach (var broadcaster in broadcasters)
        {
            await broadcaster.BroadcastAsync(notification, cancellationToken);
        }

        return result;
    }

    private IConfigurationValueSource ResolveSource(string? sourceKey)
    {
        var candidates = sources.ToArray();
        if (!string.IsNullOrWhiteSpace(sourceKey))
        {
            return candidates.First(x => string.Equals(x.Descriptor.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase));
        }

        return candidates
            .Where(x => x.Descriptor.IsWritable)
            .OrderByDescending(x => x.Descriptor.Priority)
            .First();
    }
}

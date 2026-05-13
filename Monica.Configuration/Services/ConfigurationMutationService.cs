using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Monica.Configuration.Utils;

namespace Monica.Configuration.Services;

/// <summary>
/// Default mutation service that validates, routes, writes, and reloads configuration values.
/// </summary>
internal sealed class ConfigurationMutationService(
    IConfigurationDefinitionRegistry definitionRegistry,
    IEnumerable<IConfigurationValueSource> sources,
    ConfigurationValidationCoordinator validationCoordinator,
    ConfigurationPathProjector pathProjector,
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
        var targetNode = ResolveTargetNode(definition, request.LogicalPath);
        var mutation = new ConfigurationSourceMutation
        {
            Request = request,
            Definition = definition,
            TargetNode = targetNode,
            SourceKey = source.Descriptor.SourceKey,
            ConfigurationPath = pathProjector.Project(definition.SectionPath, request.LogicalPath),
            Granularity = ResolveGranularity(request, targetNode)
        };
        var result = await source.MutateAsync(mutation, cancellationToken);
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

    private static ConfigurationNodeDefinition ResolveTargetNode(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var current = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            current = ResolveChild(current, segment)
                ?? throw new Exceptions.ConfigurationValidationFailedException(
                    $"Logical path '{logicalPath}' does not exist in definition '{definition.DefinitionKey}'.");
        }

        return current;
    }

    private static ConfigurationNodeDefinition? ResolveChild(ConfigurationNodeDefinition current, ConfigurationPathSegment segment)
    {
        return segment switch
        {
            PropertySegment property => current.Children.FirstOrDefault(child =>
                string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
            DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
            ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
            _ => null
        };
    }

    private static ConfigurationOverrideGranularity ResolveGranularity(
        ConfigurationMutationRequest request,
        ConfigurationNodeDefinition targetNode)
    {
        return request.MutationKind == ConfigurationMutationKind.Replace || targetNode.NodeKind != ConfigurationNodeKind.Scalar
            ? ConfigurationOverrideGranularity.Container
            : ConfigurationOverrideGranularity.Scalar;
    }
}

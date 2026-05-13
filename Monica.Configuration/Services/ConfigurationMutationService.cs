using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Metrics;
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
    IConfigurationSensitiveValueProtector sensitiveValueProtector,
    ConfigurationPathProjector pathProjector,
    IConfigurationReloadCoordinator reloadCoordinator,
    IEnumerable<IConfigurationChangeBroadcaster> broadcasters,
    ConfigurationMetricsRecorder metricsRecorder)
    : IConfigurationMutationService
{
    /// <inheritdoc />
    public async Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken)
    {
        var definition = definitionRegistry.GetRequired(request.DefinitionKey);
        validationCoordinator.Validate(definition, request);
        var source = ResolveSource(request.TargetSourceKey);
        var targetNode = ResolveTargetNode(definition, request.LogicalPath);
        ValidateEditablePath(definition, request.LogicalPath);
        var resolvedRequest = targetNode.IsSensitive
            ? request with { Value = sensitiveValueProtector.Protect(request.Value) }
            : request;
        var mutation = new ConfigurationSourceMutation
        {
            Request = resolvedRequest,
            Definition = definition,
            TargetNode = targetNode,
            SourceKey = source.Descriptor.SourceKey,
            ConfigurationPath = pathProjector.Project(definition.SectionPath, resolvedRequest.LogicalPath),
            Granularity = ResolveGranularity(resolvedRequest, targetNode)
        };
        ConfigurationMutationResult result;
        try
        {
            result = await source.MutateAsync(mutation, cancellationToken);
            metricsRecorder.RecordMutation(source.Descriptor.SourceKey);
        }
        catch (Exception ex)
        {
            metricsRecorder.RecordMutationFailure(source.Descriptor.SourceKey, ex.GetType().Name);
            throw;
        }

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

    private static void ValidateEditablePath(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var current = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            switch (segment)
            {
                case PropertySegment property:
                    current = current.Children.FirstOrDefault(child =>
                            string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new Exceptions.ConfigurationValidationFailedException(
                            $"Property '{property.Name}' does not exist in definition '{definition.DefinitionKey}'.");
                    break;
                case DictionaryKeySegment dictionaryKey:
                    if (current.DictionaryTemplate is null)
                    {
                        throw new Exceptions.ConfigurationValidationFailedException(
                            $"Path segment '{dictionaryKey.Key}' targets a non-dictionary node in definition '{definition.DefinitionKey}'.");
                    }

                    ConfigurationDictionaryKeyEscaper.ThrowIfInvalidForProjection(dictionaryKey.Key);
                    current = current.DictionaryTemplate.ValueTemplate;
                    break;
                case ListItemKeySegment itemKey:
                    if (current.ListTemplate is not { SupportsPerItemMutation: true } listTemplate)
                    {
                        throw new Exceptions.ConfigurationValidationFailedException(
                            $"List path segment '{itemKey.ItemKey}' requires a list node with a stable item key in definition '{definition.DefinitionKey}'.");
                    }

                    ConfigurationDictionaryKeyEscaper.ThrowIfInvalidForProjection(itemKey.ItemKey);
                    current = listTemplate.ItemTemplate;
                    break;
                case ListIndexSegment:
                    throw new Exceptions.ConfigurationValidationFailedException(
                        "ListIndexSegment is projection-only and cannot be used in mutation requests. Use ListItemKeySegment for per-item list mutations.");
                default:
                    throw new Exceptions.ConfigurationValidationFailedException(
                        $"Unsupported logical path segment '{segment.GetType().Name}'.");
            }
        }
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

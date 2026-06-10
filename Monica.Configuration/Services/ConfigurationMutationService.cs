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
    ConfigurationDefinitionResolver definitionResolver,
    IConfigurationEffectiveValueStore effectiveValueStore,
    IConfigurationHistoryStore historyStore,
    ConfigurationEffectiveValueSeedFactory seedFactory,
    ConfigurationEffectiveValueDocumentEditor documentEditor,
    ConfigurationValidationCoordinator validationCoordinator,
    ConfigurationPathProjector pathProjector,
    IConfigurationReloadCoordinator reloadCoordinator,
    IEnumerable<IConfigurationChangeNotifier> changeNotifiers,
    ConfigurationMetricsRecorder metricsRecorder)
    : IConfigurationMutationService
{
    /// <inheritdoc />
    public async Task<ConfigurationMutationResult> MutateAsync(ConfigurationMutationRequest request, CancellationToken cancellationToken)
    {
        var definition = await definitionResolver.GetRequiredAsync(request.DefinitionKey, cancellationToken);
        validationCoordinator.Validate(definition, request);
        var targetNode = ResolveTargetNode(definition, request.LogicalPath);
        ValidateEditablePath(definition, request.LogicalPath);
        var configurationPath = pathProjector.Project(definition.SectionPath, request.LogicalPath);
        var granularity = ResolveGranularity(targetNode);

        ConfigurationMutationResult result;
        try
        {
            var seedJson = seedFactory.CreateSeedJson(definition);
            var existingDocument = await effectiveValueStore.EnsureCreatedAsync(definition, seedJson, cancellationToken);
            var oldValue = documentEditor.ReadValue(definition, existingDocument.Json, request.LogicalPath);
            var updatedJson = documentEditor.ApplyMutation(definition, existingDocument.Json, request);
            var savedDocument = await effectiveValueStore.SaveAsync(new ConfigurationEffectiveValueSaveRequest
            {
                Definition = definition,
                Json = updatedJson,
                ExpectedVersion = request.ExpectedValueVersion,
                Context = request.Context
            }, cancellationToken);
            var newValue = request.MutationKind == ConfigurationMutationKind.Remove
                ? ConfigurationStoredValue.Null
                : documentEditor.ReadValue(definition, savedDocument.Json, request.LogicalPath) ?? ConfigurationStoredValue.Null;

            await historyStore.AppendHistoryAsync(new ConfigurationValueHistory
            {
                HistoryId = Guid.NewGuid().ToString("N"),
                DefinitionKey = definition.DefinitionKey,
                LogicalPath = request.LogicalPath,
                ConfigurationPath = configurationPath,
                MutationKind = request.MutationKind,
                Granularity = granularity,
                State = request.MutationKind == ConfigurationMutationKind.Remove
                    ? ConfigurationValueState.Removed
                    : ConfigurationValueState.Active,
                OldValue = oldValue,
                NewValue = newValue,
                Version = savedDocument.Version,
                SchemaVersion = definition.SchemaVersion,
                ModifiedTime = savedDocument.LastModifiedTime,
                ModifierId = request.Context.ModifierId,
                ModifierName = request.Context.ModifierName,
                Reason = request.Context.Reason,
                MutationGroupId = request.Context.MutationGroupId
            }, cancellationToken);

            result = new ConfigurationMutationResult
            {
                DefinitionKey = definition.DefinitionKey,
                LogicalPath = request.LogicalPath,
                NewVersion = savedDocument.Version,
                SchemaVersion = definition.SchemaVersion,
                ModifiedTime = savedDocument.LastModifiedTime
            };
            metricsRecorder.RecordMutation(effectiveValueStore.Descriptor.StoreKey);
        }
        catch (Exception ex)
        {
            metricsRecorder.RecordMutationFailure(effectiveValueStore.Descriptor.StoreKey, ex.GetType().Name);
            throw;
        }

        await reloadCoordinator.ReloadMonicaProjectionAsync(cancellationToken);

        var notification = new ConfigurationChangeNotification
        {
            NotificationId = Guid.NewGuid().ToString("N"),
            DefinitionKey = result.DefinitionKey,
            LogicalPath = result.LogicalPath,
            Version = result.NewVersion,
            ChangedTime = result.ModifiedTime
        };

        foreach (var notifier in changeNotifiers)
        {
            await notifier.NotifyAsync(notification, cancellationToken);
        }

        return result;
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

    private static ConfigurationMutationGranularity ResolveGranularity(ConfigurationNodeDefinition targetNode)
    {
        return targetNode.NodeKind != ConfigurationNodeKind.Scalar
            ? ConfigurationMutationGranularity.Container
            : ConfigurationMutationGranularity.Scalar;
    }
}

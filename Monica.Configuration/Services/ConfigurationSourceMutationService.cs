using Microsoft.Extensions.Configuration;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Applies mutations to external Microsoft configuration sources supported by Monica.
/// </summary>
internal sealed class ConfigurationSourceMutationService(
    IConfiguration configuration,
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationHistoryStore historyStore,
    IConfigurationSourceInspector sourceInspector,
    IConfigurationJsonFileSourceWriter sourceWriter,
    ConfigurationValidationCoordinator validationCoordinator,
    ConfigurationPathProjector pathProjector,
    IConfigurationReloadCoordinator reloadCoordinator)
    : IConfigurationSourceMutationService
{
    /// <summary>
    /// Mutates one external configuration source.
    /// </summary>
    public async Task<ConfigurationMutationResult> MutateAsync(
        ConfigurationSourceMutationRequest request,
        CancellationToken cancellationToken)
    {
        var definition = definitionRegistry.GetRequired(request.DefinitionKey);
        validationCoordinator.Validate(definition, new ConfigurationMutationRequest
        {
            DefinitionKey = request.DefinitionKey,
            LogicalPath = request.LogicalPath,
            MutationKind = request.MutationKind,
            Value = request.Value,
            ExpectedSchemaVersion = request.ExpectedSchemaVersion,
            Context = request.Context
        });

        var source = sourceInspector.GetRequiredSource(request.SourceKey);
        var configurationPath = pathProjector.Project(definition.SectionPath, request.LogicalPath);
        var effectiveOldValue = ReadEffectiveValue(configurationPath);
        var write = await sourceWriter.WriteAsync(
            source,
            configurationPath,
            request.MutationKind,
            request.Value,
            request.ExpectedSourceRevision,
            cancellationToken);

        await reloadCoordinator.ReloadAsync(cancellationToken);
        var effectiveNewValue = ReadEffectiveValue(configurationPath);
        var effectiveChanged = !string.Equals(effectiveOldValue?.Json, effectiveNewValue?.Json, StringComparison.Ordinal);

        var targetNode = ResolveTargetNode(definition, request.LogicalPath);
        await historyStore.AppendHistoryAsync(new ConfigurationValueHistory
        {
            HistoryId = Guid.NewGuid().ToString("N"),
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = request.LogicalPath,
            ConfigurationPath = configurationPath,
            TargetKind = ConfigurationMutationTargetKind.ExternalConfigurationSource,
            SourceKey = source.SourceKey,
            SourceProviderType = source.ProviderType,
            SourceDisplayName = source.DisplayName,
            SourcePhysicalPath = source.PhysicalPath,
            SourceConfigurationPath = configurationPath,
            MutationKind = request.MutationKind,
            Granularity = targetNode.NodeKind == ConfigurationNodeKind.Scalar
                ? ConfigurationMutationGranularity.Scalar
                : ConfigurationMutationGranularity.Container,
            State = request.MutationKind == ConfigurationMutationKind.Remove
                ? ConfigurationValueState.Removed
                : ConfigurationValueState.Active,
            OldValue = write.OldValue,
            NewValue = write.NewValue,
            Version = 0,
            TargetRevision = write.NewRevision,
            PreviousTargetRevision = write.OldRevision,
            EffectiveOldValue = effectiveOldValue,
            EffectiveNewValue = effectiveNewValue,
            EffectiveValueChanged = effectiveChanged,
            SchemaVersion = definition.SchemaVersion,
            ModifiedTime = write.ModifiedTime,
            ModifierId = request.Context.ModifierId,
            ModifierName = request.Context.ModifierName,
            Reason = request.Context.Reason,
            MutationGroupId = request.Context.MutationGroupId
        }, cancellationToken);

        return new ConfigurationMutationResult
        {
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = request.LogicalPath,
            NewVersion = 0,
            SchemaVersion = definition.SchemaVersion,
            ModifiedTime = write.ModifiedTime,
            RequiresRestart = targetNode.ReloadBehavior is ConfigurationReloadBehavior.RequiresRestart or ConfigurationReloadBehavior.StaticAfterStartup
                              || definition.ReloadBehavior is ConfigurationReloadBehavior.RequiresRestart or ConfigurationReloadBehavior.StaticAfterStartup
        };
    }

    private ConfigurationStoredValue? ReadEffectiveValue(string configurationPath)
    {
        var value = configuration[configurationPath];
        return value is null ? null : ConfigurationStoredValue.FromJson(System.Text.Json.JsonSerializer.Serialize(value));
    }

    private static ConfigurationNodeDefinition ResolveTargetNode(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        return EnumerateNodes(definition.Root).FirstOrDefault(node => node.RelativePath.Equals(logicalPath))
               ?? throw new InvalidOperationException($"Logical path '{logicalPath}' does not exist in definition '{definition.DefinitionKey}'.");
    }

    private static IEnumerable<ConfigurationNodeDefinition> EnumerateNodes(ConfigurationNodeDefinition node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateNodes(child))
            {
                yield return descendant;
            }
        }
    }
}

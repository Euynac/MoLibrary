using Monica.Configuration.Abstractions;
using Monica.Configuration.Abstractions.Internal;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Applies mutations to external Microsoft configuration sources supported by Monica.
/// </summary>
internal sealed class ConfigurationSourceMutationService(
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
        var write = await sourceWriter.WriteAsync(
            source,
            configurationPath,
            request.MutationKind,
            request.Value,
            request.ExpectedSourceRevision,
            cancellationToken);

        await reloadCoordinator.ReloadRuntimeConfigurationAsync(cancellationToken);

        var targetNode = ResolveTargetNode(definition, request.LogicalPath);
        await historyStore.AppendHistoryAsync(new ConfigurationValueHistory
        {
            HistoryId = Guid.NewGuid().ToString("N"),
            DefinitionKey = definition.DefinitionKey,
            LogicalPath = request.LogicalPath,
            ConfigurationPath = configurationPath,
            TargetKind = ConfigurationMutationTargetKind.ExternalConfigurationSource,
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
            SourceRevisionBefore = write.OldRevision,
            SourceRevisionAfter = write.NewRevision,
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

    private static ConfigurationNodeDefinition ResolveTargetNode(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var current = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            current = segment switch
            {
                PropertySegment property => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
                DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
                ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                throw new InvalidOperationException(
                    $"Logical path '{logicalPath}' does not exist in definition '{definition.DefinitionKey}'.");
            }
        }

        return current;
    }
}

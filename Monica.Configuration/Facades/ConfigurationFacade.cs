using Microsoft.Extensions.Configuration;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Facades;

/// <summary>
/// Host-facing entry point for configuration management APIs and UI consumers.
/// </summary>
public sealed class ConfigurationFacade(
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationMutationService mutationService,
    IConfigurationHistoryService historyService,
    IConfigurationMutationGroupService mutationGroupService,
    IConfigurationRollbackService rollbackService,
    IConfigurationEffectiveValueStore effectiveValueStore,
    IConfigurationHistoryStore historyStore,
    IConfigurationMetadataStore metadataStore,
    IEnumerable<IConfigurationChangeNotifier> changeNotifiers,
    IConfigurationStoreStateTracker storeStateTracker,
    ConfigurationEffectiveValueDocumentEditor documentEditor,
    ConfigurationStoredValueCodec codec,
    IConfiguration configuration)
{
    /// <summary>
    /// Gets all configuration definition summaries.
    /// </summary>
    /// <returns>Definition summaries.</returns>
    public Task<Res<IReadOnlyList<ConfigurationDefinitionSummary>>> GetDefinitionsAsync()
    {
        IReadOnlyList<ConfigurationDefinitionSummary> summaries = definitionRegistry.GetAll()
            .Select(definition => new ConfigurationDefinitionSummary
            {
                DefinitionKey = definition.DefinitionKey,
                SectionPath = definition.SectionPath,
                DisplayName = definition.DisplayName,
                ClrTypeName = definition.ClrTypeName,
                OwnerModule = definition.OwnerModule,
                SchemaVersion = definition.SchemaVersion
            })
            .ToArray();

        return Task.FromResult(Res.Ok(summaries));
    }

    /// <summary>
    /// Gets one configuration definition.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <returns>The definition detail.</returns>
    public Task<Res<ConfigurationDefinitionDetail>> GetDefinitionAsync(string definitionKey)
    {
        try
        {
            var detail = new ConfigurationDefinitionDetail
            {
                Definition = definitionRegistry.GetRequired(definitionKey)
            };
            return Task.FromResult<Res<ConfigurationDefinitionDetail>>(detail);
        }
        catch (ConfigurationDefinitionNotFoundException ex)
        {
            return Task.FromResult<Res<ConfigurationDefinitionDetail>>(Res.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<ConfigurationDefinitionDetail>>(Res.Fail($"Failed to get configuration definition: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets the display-safe effective value for one configuration path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <returns>The effective value, if one exists.</returns>
    public async Task<Res<ConfigurationEffectiveValue>> GetEffectiveValueAsync(string definitionKey, LogicalPath logicalPath)
    {
        try
        {
            var definition = definitionRegistry.GetRequired(definitionKey);
            var targetNode = ResolveTargetNode(definition, logicalPath);
            var isSensitive = targetNode?.IsSensitive is true;
            var document = await effectiveValueStore.GetAsync(definitionKey, CancellationToken.None);
            var value = document is null ? null : documentEditor.ReadValue(definition, document.Json, logicalPath);

            return new ConfigurationEffectiveValue
            {
                DefinitionKey = definitionKey,
                LogicalPath = logicalPath,
                ConfigurationPath = targetNode?.ConfigurationPath,
                DisplayValue = value is null || isSensitive ? null : codec.ToConfigurationString(value),
                IsSensitive = isSensitive,
                Version = document?.Version
            };
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get effective configuration value: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets the active configuration storage overview.
    /// </summary>
    /// <returns>The storage overview.</returns>
    public Task<Res<ConfigurationStorageOverview>> GetStorageOverviewAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok(new ConfigurationStorageOverview
            {
                EffectiveValueStore = effectiveValueStore.Descriptor,
                HistoryStore = historyStore.Descriptor,
                MetadataStore = metadataStore.Descriptor,
                HasChangeNotifier = changeNotifiers.Any()
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<ConfigurationStorageOverview>>(
                Res.Fail($"Failed to get configuration storage overview: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets all runtime store states currently known by this process.
    /// </summary>
    /// <returns>The store states.</returns>
    public Task<Res<IReadOnlyList<ConfigurationStoreState>>> GetStoreStatesAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok(storeStateTracker.GetStates()));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<IReadOnlyList<ConfigurationStoreState>>>(
                Res.Fail($"Failed to get configuration store states: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets the current Microsoft configuration debug view.
    /// </summary>
    /// <returns>The debug view text.</returns>
    public Task<Res<string>> GetDebugViewAsync()
    {
        try
        {
            var debugView = configuration is IConfigurationRoot root
                ? root.GetDebugView()
                : configuration.AsEnumerable().OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => $"{pair.Key}={pair.Value}")
                    .JoinAsString(Environment.NewLine);

            return Task.FromResult(Res.Ok<string>(debugView));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<string>>(Res.Fail($"Failed to get configuration debug view: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Mutates a configuration value.
    /// </summary>
    /// <param name="request">The mutation request.</param>
    /// <returns>The mutation result.</returns>
    public async Task<Res<ConfigurationMutationResult>> MutateAsync(ConfigurationMutationRequest request)
    {
        try
        {
            return await mutationService.MutateAsync(request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to mutate configuration value: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets mutation history for one value.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <returns>History records.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationValueHistory>>> GetHistoryAsync(string definitionKey, LogicalPath logicalPath)
    {
        try
        {
            var history = await historyService.GetHistoryAsync(definitionKey, logicalPath, CancellationToken.None);
            return Res.Ok(history);
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration value history: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Queries mutation history across definitions and paths.
    /// </summary>
    /// <param name="from">Earliest modification time to include.</param>
    /// <param name="to">Latest modification time to include.</param>
    /// <param name="definitionKey">Definition key filter.</param>
    /// <param name="logicalPath">Logical path filter.</param>
    /// <param name="mutationGroupId">Mutation group filter.</param>
    /// <returns>The matching history rows.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationValueHistory>>> QueryHistoryAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? definitionKey = null,
        LogicalPath? logicalPath = null,
        string? mutationGroupId = null)
    {
        try
        {
            return Res.Ok(await historyService.QueryHistoryAsync(from, to, definitionKey, logicalPath, mutationGroupId, CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to query configuration value history: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Creates a persisted mutation group.
    /// </summary>
    /// <param name="label">The group label.</param>
    /// <param name="reason">The optional mutation reason.</param>
    /// <param name="context">Optional audit context.</param>
    /// <returns>The created group.</returns>
    public async Task<Res<ConfigurationMutationGroup>> BeginMutationGroupAsync(
        string label,
        string? reason,
        ConfigurationMutationContext? context = null)
    {
        try
        {
            return Res.Ok(await mutationGroupService.BeginAsync(label, reason, context ?? new ConfigurationMutationContext(), CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to begin configuration mutation group: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Marks a persisted mutation group as fully applied.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="mutationCount">The number of applied mutations.</param>
    /// <param name="definitionKeys">The distinct touched definition keys.</param>
    /// <returns>Operation result.</returns>
    public async Task<Res> CompleteMutationGroupAsync(string groupId, int mutationCount, IReadOnlyList<string> definitionKeys)
    {
        try
        {
            await mutationGroupService.CompleteAsync(groupId, mutationCount, definitionKeys, CancellationToken.None);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to complete configuration mutation group: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Marks a persisted mutation group as partially applied.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="successfulCount">The number of successful mutations.</param>
    /// <param name="definitionKeys">The distinct touched definition keys.</param>
    /// <returns>Operation result.</returns>
    public async Task<Res> MarkMutationGroupPartialAsync(string groupId, int successfulCount, IReadOnlyList<string> definitionKeys)
    {
        try
        {
            await mutationGroupService.MarkPartialAsync(groupId, successfulCount, definitionKeys, CancellationToken.None);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to mark configuration mutation group partial: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Lists mutation groups.
    /// </summary>
    /// <param name="from">Earliest creation time to include.</param>
    /// <param name="to">Latest creation time to include.</param>
    /// <param name="definitionKey">Definition key filter.</param>
    /// <returns>The matching groups.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationMutationGroup>>> GetMutationGroupsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? definitionKey = null)
    {
        try
        {
            return Res.Ok(await mutationGroupService.ListAsync(from, to, definitionKey, CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration mutation groups: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets one mutation group.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <returns>The group.</returns>
    public async Task<Res<ConfigurationMutationGroup>> GetMutationGroupAsync(string groupId)
    {
        try
        {
            var group = await mutationGroupService.GetAsync(groupId, CancellationToken.None);
            return group is null
                ? Res.Fail($"Configuration mutation group '{groupId}' was not found.")
                : Res.Ok(group);
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration mutation group: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets history rows for a mutation group.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <returns>The group history rows.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationValueHistory>>> GetGroupHistoryAsync(string groupId)
    {
        try
        {
            return Res.Ok(await mutationGroupService.GetGroupHistoryAsync(groupId, CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration mutation group history: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Rolls one history row back to its previous value.
    /// </summary>
    /// <param name="historyId">The history record identity.</param>
    /// <param name="reason">Optional rollback reason.</param>
    /// <returns>The rollback mutation result.</returns>
    public async Task<Res<ConfigurationMutationResult>> RollbackHistoryAsync(string historyId, string? reason = null)
    {
        try
        {
            return Res.Ok(await rollbackService.RollbackHistoryAsync(
                historyId,
                new ConfigurationMutationContext { Reason = reason },
                CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to roll back configuration history: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Rolls one mutation group back in reverse history order.
    /// </summary>
    /// <param name="groupId">The group identity.</param>
    /// <param name="reason">Optional rollback reason.</param>
    /// <returns>The rollback mutation results.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationMutationResult>>> RollbackGroupAsync(string groupId, string? reason = null)
    {
        try
        {
            return Res.Ok(await rollbackService.RollbackGroupAsync(
                groupId,
                new ConfigurationMutationContext { Reason = reason },
                CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to roll back configuration mutation group: {ex.GetMessageRecursively()}");
        }
    }

    private static ConfigurationNodeDefinition? ResolveTargetNode(ConfigurationDefinition definition, LogicalPath logicalPath)
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
                return null;
            }
        }

        return current;
    }
}

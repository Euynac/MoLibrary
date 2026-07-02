using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;
using Monica.Configuration.Services;
using Monica.Configuration.Services.Support;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Facades;

/// <summary>
/// Host-facing entry point for configuration management APIs and UI consumers.
/// </summary>
public sealed class ConfigurationFacade(
    ConfigurationDefinitionResolver definitionResolver,
    IConfigurationMutationService mutationService,
    IConfigurationSourceMutationService sourceMutationService,
    IConfigurationHistoryService historyService,
    IConfigurationMutationGroupService mutationGroupService,
    IConfigurationRollbackService rollbackService,
    IConfigurationUnifiedVersionService unifiedVersionService,
    IConfigurationEffectiveValueStore effectiveValueStore,
    IConfigurationHistoryStore historyStore,
    IConfigurationMetadataStore metadataStore,
    IEnumerable<IConfigurationChangeNotifier> changeNotifiers,
    IConfigurationStoreStateTracker storeStateTracker,
    ConfigurationEffectiveValueSeedFactory seedFactory,
    ConfigurationEffectiveValueDocumentEditor documentEditor,
    IConfigurationSourceInspector sourceInspector,
    IConfigurationJsonFileSourceWriter sourceWriter,
    ConfigurationRuntimeContext runtimeContext,
    IConfigurationRuntimeValidationService runtimeValidationService,
    IConfigurationRuntimeReloadService runtimeReloadService)
{
    /// <summary>
    /// Gets all configuration definition summaries.
    /// </summary>
    /// <returns>Definition summaries.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationDefinitionSummary>>> GetDefinitionsAsync()
    {
        try
        {
            IReadOnlyList<ConfigurationDefinitionSummary> summaries = (await definitionResolver.GetMergedDefinitionsAsync(CancellationToken.None))
                .Select(ToSummary)
                .ToArray();

            return Res.Ok(summaries);
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration definitions: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets one configuration definition.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <returns>The definition detail.</returns>
    public async Task<Res<ConfigurationDefinitionDetail>> GetDefinitionAsync(string definitionKey)
    {
        try
        {
            var detail = new ConfigurationDefinitionDetail
            {
                Definition = await definitionResolver.GetRequiredAsync(definitionKey, CancellationToken.None)
            };
            return detail;
        }
        catch (ConfigurationDefinitionNotFoundException ex)
        {
            return Res.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration definition: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets source-aware runtime validation diagnostics for the current process.
    /// </summary>
    /// <returns>The runtime validation report.</returns>
    public Task<Res<ConfigurationValidationReport>> GetRuntimeValidationReportAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok(runtimeValidationService.GetReport()));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<ConfigurationValidationReport>>(
                Res.Fail($"Failed to get runtime configuration validation report: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets runtime reload status for Monica-managed configuration definitions.
    /// </summary>
    /// <returns>The runtime reload status report.</returns>
    public async Task<Res<ConfigurationReloadStatusReport>> GetRuntimeReloadStatusAsync()
    {
        try
        {
            return Res.Ok(await runtimeReloadService.GetStatusAsync(CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get runtime configuration reload status: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Reloads runtime configuration providers and returns the refreshed reload status report.
    /// </summary>
    /// <returns>The refreshed runtime reload status report.</returns>
    public async Task<Res<ConfigurationReloadStatusReport>> ReloadRuntimeConfigurationAsync()
    {
        try
        {
            return Res.Ok(await runtimeReloadService.ReloadAsync(CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to reload runtime configuration: {ex.GetMessageRecursively()}");
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
            var definition = await definitionResolver.GetRequiredAsync(definitionKey, CancellationToken.None);
            var targetNode = ResolveTargetNode(definition, logicalPath);
            var isSensitive = targetNode?.IsSensitive is true;
            var document = await effectiveValueStore.GetAsync(definitionKey, CancellationToken.None);
            var configurationPath = targetNode?.ConfigurationPath ?? ProjectPath(definition, logicalPath);
            var runtimeSourceValue = definition.Origin == ConfigurationDefinitionOrigin.LocalScan
                                     && targetNode?.NodeKind == ConfigurationNodeKind.Scalar
                ? sourceInspector.GetSourceChain(definition, logicalPath).Values.FirstOrDefault(value => value.IsEffective)
                : null;

            if (runtimeSourceValue is not null)
            {
                return new ConfigurationEffectiveValue
                {
                    DefinitionKey = definitionKey,
                    LogicalPath = logicalPath,
                    ConfigurationPath = configurationPath,
                    DisplayValue = runtimeSourceValue.DisplayValue,
                    IsSensitive = runtimeSourceValue.IsSensitive,
                    Version = runtimeSourceValue.Source.Kind == ConfigurationSourceKind.MonicaEffectiveStore ? document?.Version : null,
                    EffectiveSource = runtimeSourceValue.Source
                };
            }

            if (definition.Origin == ConfigurationDefinitionOrigin.PublishedMetadata)
            {
                var valueJson = document?.Json ?? seedFactory.CreateSeedJson(definition);
                var storedValue = documentEditor.ReadValue(definition, valueJson, logicalPath);
                return new ConfigurationEffectiveValue
                {
                    DefinitionKey = definitionKey,
                    LogicalPath = logicalPath,
                    ConfigurationPath = configurationPath,
                    DisplayValue = isSensitive ? null : ToDisplayValue(storedValue, targetNode),
                    IsSensitive = isSensitive,
                    Version = document?.Version,
                    EffectiveSource = targetNode?.NodeKind == ConfigurationNodeKind.Scalar ? BuildEffectiveStoreSource() : null
                };
            }

            var displayValue = targetNode is null || targetNode.NodeKind == ConfigurationNodeKind.Scalar
                ? runtimeContext.Configuration[configurationPath]
                : seedFactory.CreateRuntimeJson(targetNode, configurationPath);

            return new ConfigurationEffectiveValue
            {
                DefinitionKey = definitionKey,
                LogicalPath = logicalPath,
                ConfigurationPath = configurationPath,
                DisplayValue = isSensitive ? null : displayValue,
                IsSensitive = isSensitive,
                Version = document?.Version
            };
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get effective configuration value: {ex.GetMessageRecursively()}");
        }
    }

    private static string ProjectPath(ConfigurationDefinition definition, LogicalPath logicalPath)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(definition.SectionPath))
        {
            parts.Add(definition.SectionPath);
        }

        parts.AddRange(logicalPath.Segments.Select(segment => segment.Value));
        return string.Join(':', parts);
    }

    private ConfigurationSourceDescriptor BuildEffectiveStoreSource()
    {
        return new ConfigurationSourceDescriptor
        {
            SourceKey = effectiveValueStore.Descriptor.StoreKey,
            DisplayName = effectiveValueStore.Descriptor.DisplayName,
            ProviderType = effectiveValueStore.Descriptor.Kind.ToString(),
            Kind = ConfigurationSourceKind.MonicaEffectiveStore,
            IsManagedByMonica = true,
            IsWritable = effectiveValueStore.Descriptor.IsWritable
        };
    }

    private static ConfigurationDefinitionSummary ToSummary(ConfigurationDefinition definition)
    {
        return new ConfigurationDefinitionSummary
        {
            DefinitionKey = definition.DefinitionKey,
            SectionPath = definition.SectionPath,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            ClrTypeName = definition.ClrTypeName,
            FromProject = definition.FromProject,
            Category = definition.Category,
            SchemaVersion = definition.SchemaVersion,
            SchemaHash = definition.SchemaHash,
            Origin = definition.Origin
        };
    }

    private static string? ToDisplayValue(ConfigurationStoredValue? storedValue, ConfigurationNodeDefinition? node)
    {
        if (storedValue is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(storedValue.Json);
            if (document.RootElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            if (node?.NodeKind == ConfigurationNodeKind.Scalar)
            {
                return document.RootElement.ValueKind == JsonValueKind.String
                    ? document.RootElement.GetString()
                    : document.RootElement.GetRawText();
            }

            return JsonSerializer.Serialize(document.RootElement, ConfigurationPersistedJsonOptions.ReadableValue);
        }
        catch (JsonException)
        {
            return storedValue.Json;
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
    /// Gets schema publish history entries for one configuration definition.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="limit">Maximum number of newest entries to return.</param>
    /// <returns>Newest schema publish history entries first.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationDefinitionPublishHistory>>> GetDefinitionPublishHistoriesAsync(
        string definitionKey,
        int limit = 20)
    {
        try
        {
            return Res.Ok(await metadataStore.ListDefinitionPublishHistoriesAsync(
                definitionKey,
                limit,
                CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration definition publish histories: {ex.GetMessageRecursively()}");
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
    /// Gets all runtime Microsoft configuration sources.
    /// </summary>
    /// <returns>Configuration source descriptors ordered by runtime priority index.</returns>
    public Task<Res<IReadOnlyList<ConfigurationSourceDescriptor>>> GetConfigurationSourcesAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok(sourceInspector.GetSources()));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<IReadOnlyList<ConfigurationSourceDescriptor>>>(
                Res.Fail($"Failed to get configuration sources: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets managed configuration values supplied by each runtime Microsoft configuration source.
    /// </summary>
    /// <returns>Source inventories ordered from highest priority to lowest priority.</returns>
    public Task<Res<IReadOnlyList<ConfigurationSourceInventory>>> GetConfigurationSourceInventoriesAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok(sourceInspector.GetSourceInventories()));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<IReadOnlyList<ConfigurationSourceInventory>>>(
                Res.Fail($"Failed to get configuration source inventory: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets source contribution counts for one definition.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <returns>Source contribution rows.</returns>
    public Task<Res<IReadOnlyList<ConfigurationDefinitionSourceContribution>>> GetDefinitionSourceContributionsAsync(string definitionKey)
    {
        try
        {
            if (!definitionResolver.TryGetLocal(definitionKey, out var definition))
            {
                return Task.FromResult<Res<IReadOnlyList<ConfigurationDefinitionSourceContribution>>>(
                    Res.Fail("Configuration source-chain inspection is only available for definitions scanned by the current process."));
            }

            return Task.FromResult(Res.Ok(sourceInspector.GetDefinitionContributions(definition)));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<IReadOnlyList<ConfigurationDefinitionSourceContribution>>>(
                Res.Fail($"Failed to get configuration source contributions: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets the source chain for one configuration path.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <returns>The source chain.</returns>
    public Task<Res<ConfigurationSourceChain>> GetSourceChainAsync(string definitionKey, LogicalPath logicalPath)
    {
        try
        {
            if (!definitionResolver.TryGetLocal(definitionKey, out var definition))
            {
                return Task.FromResult<Res<ConfigurationSourceChain>>(
                    Res.Fail("Configuration source-chain inspection is only available for definitions scanned by the current process."));
            }

            return Task.FromResult(Res.Ok(sourceInspector.GetSourceChain(definition, logicalPath)));
        }
        catch (Exception ex)
        {
            return Task.FromResult<Res<ConfigurationSourceChain>>(
                Res.Fail($"Failed to get configuration source chain: {ex.GetMessageRecursively()}"));
        }
    }

    /// <summary>
    /// Gets a display-safe JSON file view for one source.
    /// </summary>
    /// <param name="sourceKey">The source key.</param>
    /// <returns>The source file view.</returns>
    public async Task<Res<ConfigurationSourceFileView>> GetSourceFileViewAsync(string sourceKey)
    {
        try
        {
            return Res.Ok(await sourceInspector.GetSourceFileViewAsync(sourceKey, CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration source file: {ex.GetMessageRecursively()}");
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
            var debugView = runtimeContext.Root is { } root
                ? root.GetDebugView()
                : runtimeContext.Configuration.AsEnumerable().OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
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
    /// Mutates one external runtime configuration source.
    /// </summary>
    /// <param name="request">The source mutation request.</param>
    /// <returns>The mutation result.</returns>
    public async Task<Res<ConfigurationMutationResult>> MutateSourceAsync(ConfigurationSourceMutationRequest request)
    {
        try
        {
            return await sourceMutationService.MutateAsync(request, CancellationToken.None);
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to mutate configuration source value: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets the current source revision hash for optimistic source mutation.
    /// </summary>
    /// <param name="sourceKey">The source key.</param>
    /// <returns>The current source revision, or null when no revision is available.</returns>
    public async Task<Res<string?>> GetSourceRevisionAsync(string sourceKey)
    {
        try
        {
            var source = sourceInspector.GetRequiredSource(sourceKey);
            return Res.Ok<string?>(await sourceWriter.GetRevisionAsync(source, CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get configuration source revision: {ex.GetMessageRecursively()}");
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
    /// Lists unified configuration versions.
    /// </summary>
    /// <param name="from">Earliest creation time to include.</param>
    /// <param name="to">Latest creation time to include.</param>
    /// <param name="definitionKey">Definition key filter.</param>
    /// <param name="limit">Maximum number of versions to return.</param>
    /// <returns>The matching unified version summaries.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationUnifiedVersionSummary>>> GetUnifiedVersionsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? definitionKey = null,
        int limit = 100)
    {
        try
        {
            return Res.Ok(await unifiedVersionService.ListVersionsAsync(
                from,
                to,
                definitionKey,
                limit,
                CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get unified configuration versions: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets one unified configuration version snapshot.
    /// </summary>
    /// <param name="version">The version number.</param>
    /// <returns>The version snapshot.</returns>
    public async Task<Res<ConfigurationUnifiedVersionSnapshot>> GetUnifiedVersionAsync(long version)
    {
        try
        {
            var snapshot = await unifiedVersionService.GetVersionAsync(version, CancellationToken.None);
            return snapshot is null
                ? Res.Fail($"Unified configuration version '{version}' was not found.")
                : Res.Ok(snapshot);
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to get unified configuration version: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Compares two unified configuration versions.
    /// </summary>
    /// <param name="originVersion">The origin version number.</param>
    /// <param name="targetVersion">The target version number.</param>
    /// <returns>The version comparison.</returns>
    public async Task<Res<ConfigurationUnifiedVersionComparison>> CompareUnifiedVersionsAsync(
        long originVersion,
        long targetVersion)
    {
        try
        {
            return Res.Ok(await unifiedVersionService.CompareVersionsAsync(
                originVersion,
                targetVersion,
                CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to compare unified configuration versions: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Previews applying one unified configuration version to current sources.
    /// </summary>
    /// <param name="version">The version number.</param>
    /// <returns>The apply preview.</returns>
    public async Task<Res<ConfigurationUnifiedVersionApplyPreview>> PreviewUnifiedVersionRollbackAsync(long version)
    {
        try
        {
            return Res.Ok(await unifiedVersionService.PreviewRollbackAsync(version, CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to preview unified configuration version rollback: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Applies the captured definition values from one unified configuration version.
    /// </summary>
    /// <param name="version">The version number.</param>
    /// <param name="reason">Optional rollback reason.</param>
    /// <returns>The rollback result.</returns>
    public async Task<Res<ConfigurationUnifiedVersionRollbackResult>> RollbackUnifiedVersionAsync(
        long version,
        string? reason = null)
    {
        try
        {
            return Res.Ok(await unifiedVersionService.RollbackToVersionAsync(
                version,
                reason,
                CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to roll back unified configuration version: {ex.GetMessageRecursively()}");
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
    /// Rolls selected history rows back in reverse history order.
    /// </summary>
    /// <param name="historyIds">The history record identities.</param>
    /// <param name="reason">Optional rollback reason.</param>
    /// <returns>The rollback mutation results.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationMutationResult>>> RollbackHistoriesAsync(
        IReadOnlyList<string> historyIds,
        string? reason = null)
    {
        try
        {
            return Res.Ok(await rollbackService.RollbackHistoriesAsync(
                historyIds,
                new ConfigurationMutationContext { Reason = reason },
                CancellationToken.None));
        }
        catch (Exception ex)
        {
            return Res.Fail($"Failed to roll back selected configuration histories: {ex.GetMessageRecursively()}");
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

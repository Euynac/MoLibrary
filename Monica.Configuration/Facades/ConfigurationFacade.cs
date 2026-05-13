using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Core.Results;

namespace Monica.Configuration.Facades;

/// <summary>
/// Host-facing entry point for configuration management APIs and UI consumers.
/// </summary>
public sealed class ConfigurationFacade(
    IConfigurationDefinitionRegistry definitionRegistry,
    IConfigurationMutationService mutationService,
    IConfigurationHistoryService historyService,
    IConfigurationSourceChainService sourceChainService)
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
        var detail = new ConfigurationDefinitionDetail
        {
            Definition = definitionRegistry.GetRequired(definitionKey)
        };
        return Task.FromResult<Res<ConfigurationDefinitionDetail>>(detail);
    }

    /// <summary>
    /// Gets the source chain for one configuration value.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <returns>The source chain.</returns>
    public async Task<Res<ConfigurationSourceChain>> GetSourceChainAsync(string definitionKey, LogicalPath logicalPath)
    {
        return await sourceChainService.GetSourceChainAsync(definitionKey, logicalPath, CancellationToken.None);
    }

    /// <summary>
    /// Mutates a configuration value.
    /// </summary>
    /// <param name="request">The mutation request.</param>
    /// <returns>The mutation result.</returns>
    public async Task<Res<ConfigurationMutationResult>> MutateAsync(ConfigurationMutationRequest request)
    {
        return await mutationService.MutateAsync(request, CancellationToken.None);
    }

    /// <summary>
    /// Gets mutation history for one value.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="logicalPath">The logical path.</param>
    /// <returns>History records.</returns>
    public async Task<Res<IReadOnlyList<ConfigurationValueHistory>>> GetHistoryAsync(string definitionKey, LogicalPath logicalPath)
    {
        var history = await historyService.GetHistoryAsync(definitionKey, logicalPath, CancellationToken.None);
        return Res.Ok(history);
    }
}

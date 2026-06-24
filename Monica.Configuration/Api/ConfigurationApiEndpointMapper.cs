using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Monica.Core.Results;

namespace Monica.Configuration.Api;

internal static class ConfigurationApiEndpointMapper
{
    public static void Map(RouteGroupBuilder group, string tagName)
    {
        group.WithTags(tagName)
            .WithSummary("Monica.Configuration external management API")
            .WithDescription("External API surface for configuration definitions, values, mutation groups, history, rollback, import, and export.");

        group.MapGet("/parameters",
                async ([FromQuery] string? definitionKey,
                    [FromQuery] string? search,
                    [FromQuery] bool includeContainers,
                    [FromQuery] bool? includeEffectiveValue,
                    [FromQuery] bool? includeSource,
                    [FromServices] ConfigurationApiFacade facade) =>
                {
                    var query = new ConfigurationParameterQuery
                    {
                        DefinitionKey = definitionKey,
                        Search = search,
                        IncludeContainers = includeContainers,
                        IncludeEffectiveValue = includeEffectiveValue ?? true,
                        IncludeSource = includeSource ?? true
                    };
                    return (await facade.GetParametersAsync(query)).GetResponse();
                })
            .WithName("Configuration_GetParameters")
            .WithSummary("Gets flattened configuration parameter rows");

        group.MapGet("/definitions",
                async ([FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetDefinitionsAsync()).GetResponse())
            .WithName("Configuration_GetDefinitions")
            .WithSummary("Gets configuration definition summaries");

        group.MapGet("/definitions/{definitionKey}",
                async ([FromRoute] string definitionKey,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetDefinitionAsync(definitionKey)).GetResponse())
            .WithName("Configuration_GetDefinition")
            .WithSummary("Gets one configuration definition");

        group.MapGet("/definitions/{definitionKey}/publish-history",
                async ([FromRoute] string definitionKey,
                    [FromQuery] int? limit,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetDefinitionPublishHistoriesAsync(definitionKey, limit ?? 20)).GetResponse())
            .WithName("Configuration_GetDefinitionPublishHistory")
            .WithSummary("Gets schema publish history for a definition");

        group.MapGet("/effective-value",
                async ([FromQuery] string definitionKey,
                    [FromQuery] string? logicalPath,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetEffectiveValueAsync(definitionKey, logicalPath)).GetResponse())
            .WithName("Configuration_GetEffectiveValue")
            .WithSummary("Gets the current effective value for a logical path");

        group.MapGet("/source-chain",
                async ([FromQuery] string definitionKey,
                    [FromQuery] string logicalPath,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetSourceChainAsync(definitionKey, logicalPath)).GetResponse())
            .WithName("Configuration_GetSourceChain")
            .WithSummary("Gets runtime source chain for a logical path");

        group.MapGet("/sources",
                async ([FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetConfigurationSourcesAsync()).GetResponse())
            .WithName("Configuration_GetSources")
            .WithSummary("Gets runtime configuration sources");

        group.MapGet("/sources/inventories",
                async ([FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetConfigurationSourceInventoriesAsync()).GetResponse())
            .WithName("Configuration_GetSourceInventories")
            .WithSummary("Gets runtime configuration source inventories");

        group.MapGet("/sources/{sourceKey}/revision",
                async ([FromRoute] string sourceKey,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetSourceRevisionAsync(sourceKey)).GetResponse())
            .WithName("Configuration_GetSourceRevision")
            .WithSummary("Gets a writable source revision");

        group.MapGet("/sources/{sourceKey}/file",
                async ([FromRoute] string sourceKey,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetSourceFileViewAsync(sourceKey)).GetResponse())
            .WithName("Configuration_GetSourceFile")
            .WithSummary("Gets a display-safe source file view");

        group.MapGet("/json-editor-document",
                async ([FromQuery] string definitionKey,
                    [FromQuery] string? scopePath,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.BuildJsonEditorDocumentAsync(definitionKey, scopePath)).GetResponse())
            .WithName("Configuration_GetJsonEditorDocument")
            .WithSummary("Builds a JSON editor document");

        group.MapPost("/drafts/json/analyze",
                async ([FromBody] ConfigurationJsonDraftAnalyzeRequest request,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.AnalyzeJsonDraftAsync(request)).GetResponse())
            .WithName("Configuration_AnalyzeJsonDraft")
            .WithSummary("Analyzes edited JSON into draft changes");

        group.MapPost("/mutation-groups",
                async ([FromBody] ConfigurationMutationGroupPublishRequest request,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.PublishMutationGroupAsync(request)).GetResponse())
            .WithName("Configuration_PublishMutationGroup")
            .WithSummary("Publishes a mutation group");

        group.MapGet("/mutation-groups",
                async ([FromQuery] DateTimeOffset? from,
                    [FromQuery] DateTimeOffset? to,
                    [FromQuery] string? definitionKey,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetMutationGroupsAsync(from, to, definitionKey)).GetResponse())
            .WithName("Configuration_GetMutationGroups")
            .WithSummary("Lists mutation groups");

        group.MapGet("/mutation-groups/{groupId}",
                async ([FromRoute] string groupId,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetMutationGroupAsync(groupId)).GetResponse())
            .WithName("Configuration_GetMutationGroup")
            .WithSummary("Gets one mutation group");

        group.MapGet("/mutation-groups/{groupId}/history",
                async ([FromRoute] string groupId,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetGroupHistoryAsync(groupId)).GetResponse())
            .WithName("Configuration_GetMutationGroupHistory")
            .WithSummary("Gets history rows for a mutation group");

        group.MapGet("/history",
                async ([FromQuery] DateTimeOffset? from,
                    [FromQuery] DateTimeOffset? to,
                    [FromQuery] string? definitionKey,
                    [FromQuery] string? logicalPath,
                    [FromQuery] string? mutationGroupId,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.QueryHistoryAsync(from, to, definitionKey, logicalPath, mutationGroupId)).GetResponse())
            .WithName("Configuration_QueryHistory")
            .WithSummary("Queries configuration mutation history");

        group.MapGet("/history/{historyId}",
                async ([FromRoute] string historyId,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.GetHistoryByIdAsync(historyId)).GetResponse())
            .WithName("Configuration_GetHistory")
            .WithSummary("Gets one history row");

        group.MapPost("/rollback/history/{historyId}",
                async ([FromRoute] string historyId,
                    [FromBody] ConfigurationRollbackRequest request,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.RollbackHistoryAsync(historyId, request)).GetResponse())
            .WithName("Configuration_RollbackHistory")
            .WithSummary("Rolls back one history row");

        group.MapPost("/rollback/histories",
                async ([FromBody] ConfigurationRollbackHistoriesRequest request,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.RollbackHistoriesAsync(request)).GetResponse())
            .WithName("Configuration_RollbackHistories")
            .WithSummary("Rolls back selected history rows");

        group.MapPost("/rollback/mutation-groups/{groupId}",
                async ([FromRoute] string groupId,
                    [FromBody] ConfigurationRollbackRequest request,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.RollbackGroupAsync(groupId, request)).GetResponse())
            .WithName("Configuration_RollbackMutationGroup")
            .WithSummary("Rolls back one mutation group");

        group.MapGet("/export",
                async ([FromQuery] string? definitionKey,
                    [FromQuery] bool includeSensitive,
                    [FromQuery] string? exportedBy,
                    [FromQuery] string? systemVersion,
                    [FromQuery] string? environmentName,
                    [FromServices] ConfigurationApiFacade facade) =>
                {
                    var request = new ConfigurationApiExportRequest
                    {
                        DefinitionKey = definitionKey,
                        IncludeSensitive = includeSensitive,
                        ExportedBy = exportedBy,
                        SystemVersion = systemVersion,
                        EnvironmentName = environmentName
                    };
                    return (await facade.CreateExportAsync(request)).GetResponse();
                })
            .WithName("Configuration_Export")
            .WithSummary("Exports a configuration package");

        group.MapPost("/import/analyze",
                async ([FromBody] ConfigurationImportAnalyzeRequest request,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.AnalyzeImportAsync(request)).GetResponse())
            .WithName("Configuration_AnalyzeImport")
            .WithSummary("Analyzes a configuration import package");

        group.MapPost("/import/publish",
                async ([FromBody] ConfigurationImportPublishRequest request,
                    [FromServices] ConfigurationApiFacade facade) =>
                    (await facade.PublishImportAsync(request)).GetResponse())
            .WithName("Configuration_PublishImport")
            .WithSummary("Analyzes and publishes a configuration import package");
    }
}

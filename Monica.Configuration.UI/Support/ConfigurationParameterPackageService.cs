using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using Monica.Configuration.Facades;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;
using Monica.Configuration.UI.Models;
using Monica.Configuration.UI.State;
using Monica.Core.Results;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Creates and analyzes Monica-managed configuration parameter packages.
/// </summary>
internal sealed class ConfigurationParameterPackageService(
    ConfigurationFacade facade,
    ConfigurationJsonDraftService draftService,
    IStringLocalizer<ConfigurationUIResource> localizer)
{
    /// <summary>
    /// Creates a versioned export document from current runtime effective values.
    /// </summary>
    /// <param name="request">The export request.</param>
    /// <returns>The export document.</returns>
    public async Task<ConfigurationExportDocument> CreateExportAsync(ConfigurationExportRequest request)
    {
        var definitions = await LoadDefinitionsAsync();
        if (!string.IsNullOrWhiteSpace(request.DefinitionKey))
        {
            definitions = definitions
                .Where(definition => string.Equals(definition.DefinitionKey, request.DefinitionKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var exportedDefinitions = new List<ConfigurationExportDefinition>();
        foreach (var definition in definitions)
        {
            exportedDefinitions.Add(await CreateExportDefinitionAsync(definition, request.IncludeSensitive));
        }

        return new ConfigurationExportDocument
        {
            ExportedAt = DateTimeOffset.UtcNow,
            ExportedBy = request.ExportedBy,
            SystemVersion = request.SystemVersion,
            EnvironmentName = request.EnvironmentName,
            IncludeSensitive = request.IncludeSensitive,
            Definitions = exportedDefinitions
        };
    }

    /// <summary>
    /// Analyzes an uploaded export document and converts it into UI staged state.
    /// </summary>
    /// <param name="document">The import document.</param>
    /// <param name="pendingChanges">Existing pending changes that should remain outside analyzed scopes.</param>
    /// <param name="fileName">The uploaded file name.</param>
    /// <returns>The import report.</returns>
    public async Task<ConfigurationImportReport> AnalyzeImportAsync(
        ConfigurationExportDocument document,
        IReadOnlyList<PendingChange> pendingChanges,
        string? fileName)
    {
        var diagnostics = new List<ConfigurationImportDiagnostic>();
        if (document.FormatVersion != 1)
        {
            diagnostics.Add(new ConfigurationImportDiagnostic
            {
                Severity = ConfigurationImportDiagnosticSeverity.Warning,
                Message = localizer["ImportExport:Diagnostics:FormatVersionMismatch", document.FormatVersion]
            });
        }

        var definitions = await LoadDefinitionsAsync();
        var definitionsByKey = definitions.ToDictionary(definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase);
        var drafts = new List<ConfigurationJsonDraftResult>();

        foreach (var exportedDefinition in document.Definitions)
        {
            if (!definitionsByKey.TryGetValue(exportedDefinition.DefinitionKey, out var definition))
            {
                diagnostics.Add(new ConfigurationImportDiagnostic
                {
                    Severity = ConfigurationImportDiagnosticSeverity.Warning,
                    DefinitionKey = exportedDefinition.DefinitionKey,
                    Message = localizer["ImportExport:Diagnostics:UnknownDefinition", exportedDefinition.DefinitionKey]
                });
                continue;
            }

            var definitionDiagnostics = new List<ConfigurationImportDiagnostic>();
            if (exportedDefinition.SchemaVersion != definition.SchemaVersion)
            {
                definitionDiagnostics.Add(new ConfigurationImportDiagnostic
                {
                    Severity = ConfigurationImportDiagnosticSeverity.Warning,
                    DefinitionKey = definition.DefinitionKey,
                    Message = localizer["ImportExport:Diagnostics:SchemaVersionMismatch", exportedDefinition.SchemaVersion, definition.SchemaVersion]
                });
            }

            if (!string.IsNullOrWhiteSpace(exportedDefinition.SchemaHash)
                && !string.Equals(exportedDefinition.SchemaHash, definition.SchemaHash, StringComparison.Ordinal))
            {
                definitionDiagnostics.Add(new ConfigurationImportDiagnostic
                {
                    Severity = ConfigurationImportDiagnosticSeverity.Warning,
                    DefinitionKey = definition.DefinitionKey,
                    Message = localizer["ImportExport:Diagnostics:SchemaHashMismatch"]
                });
            }

            var effectiveValue = await LoadEffectiveValueAsync(definition.DefinitionKey, LogicalPath.Root);
            var scalarValues = await LoadScalarEffectiveValuesAsync(definition);
            var json = exportedDefinition.Value?.ToJsonString(ConfigurationJsonDisplayFormatter.ReadableJsonOptions) ?? "null";
            var draft = draftService.Analyze(new ConfigurationJsonDraftRequest
            {
                Definition = definition,
                ScopeNode = definition.Root,
                EffectiveValue = effectiveValue,
                ScalarEffectiveValues = scalarValues,
                Json = json,
                RedactedPaths = exportedDefinition.RedactedPaths.ToHashSet(StringComparer.Ordinal)
            });

            drafts.Add(draft with
            {
                Diagnostics = definitionDiagnostics.Concat(draft.Diagnostics).ToArray()
            });
        }

        return new ConfigurationImportReport
        {
            FileName = fileName,
            Document = document,
            Drafts = drafts,
            Diagnostics = diagnostics
        };
    }

    /// <summary>
    /// Loads current effective scalar values for a definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <returns>Effective values keyed by logical path.</returns>
    public async Task<IReadOnlyDictionary<LogicalPath, ConfigurationEffectiveValue>> LoadScalarEffectiveValuesAsync(
        ConfigurationDefinition definition)
    {
        var values = new Dictionary<LogicalPath, ConfigurationEffectiveValue>();
        foreach (var node in EnumerateNodes(definition.Root).Where(static node => node.NodeKind == ConfigurationNodeKind.Scalar))
        {
            values[node.RelativePath] = await LoadEffectiveValueAsync(definition.DefinitionKey, node.RelativePath);
        }

        return values;
    }

    /// <summary>
    /// Loads all managed configuration definitions.
    /// </summary>
    /// <returns>Definitions ordered by display name.</returns>
    public async Task<IReadOnlyList<ConfigurationDefinition>> LoadDefinitionsAsync()
    {
        var summariesResult = await facade.GetDefinitionsAsync();
        if (summariesResult.IsFailed(out var summaryError, out var summaries))
        {
            throw new InvalidOperationException(summaryError.Message);
        }

        var definitions = new List<ConfigurationDefinition>();
        foreach (var summary in summaries.OrderBy(summary => summary.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            definitions.Add(await LoadDefinitionAsync(summary.DefinitionKey));
        }

        return definitions;
    }

    private async Task<ConfigurationExportDefinition> CreateExportDefinitionAsync(
        ConfigurationDefinition definition,
        bool includeSensitive)
    {
        var effectiveValue = await LoadEffectiveValueAsync(definition.DefinitionKey, LogicalPath.Root);
        var scalarValues = await LoadScalarEffectiveValuesAsync(definition);
        var root = ParseJson(effectiveValue.DisplayValue) ?? ConfigurationPendingValueDocumentBuilder.CreateDefaultJsonFor(definition.Root);
        IReadOnlyList<string> redactedPaths = [];
        if (!includeSensitive)
        {
            root = draftService.RedactForExport(root, definition.Root, out redactedPaths);
        }

        return new ConfigurationExportDefinition
        {
            DefinitionKey = definition.DefinitionKey,
            DisplayName = definition.DisplayName,
            OwnerModule = definition.OwnerModule,
            Category = definition.Category,
            ClrTypeName = definition.ClrTypeName,
            SchemaVersion = definition.SchemaVersion,
            SchemaHash = definition.SchemaHash,
            ValueVersion = effectiveValue.Version,
            SourceSummary = scalarValues.Values
                .Select(value => value.EffectiveSource?.DisplayName)
                .OfType<string>()
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Value = root,
            RedactedPaths = redactedPaths
        };
    }

    private async Task<ConfigurationDefinition> LoadDefinitionAsync(string definitionKey)
    {
        var result = await facade.GetDefinitionAsync(definitionKey);
        if (result.IsFailed(out var error, out var detail))
        {
            throw new InvalidOperationException(error.Message);
        }

        return detail.Definition;
    }

    /// <summary>
    /// Loads one effective value.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="path">The logical path.</param>
    /// <returns>The effective value.</returns>
    public async Task<ConfigurationEffectiveValue> LoadEffectiveValueAsync(string definitionKey, LogicalPath path)
    {
        var result = await facade.GetEffectiveValueAsync(definitionKey, path);
        if (result.IsFailed(out var error, out var effectiveValue))
        {
            throw new InvalidOperationException(error.Message);
        }

        return effectiveValue;
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

    private static JsonNode? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(json);
        }
    }

}

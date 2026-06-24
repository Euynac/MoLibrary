using System.Text.Json.Nodes;
using Monica.Configuration.Models;

namespace Monica.Configuration.Api;

internal sealed partial class ConfigurationApiService
{
    public async Task<ConfigurationExportDocument> CreateExportAsync(ConfigurationApiExportRequest request)
    {
        var definitions = await LoadDefinitionsAsync(request.DefinitionKey);
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

    public async Task<ConfigurationImportAnalyzeResult> AnalyzeImportAsync(ConfigurationImportAnalyzeRequest request)
    {
        var diagnostics = new List<ConfigurationApiDiagnostic>();
        if (request.Document.FormatVersion != 1)
        {
            diagnostics.Add(new ConfigurationApiDiagnostic
            {
                Severity = ConfigurationApiDiagnosticSeverity.Warning,
                Message = $"Import package format version {request.Document.FormatVersion} differs from supported version 1."
            });
        }

        var definitions = await LoadDefinitionsAsync(null);
        var definitionsByKey = definitions.ToDictionary(static definition => definition.DefinitionKey, StringComparer.OrdinalIgnoreCase);
        var drafts = new List<ConfigurationJsonDraftAnalyzeResult>();

        foreach (var exportedDefinition in request.Document.Definitions)
        {
            if (!definitionsByKey.TryGetValue(exportedDefinition.DefinitionKey, out var definition))
            {
                diagnostics.Add(new ConfigurationApiDiagnostic
                {
                    Severity = ConfigurationApiDiagnosticSeverity.Warning,
                    DefinitionKey = exportedDefinition.DefinitionKey,
                    Message = $"Configuration definition '{exportedDefinition.DefinitionKey}' was not found in this process."
                });
                continue;
            }

            var draft = await AnalyzeJsonDraftAsync(new ConfigurationJsonDraftAnalyzeRequest
            {
                DefinitionKey = exportedDefinition.DefinitionKey,
                ScopePath = string.Empty,
                Json = exportedDefinition.Value?.ToJsonString(COMPACT_JSON_OPTIONS) ?? "null",
                RedactedPaths = exportedDefinition.RedactedPaths,
                CompactChanges = request.CompactChanges
            });

            var definitionDiagnostics = new List<ConfigurationApiDiagnostic>();
            if (exportedDefinition.SchemaVersion != definition.SchemaVersion)
            {
                definitionDiagnostics.Add(new ConfigurationApiDiagnostic
                {
                    Severity = ConfigurationApiDiagnosticSeverity.Warning,
                    DefinitionKey = definition.DefinitionKey,
                    Message = $"Imported schema version {exportedDefinition.SchemaVersion} differs from current version {definition.SchemaVersion}."
                });
            }

            if (!string.IsNullOrWhiteSpace(exportedDefinition.SchemaHash)
                && !string.Equals(exportedDefinition.SchemaHash, definition.SchemaHash, StringComparison.Ordinal))
            {
                definitionDiagnostics.Add(new ConfigurationApiDiagnostic
                {
                    Severity = ConfigurationApiDiagnosticSeverity.Warning,
                    DefinitionKey = definition.DefinitionKey,
                    Message = "Imported schema hash differs from the current definition schema hash."
                });
            }

            drafts.Add(draft with
            {
                Diagnostics = definitionDiagnostics.Concat(draft.Diagnostics).ToArray()
            });
        }

        return new ConfigurationImportAnalyzeResult
        {
            FileName = request.FileName,
            Document = request.Document,
            Drafts = drafts,
            Diagnostics = diagnostics
        };
    }

    public async Task<ConfigurationImportPublishResult> PublishImportAsync(ConfigurationImportPublishRequest request)
    {
        var report = await AnalyzeImportAsync(new ConfigurationImportAnalyzeRequest
        {
            FileName = request.FileName,
            Document = request.Document,
            CompactChanges = request.CompactChanges
        });

        if (report.HasBlockingIssues)
        {
            return new ConfigurationImportPublishResult
            {
                Report = report
            };
        }

        if (!request.AllowWarnings
            && report.AllDiagnostics.Any(static diagnostic => diagnostic.Severity == ConfigurationApiDiagnosticSeverity.Warning))
        {
            return new ConfigurationImportPublishResult
            {
                Report = report
            };
        }

        var publishResult = await PublishMutationGroupAsync(new ConfigurationMutationGroupPublishRequest
        {
            Label = request.Label,
            Reason = request.Reason,
            Changes = report.Changes.Select(static change => new ConfigurationMutationGroupPublishChange
            {
                DefinitionKey = change.DefinitionKey,
                LogicalPath = change.LogicalPath,
                MutationKind = change.MutationKind,
                TargetKind = change.TargetKind,
                SourceKey = change.SourceKey,
                Value = change.Value?.DeepClone(),
                ExpectedSchemaVersion = change.ExpectedSchemaVersion,
                ExpectedValueVersion = change.ExpectedValueVersion,
                ExpectedSourceRevision = change.ExpectedSourceRevision
            }).ToArray()
        });

        return new ConfigurationImportPublishResult
        {
            Report = report,
            PublishResult = publishResult
        };
    }

    private async Task<ConfigurationExportDefinition> CreateExportDefinitionAsync(
        ConfigurationDefinition definition,
        bool includeSensitive)
    {
        var effectiveValue = await LoadEffectiveValueAsync(definition.DefinitionKey, LogicalPath.Root);
        var root = ParseEffectiveNode(definition.Root, effectiveValue.DisplayValue);
        IReadOnlyList<string> redactedPaths = [];
        if (!includeSensitive)
        {
            root = RedactForExport(root?.DeepClone(), definition.Root, LogicalPath.Root, out redactedPaths);
        }

        var sourceSummary = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in EnumerateSchemaNodes(definition.Root).Where(static node => node.NodeKind == ConfigurationNodeKind.Scalar))
        {
            var scalar = await LoadEffectiveValueAsync(definition.DefinitionKey, node.RelativePath);
            if (!string.IsNullOrWhiteSpace(scalar.EffectiveSource?.DisplayName))
            {
                sourceSummary.Add(scalar.EffectiveSource.DisplayName);
            }
        }

        return new ConfigurationExportDefinition
        {
            DefinitionKey = definition.DefinitionKey,
            DisplayName = definition.DisplayName,
            FromProject = definition.FromProject,
            Category = definition.Category,
            ClrTypeName = definition.ClrTypeName,
            SchemaVersion = definition.SchemaVersion,
            SchemaHash = definition.SchemaHash,
            ValueVersion = effectiveValue.Version,
            SourceSummary = sourceSummary.ToArray(),
            Value = root,
            RedactedPaths = redactedPaths
        };
    }
}

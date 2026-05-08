using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Monica.Core;
using Monica.Core.Modularity.Models;
using Monica.Modules;
using Monica.WebApi.Swagger.Models;

namespace Monica.WebApi.Swagger.Services.Support;

/// <summary>
/// Computes the Swagger document set and routes endpoint descriptions into the correct document.
/// </summary>
internal sealed class SwaggerDocumentCatalog
{
    private const string DefaultDocumentVersion = "v1";
    private const string DefaultMonicaDocumentTitle = "Monica API";
    private const string DefaultBusinessDocumentTitle = "Business API";

    private readonly ModuleSwaggerOption _option;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<string, byte> _unknownResolverDocumentNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly SwaggerDocumentDefinition _businessDocument;
    private readonly IReadOnlyList<SwaggerDocumentDefinition> _documents;
    private readonly IReadOnlyDictionary<string, SwaggerDocumentDefinition> _documentsByName;

    public SwaggerDocumentCatalog(
        ModuleSwaggerOption option,
        ILogger? logger = null)
    {
        _option = option;
        _logger = logger;
        (_businessDocument, _documents) = BuildDocuments();
        _documentsByName = _documents.ToDictionary(document => document.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SwaggerDocumentDefinition> Documents => _documents;

    public string DocumentVersion => NormalizeOrDefault(_option.ApiVersion, DefaultDocumentVersion);

    public OpenApiInfo GetOpenApiInfo(SwaggerDocumentDefinition document)
    {
        return new OpenApiInfo
        {
            Title = document.Title,
            Version = DocumentVersion,
            Description = document.Description ?? string.Empty
        };
    }

    public string ResolveDocumentName(ApiDescription description)
    {
        var overriddenDocumentName = _option.DocumentNameResolver?.Invoke(description);
        if (overriddenDocumentName is not null)
        {
            if (TryGetRegisteredDocumentName(overriddenDocumentName, out var resolvedDocumentName))
            {
                return resolvedDocumentName;
            }

            LogUnknownDocumentName(overriddenDocumentName);
        }

        if (TryGetRegisteredDocumentName(description.GroupName, out var groupedDocumentName))
        {
            return groupedDocumentName;
        }

        if (_option.Monica.Enabled
            && IsMonicaEndpoint(description)
            && TryGetRegisteredDocumentName(_option.Monica.Name, out var monicaDocumentName))
        {
            return monicaDocumentName;
        }

        return _businessDocument.Name;
    }

    public bool ShouldInclude(string docName, ApiDescription description)
    {
        return !_documentsByName.ContainsKey(docName)
            || string.Equals(ResolveDocumentName(description), docName, StringComparison.OrdinalIgnoreCase);
    }

    public string GetSwaggerEndpointDisplayName(SwaggerDocumentDefinition document)
    {
        return $"{document.Title} {DocumentVersion}";
    }

    private (SwaggerDocumentDefinition BusinessDocument, IReadOnlyList<SwaggerDocumentDefinition> Documents) BuildDocuments()
    {
        var businessDocument = new SwaggerDocumentDefinition(
            NormalizeOrDefault(_option.BusinessDocumentName, DocumentVersion),
            ResolveBusinessDocumentTitle(),
            NormalizeOptional(_option.Description));

        List<SwaggerDocumentDefinition> documents =
        [
            businessDocument
        ];

        if (_option.Monica.Enabled)
        {
            documents.Add(new SwaggerDocumentDefinition(
                NormalizeRequiredName(_option.Monica.Name, "Monica document"),
                NormalizeOrDefault(_option.Monica.Title, DefaultMonicaDocumentTitle),
                NormalizeOptional(_option.Monica.Description)));
        }

        foreach (var descriptor in _option.AdditionalDocuments)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            var name = NormalizeRequiredName(descriptor.Name, "Additional Swagger document");
            documents.Add(new SwaggerDocumentDefinition(
                name,
                NormalizeOrDefault(descriptor.Title, name),
                NormalizeOptional(descriptor.Description)));
        }

        ValidateDocumentNames(documents);
        return (businessDocument, documents);
    }

    private string ResolveBusinessDocumentTitle()
    {
        if (!string.IsNullOrWhiteSpace(_option.BusinessDocumentTitle))
        {
            return _option.BusinessDocumentTitle.Trim();
        }

        var appName = Mo.Application.ResolveAppName(_option.AppName);
        return string.IsNullOrWhiteSpace(appName)
            ? DefaultBusinessDocumentTitle
            : $"{appName} API";
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequiredName(
        string? value,
        string documentLabel)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{documentLabel} name must not be empty.");
        }

        return value.Trim();
    }

    private bool TryGetRegisteredDocumentName(
        string? requestedName,
        out string resolvedName)
    {
        resolvedName = string.Empty;
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return false;
        }

        if (!_documentsByName.TryGetValue(requestedName.Trim(), out var document))
        {
            return false;
        }

        resolvedName = document.Name;
        return true;
    }

    private void LogUnknownDocumentName(string documentName)
    {
        if (_logger is null)
        {
            return;
        }

        var normalizedDocumentName = string.IsNullOrWhiteSpace(documentName)
            ? "<empty>"
            : documentName.Trim();

        if (!_unknownResolverDocumentNames.TryAdd(normalizedDocumentName, 0))
        {
            return;
        }

        _logger.LogWarning(
            "Swagger DocumentNameResolver returned unknown document name '{DocumentName}'. Registered documents: {RegisteredDocuments}. Falling back to ApiDescription.GroupName and default routing.",
            normalizedDocumentName,
            string.Join(", ", _documents.Select(document => document.Name)));
    }

    private static bool IsMonicaEndpoint(ApiDescription description)
    {
        return description.ActionDescriptor.EndpointMetadata.Any(static metadata => metadata is MonicaMinimalApiMetadata);
    }

    private static void ValidateDocumentNames(IReadOnlyList<SwaggerDocumentDefinition> documents)
    {
        var duplicateNames = documents
            .GroupBy(document => document.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateNames.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Swagger document names must be unique. Duplicates: {string.Join(", ", duplicateNames)}");
    }
}

internal sealed record SwaggerDocumentDefinition(
    string Name,
    string Title,
    string? Description);

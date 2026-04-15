using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Monica.Modules;
using Monica.WebApi.Swagger.Models;

namespace Monica.WebApi.Swagger.Services.Support;

/// <summary>
/// Computes the Swagger document set and routes endpoint descriptions into the correct document.
/// </summary>
internal sealed class SwaggerDocumentCatalog
{
    private const string DefaultDocumentVersion = "v1";
    private const string DefaultMonicaDocumentName = "monica";
    private const string DefaultBusinessDocumentName = "business";
    private const string DefaultMonicaDocumentTitle = "Monica API";
    private const string DefaultBusinessDocumentTitle = "Business API";

    private readonly ModuleSwaggerOption _option;
    private readonly IReadOnlyList<SwaggerDocumentDefinition> _documents;
    private readonly IReadOnlyDictionary<string, SwaggerDocumentDefinition> _documentsByName;

    public SwaggerDocumentCatalog(ModuleSwaggerOption option)
    {
        _option = option;
        _documents = BuildDocuments();
        _documentsByName = _documents.ToDictionary(document => document.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SwaggerDocumentDefinition> Documents => _documents;

    public string DocumentVersion => NormalizeOrDefault(_option.Version, DefaultDocumentVersion);

    public bool ShouldInclude(string docName, ApiDescription description)
    {
        if (!_documentsByName.TryGetValue(docName, out var document))
        {
            return true;
        }

        return SwaggerDocumentKindResolver.Resolve(description, _option.DocumentKindResolver) == document.Kind;
    }

    public string GetOpenApiDocumentTitle(SwaggerDocumentDefinition document)
    {
        return string.IsNullOrWhiteSpace(_option.AppName)
            ? document.Title
            : $"{_option.AppName} {document.Title}";
    }

    public string GetSwaggerEndpointDisplayName(SwaggerDocumentDefinition document)
    {
        return $"{GetOpenApiDocumentTitle(document)} {DocumentVersion}";
    }

    private IReadOnlyList<SwaggerDocumentDefinition> BuildDocuments()
    {
        SwaggerDocumentDefinition[] documents =
        [
            new(
                ESwaggerDocumentKind.Business,
                NormalizeOrDefault(_option.BusinessDocumentName, DefaultBusinessDocumentName),
                NormalizeOrDefault(_option.BusinessDocumentTitle, DefaultBusinessDocumentTitle)),
            new(
                ESwaggerDocumentKind.Monica,
                NormalizeOrDefault(_option.MonicaDocumentName, DefaultMonicaDocumentName),
                NormalizeOrDefault(_option.MonicaDocumentTitle, DefaultMonicaDocumentTitle))
        ];

        ValidateDocumentNames(documents);
        return documents;
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
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
    ESwaggerDocumentKind Kind,
    string Name,
    string Title);

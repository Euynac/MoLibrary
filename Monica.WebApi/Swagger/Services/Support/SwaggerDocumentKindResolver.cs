using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Monica.Core.Modularity.Models;
using Monica.WebApi.Swagger.Models;

namespace Monica.WebApi.Swagger.Services.Support;

/// <summary>
/// Resolves which Swagger document should own a given API description.
/// </summary>
internal static class SwaggerDocumentKindResolver
{
    public static ESwaggerDocumentKind Resolve(
        ApiDescription description,
        Func<ApiDescription, ESwaggerDocumentKind?>? overrideResolver)
    {
        var overriddenKind = overrideResolver?.Invoke(description);
        if (overriddenKind.HasValue)
        {
            return overriddenKind.Value;
        }

        return description.ActionDescriptor.EndpointMetadata.Any(static metadata => metadata is MonicaMinimalApiMetadata)
            ? ESwaggerDocumentKind.Monica
            : ESwaggerDocumentKind.Business;
    }
}

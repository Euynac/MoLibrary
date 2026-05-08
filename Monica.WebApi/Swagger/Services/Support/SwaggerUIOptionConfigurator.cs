using Microsoft.AspNetCore.Builder;
using Monica.Modules;
using Swashbuckle.AspNetCore.SwaggerUI;
using Monica.Core;

namespace Monica.WebApi.Swagger.Services.Support;

/// <summary>
/// Applies Monica's shared Swagger UI configuration.
/// </summary>
internal static class SwaggerUIOptionConfigurator
{
    public static void Configure(
        SwaggerUIOptions options,
        ModuleSwaggerOption option,
        SwaggerDocumentCatalog documentCatalog)
    {
        foreach (var document in documentCatalog.Documents)
        {
            options.SwaggerEndpoint(
                $"/swagger/{document.Name}/swagger.json",
                documentCatalog.GetSwaggerEndpointDisplayName(document));
        }

        options.DocumentTitle = Mo.Application.ResolveAppName(option.AppName, "Swagger UI");
        options.RoutePrefix = option.RoutePrefix;

        option.ExtendSwaggerUIAction?.Invoke(options);
    }
}

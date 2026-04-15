using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Modules;

namespace Monica.WebApi.Swagger.Services.Support;

/// <summary>
/// Resolves XML documentation files that should be imported into Swagger.
/// </summary>
internal static class SwaggerXmlDocumentationFileResolver
{
    public static IReadOnlyList<string> Resolve(ModuleSwaggerOption option, ILogger logger)
    {
        // Pitfall: to generate Swagger docs you must enable `<GenerateDocumentationFile>True</GenerateDocumentationFile>` in each project and supply the resulting XML documents.
        var documentAssemblies = (option.DocumentAssemblies ?? []).ToList();
        if (!option.DisableAutoIncludeModuleSystemRelatedAsDocumentAssembly)
        {
            documentAssemblies.AddRange(Mo.Options.GlobalTypeFinder.GetAssemblies().Select(p => p.GetName().Name!));
        }

        var xmlFilePaths = new List<string>();
        foreach (var name in documentAssemblies.Distinct())
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, $"{name}.xml");
            if (File.Exists(filePath))
            {
                xmlFilePaths.Add(filePath);
                continue;
            }

            if (ShouldSuppressMissingXmlWarning(name))
            {
                continue;
            }

            logger.LogWarning(
                "Swagger XML file not found: {FilePath}. Enable <GenerateDocumentationFile>True</GenerateDocumentationFile> in the project file to generate Swagger documentation.",
                filePath);
        }

        return xmlFilePaths;
    }

    private static bool ShouldSuppressMissingXmlWarning(string assemblyName)
    {
        return assemblyName.StartsWith("Monica.", StringComparison.Ordinal);
    }
}

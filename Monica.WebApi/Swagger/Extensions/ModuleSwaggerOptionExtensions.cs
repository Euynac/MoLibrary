using Monica.WebApi.Swagger.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Provides convenience helpers for configuring additional Swagger documents.
/// </summary>
public static class ModuleSwaggerOptionExtensions
{
    /// <summary>
    /// Adds an extra Swagger document that can be targeted through <c>WithGroupName</c> or <c>[ApiExplorerSettings(GroupName = ...)]</c>.
    /// </summary>
    /// <param name="option">Swagger module options being configured.</param>
    /// <param name="name">Route segment used by the additional Swagger document.</param>
    /// <param name="configure">Optional callback that can customize the document title and description.</param>
    /// <returns>The same <paramref name="option"/> instance so configuration can continue fluently.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="option"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    public static ModuleSwaggerOption AddDocument(
        this ModuleSwaggerOption option,
        string name,
        Action<SwaggerDocumentDescriptor>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var document = new SwaggerDocumentDescriptor
        {
            Name = name.Trim()
        };

        configure?.Invoke(document);
        option.AdditionalDocuments.Add(document);
        return option;
    }
}

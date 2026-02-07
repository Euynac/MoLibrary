using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Markdown.Services;

namespace Monica.Markdown.Modules;

public static class ModuleMarkdownBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Markdown document management module.
        /// </summary>
        public static ModuleMarkdownGuide AddMarkdown(
            Action<ModuleMarkdownOption>? action = null)
        {
            return new ModuleMarkdownGuide().Register(action);
        }
    }
}

public class ModuleMarkdown(ModuleMarkdownOption option)
    : MoModule<ModuleMarkdown, ModuleMarkdownOption, ModuleMarkdownGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Markdown;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<IDocumentTitleProvider,
            FileNameDocumentTitleProvider>();
        services.AddSingleton<IMoMarkdownService, MoMarkdownService>();
    }
}

public class ModuleMarkdownGuide
    : MoModuleGuide<ModuleMarkdown, ModuleMarkdownOption, ModuleMarkdownGuide>
{
    /// <summary>
    /// Registers a markdown document group for scanning.
    /// </summary>
    /// <param name="key">Unique identifier for this group.</param>
    /// <param name="title">Display title for this group.</param>
    /// <param name="basePath">Folder path containing markdown files.</param>
    /// <param name="description">Optional description.</param>
    public ModuleMarkdownGuide AddDocumentGroup(
        string key, string title, string basePath, string? description = null)
    {
        ConfigureModuleOption(option =>
        {
            option.DocumentGroupRegistrations.Add(new DocumentGroupRegistration
            {
                Key = key,
                Title = title,
                BasePath = basePath,
                Description = description
            });
        }, secondKey: key);

        return this;
    }

    /// <summary>
    /// Replaces the default document title provider with a custom implementation.
    /// </summary>
    /// <typeparam name="TProvider">Custom title provider type.</typeparam>
    public ModuleMarkdownGuide UseDocumentTitleProvider<TProvider>()
        where TProvider : class, IDocumentTitleProvider
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.RemoveAll<IDocumentTitleProvider>();
            ctx.Services.AddSingleton<IDocumentTitleProvider, TProvider>();
        }, order: 0);

        return this;
    }
}

public class ModuleMarkdownOption : MoModuleOption<ModuleMarkdown>
{
    /// <summary>
    /// Registered document group descriptors, populated by Guide.
    /// </summary>
    public List<DocumentGroupRegistration> DocumentGroupRegistrations { get; set; } = [];

    /// <summary>
    /// File extensions recognized as markdown files.
    /// </summary>
    public string[] MarkdownFileExtensions { get; set; } = [".md", ".markdown"];

    /// <summary>
    /// Whether to parse YAML front matter from markdown files.
    /// </summary>
    public bool ParseFrontMatter { get; set; } = true;
}

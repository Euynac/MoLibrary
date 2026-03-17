using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Markdown.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

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
        services.TryAddSingleton<IMarkdownDocumentProvider,
            FileMarkdownDocumentProvider>();
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
    /// <param name="excludedFolders">Additional folders to exclude for this group only.</param>
    public ModuleMarkdownGuide AddDocumentGroup(
        string key,
        string title,
        string basePath,
        string? description = null,
        string[]? excludedFolders = null)
    {
        ConfigureModuleOption(option =>
        {
            option.DocumentGroupRegistrations.Add(new DocumentGroupRegistration
            {
                Key = key,
                Title = title,
                BasePath = basePath,
                Description = description,
                ExcludedFolders = excludedFolders
            });
        }, secondKey: key);

        return this;
    }

    /// <summary>
    /// Configures global folder exclusions that apply to all document groups.
    /// </summary>
    /// <param name="folderNames">Array of folder names to exclude (case-insensitive).</param>
    public ModuleMarkdownGuide WithExcludedFolders(params string[] folderNames)
    {
        ConfigureModuleOption(option =>
        {
            option.ExcludedFolders = folderNames;
        });

        return this;
    }

    /// <summary>
    /// Adds additional folders to the global exclusion list without replacing defaults.
    /// </summary>
    /// <param name="folderNames">Additional folder names to exclude.</param>
    public ModuleMarkdownGuide AddExcludedFolders(params string[] folderNames)
    {
        ConfigureModuleOption(option =>
        {
            var existing = option.ExcludedFolders ?? Array.Empty<string>();
            option.ExcludedFolders = existing.Concat(folderNames).Distinct(
                StringComparer.OrdinalIgnoreCase).ToArray();
        });

        return this;
    }

    /// <summary>
    /// Clears all global folder exclusions (including defaults).
    /// </summary>
    public ModuleMarkdownGuide ClearExcludedFolders()
    {
        ConfigureModuleOption(option =>
        {
            option.ExcludedFolders = Array.Empty<string>();
        });

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

    /// <summary>
    /// Replaces the default document provider with a custom implementation.
    /// </summary>
    /// <typeparam name="TProvider">Custom document provider type.</typeparam>
    public ModuleMarkdownGuide UseDocumentProvider<TProvider>()
        where TProvider : class, IMarkdownDocumentProvider
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.RemoveAll<IMarkdownDocumentProvider>();
            ctx.Services.AddSingleton<IMarkdownDocumentProvider, TProvider>();
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

    /// <summary>
    /// Global folder names to exclude from scanning across all document groups.
    /// Matching is case-insensitive and applies to directory names at any level.
    /// </summary>
    public string[] ExcludedFolders { get; set; } =
    [
        ".git",
        ".vs",
        ".vscode",
        ".idea",
        "node_modules",
        "bin",
        "obj",
        ".nuget",
        "packages",
        ".attachments",
        ".obsidian"
    ];
}

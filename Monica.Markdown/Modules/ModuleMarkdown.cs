using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Markdown.Abstractions;
using Monica.Markdown.Events;
using Monica.Markdown.Facades;
using Monica.Markdown.Models;
using Monica.Markdown.Providers.FileSystem;
using Monica.Markdown.Services;
using Monica.Markdown.UIMarkdown.Models;

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

[ModuleKey(BuiltInModuleKey.Markdown)]
public class ModuleMarkdown(ModuleMarkdownOption option)
    : ModuleBase<ModuleMarkdown, ModuleMarkdownOption, ModuleMarkdownGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<IMarkdownDocumentTitleResolver,
            FileNameMarkdownDocumentTitleResolver>();
        services.TryAddSingleton<IMarkdownDocumentProvider,
            FileSystemMarkdownDocumentProvider>();
        services.TryAddSingleton<IMarkdownDocumentSearcher, MarkdownDocumentSearchService>();
        services.AddSingleton<IMarkdownDocumentCatalog, MarkdownDocumentCatalogService>();
        services.AddSingleton<MarkdownFacade>();
        services.AddTransient<MarkdownGitBindingRefreshEventHandler>();
    }
}

public class ModuleMarkdownGuide
    : ModuleGuide<ModuleMarkdown, ModuleMarkdownOption, ModuleMarkdownGuide>
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
            option.DocumentGroupRegistrations.Add(new MarkdownDocumentGroupRegistration
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
    /// Binds a Git repository to Markdown document groups that should refresh after Git changes.
    /// </summary>
    public ModuleMarkdownGuide BindGitRepository(string repositoryId, params string[] documentGroupKeys)
    {
        ConfigureModuleOption(
            option => option.BindGitRepository(repositoryId, documentGroupKeys),
            secondKey: $"git-binding:{repositoryId}:{string.Join(",", documentGroupKeys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}");

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
        where TProvider : class, IMarkdownDocumentTitleResolver
    {
        ConfigureServices(ctx =>
        {
            ctx.Services.RemoveAll<IMarkdownDocumentTitleResolver>();
            ctx.Services.AddSingleton<IMarkdownDocumentTitleResolver, TProvider>();
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

public class ModuleMarkdownOption : ModuleOptions<ModuleMarkdown>
{
    /// <summary>
    /// Registered document group descriptors, populated by Guide.
    /// </summary>
    public List<MarkdownDocumentGroupRegistration> DocumentGroupRegistrations { get; set; } = [];

    /// <summary>
    /// Git to Markdown refresh bindings configured by the guide.
    /// </summary>
    public List<MarkdownGitRepositoryBinding> GitRepositoryBindings { get; set; } = [];

    /// <summary>
    /// File extensions recognized as markdown files.
    /// </summary>
    public string[] MarkdownFileExtensions { get; set; } = [".md", ".markdown"];

    /// <summary>
    /// Whether to parse YAML front matter from markdown files.
    /// </summary>
    public bool ParseFrontMatter { get; set; } = true;

    /// <summary>
    /// Selects the matching strategy used by markdown document search.
    /// This applies to both host integrations and the built-in UI dialog.
    /// </summary>
    public MarkdownSearchAlgorithm DocumentSearchAlgorithm { get; set; } =
        MarkdownSearchAlgorithm.KeywordFuzzy;

    /// <summary>
    /// Minimum normalized query length required before a search executes.
    /// Increase this to reduce low-signal broad matches.
    /// </summary>
    public int DocumentSearchMinQueryLength { get; set; } = 2;

    /// <summary>
    /// Maximum number of document results returned by each search request.
    /// Higher values improve recall but increase ranking and payload cost.
    /// </summary>
    public int DocumentSearchMaxResults { get; set; } = 50;

    /// <summary>
    /// Maximum number of preview characters returned for each search result.
    /// Longer previews provide more context but produce larger response payloads.
    /// </summary>
    public int DocumentSearchPreviewLength { get; set; } = 180;

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

    /// <summary>
    /// Adds Markdown refresh bindings for a Git repository.
    /// </summary>
    public void BindGitRepository(string repositoryId, IEnumerable<string> documentGroupKeys)
    {
        foreach (var groupKey in documentGroupKeys
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (GitRepositoryBindings.Any(x =>
                    string.Equals(x.RepositoryId, repositoryId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.DocumentGroupKey, groupKey, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            GitRepositoryBindings.Add(new MarkdownGitRepositoryBinding
            {
                RepositoryId = repositoryId,
                DocumentGroupKey = groupKey
            });
        }
    }

    /// <summary>
    /// Gets all Markdown document group keys bound to the specified Git repository.
    /// </summary>
    public IReadOnlyList<string> GetBoundDocumentGroupKeys(string repositoryId)
    {
        return GitRepositoryBindings
            .Where(x => string.Equals(x.RepositoryId, repositoryId, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.DocumentGroupKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

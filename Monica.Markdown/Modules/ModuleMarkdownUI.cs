using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Markdown.Pages;
using Monica.Markdown.Interfaces;
using Monica.Markdown.Services;
using Monica.Markdown.UIMarkdown.Models;
using Monica.Markdown.UIMarkdown.Services;
using Monica.UI.Components.Markdown;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Markdown UI module providing a document viewer with group selection,
/// tree navigation, and markdown rendering.
/// </summary>
public class ModuleMarkdownUI(ModuleMarkdownUIOption option)
    : MoModuleWithDependencies<ModuleMarkdownUI, ModuleMarkdownUIOption, ModuleMarkdownUIGuide>(option)
{
    /// <summary>
    /// Gets the module key for this UI module.
    /// </summary>
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.MarkdownUI;
    }

    /// <summary>
    /// Configures services for the Markdown UI module.
    /// </summary>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<MarkdownUIService>();
        services.AddScoped<MarkdownLocalImageAssetService>();
        services.TryAddSingleton<IMarkdownDocumentSearchService, MarkdownDocumentSearchService>();
        services.Replace(ServiceDescriptor.Scoped<IMoMarkdownAssetResolver, MarkdownKnowledgeBaseAssetResolver>());
    }

    /// <summary>
    /// Declares module dependencies.
    /// </summary>
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleMarkdownGuide>().Register();

        var uiCoreGuide = DependsOnModule<ModuleUICoreGuide>().Register();
        if (!Option.DisableMarkdownPage)
        {
            uiCoreGuide.RegisterUIComponents(p => p.RegisterLocalizedComponent<UIMarkdownPage>(
                MarkdownViewerLocation.PageUrl,
                "Pages:MarkdownDocuments:Title",
                Icons.Material.Filled.MenuBook,
                "Categories:Documentation",
                addToNav: true,
                navOrder: 50));
        }
    }

    /// <summary>
    /// Configures endpoints for local markdown image assets.
    /// </summary>
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        if (!Option.EnableLocalImageAssetEndpoint)
        {
            return;
        }

        UseEndpoints(app, endpoints =>
        {
            var tagName = Option.GetApiGroupName();
            var routePattern = NormalizeRoutePattern(Option.AssetEndpointBasePath);

            endpoints.MapGet(routePattern,
                    async ([FromRoute] string groupKey,
                           [FromQuery] string documentRelativePath,
                           [FromQuery] string assetPath,
                           [FromServices] MarkdownLocalImageAssetService assetService) =>
                    {
                        try
                        {
                            var asset = await assetService.OpenImageAsync(
                                groupKey,
                                documentRelativePath,
                                assetPath);

                            var stream = File.OpenRead(asset.FilePath);
                            return Results.File(stream, asset.ContentType, enableRangeProcessing: true);
                        }
                        catch (Exception ex) when (ex is InvalidOperationException
                                                   or FileNotFoundException
                                                   or DirectoryNotFoundException
                                                   or KeyNotFoundException)
                        {
                            return Results.NotFound(new
                            {
                                Message = ex.Message,
                                GroupKey = groupKey,
                                DocumentRelativePath = documentRelativePath,
                                AssetPath = assetPath
                            });
                        }
                    })
                .WithName("GetMarkdownLocalImageAsset")
                .WithTags(tagName)
                .WithSummary("Gets a local markdown image asset")
                .WithDescription("Streams a validated local image referenced by a markdown document.");
        });
    }

    private static string NormalizeRoutePattern(string? basePath)
    {
        var trimmed = string.IsNullOrWhiteSpace(basePath)
            ? "/markdown-ui/assets"
            : basePath.Trim();

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return $"{trimmed.TrimEnd('/')}/{{groupKey}}";
    }
}

public static class ModuleMarkdownUIBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Markdown UI module.
        /// </summary>
        public static ModuleMarkdownUIGuide AddMarkdownUI(Action<ModuleMarkdownUIOption>? action = null)
        {
            return new ModuleMarkdownUIGuide().Register(action);
        }
    }
}

/// <summary>
/// Guide for the Markdown UI module.
/// </summary>
public class ModuleMarkdownUIGuide : MoModuleGuide<ModuleMarkdownUI, ModuleMarkdownUIOption, ModuleMarkdownUIGuide>
{
    /// <summary>
    /// Gets the requested configuration method keys.
    /// </summary>
    protected override string[] GetRequestedConfigMethodKeys()
    {
        return [];
    }
}

/// <summary>
/// Options for the Markdown UI module.
/// </summary>
public class ModuleMarkdownUIOption : MoModuleOptionWithMinimalApi<ModuleMarkdownUI>
{
    /// <summary>
    /// Whether to disable the Markdown documents page.
    /// </summary>
    public bool DisableMarkdownPage { get; set; } = false;

    /// <summary>
    /// Whether the local markdown image asset endpoint is enabled.
    /// </summary>
    public bool EnableLocalImageAssetEndpoint { get; set; } = true;

    /// <summary>
    /// Base path for the local markdown asset endpoint.
    /// </summary>
    public string AssetEndpointBasePath { get; set; } = "/markdown-ui/assets";

    /// <summary>
    /// Selects the matching strategy used by document search.
    /// </summary>
    public EMarkdownDocumentSearchAlgorithm DocumentSearchAlgorithm { get; set; } =
        EMarkdownDocumentSearchAlgorithm.KeywordFuzzy;

    /// <summary>
    /// Minimum query length required before a search executes.
    /// </summary>
    public int DocumentSearchMinQueryLength { get; set; } = 2;

    /// <summary>
    /// Client-side debounce delay for search input, in milliseconds.
    /// </summary>
    public int DocumentSearchDebounceMilliseconds { get; set; } = 250;

    /// <summary>
    /// Maximum number of document results returned by each search request.
    /// </summary>
    public int DocumentSearchMaxResults { get; set; } = 50;

    /// <summary>
    /// Maximum number of preview characters displayed per result card.
    /// </summary>
    public int DocumentSearchPreviewLength { get; set; } = 180;

    /// <summary>
    /// Image extensions allowed to be served by the local markdown asset endpoint.
    /// </summary>
    public string[] AllowedImageExtensions { get; set; } =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".svg",
        ".bmp",
        ".ico",
        ".avif"
    ];
}

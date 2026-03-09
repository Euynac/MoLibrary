using Microsoft.Extensions.Options;
using Monica.Markdown.Interfaces;
using Monica.Modules;
using Monica.Markdown.UIMarkdown.Models;

namespace Monica.Markdown.UIMarkdown.Services;

/// <summary>
/// Resolves and validates local markdown image assets inside registered document groups.
/// </summary>
public class MarkdownLocalImageAssetService(
    IMoMarkdownService markdownService,
    IOptions<ModuleMarkdownUIOption> options)
{
    private static readonly IReadOnlyDictionary<string, string> KnownImageContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".svg"] = "image/svg+xml",
            [".bmp"] = "image/bmp",
            [".ico"] = "image/x-icon",
            [".avif"] = "image/avif"
        };

    private readonly ModuleMarkdownUIOption _option = options.Value;

    /// <summary>
    /// Builds a browser-safe URL for a local markdown image reference.
    /// </summary>
    public string? BuildRenderUrl(
        string? originalUrl,
        bool isImage,
        string? groupKey,
        string? documentRelativePath)
    {
        if (!isImage || string.IsNullOrWhiteSpace(originalUrl))
        {
            return originalUrl;
        }

        if (string.IsNullOrWhiteSpace(groupKey)
            || string.IsNullOrWhiteSpace(documentRelativePath)
            || IsAlreadyBrowserSafeUrl(originalUrl))
        {
            return originalUrl;
        }

        var endpointBasePath = NormalizeEndpointBasePath(_option.AssetEndpointBasePath);
        var encodedGroupKey = Uri.EscapeDataString(groupKey);
        var encodedDocumentPath = Uri.EscapeDataString(documentRelativePath);
        var encodedAssetPath = Uri.EscapeDataString(originalUrl);

        return $"{endpointBasePath}/{encodedGroupKey}?documentRelativePath={encodedDocumentPath}&assetPath={encodedAssetPath}";
    }

    /// <summary>
    /// Opens a local image asset after validating it against the registered group root.
    /// </summary>
    public async Task<MarkdownLocalImageAsset> OpenImageAsync(
        string groupKey,
        string documentRelativePath,
        string assetPath)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            throw new InvalidOperationException("The markdown asset request requires a group key.");
        }

        if (string.IsNullOrWhiteSpace(documentRelativePath))
        {
            throw new InvalidOperationException("The markdown asset request requires a document relative path.");
        }

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new InvalidOperationException("The markdown asset request requires an asset path.");
        }

        var group = await markdownService.GetDocumentGroupAsync(groupKey);
        if (!group.IsValid)
        {
            throw new DirectoryNotFoundException(
                $"Markdown document group '{groupKey}' is not accessible.");
        }

        var normalizedDocumentPath = NormalizeRelativePath(documentRelativePath);
        var normalizedAssetPath = NormalizeRelativePath(assetPath);

        if (string.IsNullOrWhiteSpace(normalizedDocumentPath))
        {
            throw new InvalidOperationException("The document relative path is invalid.");
        }

        if (string.IsNullOrWhiteSpace(normalizedAssetPath)
            || normalizedAssetPath.StartsWith('/')
            || Path.IsPathRooted(normalizedAssetPath)
            || Uri.TryCreate(normalizedAssetPath, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException(
                $"The markdown asset path '{assetPath}' is not a supported relative image path.");
        }

        var allowedExtensions = BuildAllowedExtensions();
        var extension = Path.GetExtension(normalizedAssetPath);
        if (!allowedExtensions.Contains(extension)
            || !KnownImageContentTypes.TryGetValue(extension, out var contentType))
        {
            throw new InvalidOperationException(
                $"The markdown asset extension '{extension}' is not allowed.");
        }

        var groupBasePath = Path.GetFullPath(group.BasePath);
        var documentDirectory = Path.GetDirectoryName(
                normalizedDocumentPath.Replace('/', Path.DirectorySeparatorChar))
            ?? string.Empty;

        var candidatePath = Path.GetFullPath(
            Path.Combine(groupBasePath, documentDirectory, normalizedAssetPath));

        if (IsOutsideGroupRoot(groupBasePath, candidatePath))
        {
            throw new InvalidOperationException(
                $"The markdown asset path '{assetPath}' resolves outside the group root.");
        }

        if (!File.Exists(candidatePath))
        {
            throw new FileNotFoundException(
                $"Markdown image asset not found: '{assetPath}'.", candidatePath);
        }

        return new MarkdownLocalImageAsset(candidatePath, contentType);
    }

    private HashSet<string> BuildAllowedExtensions()
    {
        var configuredExtensions = _option.AllowedImageExtensions;
        if (configuredExtensions is null || configuredExtensions.Length == 0)
        {
            return new HashSet<string>(KnownImageContentTypes.Keys, StringComparer.OrdinalIgnoreCase);
        }

        return configuredExtensions
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim().StartsWith('.') ? x.Trim() : $".{x.Trim()}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsAlreadyBrowserSafeUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (value.StartsWith('/')
            || value.StartsWith('#')
            || value.StartsWith("//", StringComparison.Ordinal)
            || Path.IsPathRooted(value))
        {
            return true;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "mailto", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEndpointBasePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/markdown-ui/assets";
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed.TrimEnd('/');
    }

    private static string NormalizeRelativePath(string value)
    {
        return value
            .Replace('\\', '/')
            .Trim();
    }

    private static bool IsOutsideGroupRoot(string groupBasePath, string candidatePath)
    {
        var relative = Path.GetRelativePath(groupBasePath, candidatePath);
        return relative.StartsWith("..", StringComparison.Ordinal)
               || Path.IsPathRooted(relative);
    }
}

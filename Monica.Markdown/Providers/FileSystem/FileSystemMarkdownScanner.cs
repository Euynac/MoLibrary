using Monica.Markdown.Abstractions;
using Monica.Markdown.Models;
using Monica.Modules;
using Monica.Tool.Algorithms.Trees;

namespace Monica.Markdown.Providers.FileSystem;

/// <summary>
/// Scans a directory for markdown files and builds a document group
/// with hierarchical tree structure.
/// </summary>
public static class FileSystemMarkdownScanner
{
    /// <summary>
    /// Scans a document group registration and produces a runtime MarkdownDocumentGroup.
    /// </summary>
    public static async Task<MarkdownDocumentGroup> ScanAsync(
        MarkdownDocumentGroupRegistration registration,
        ModuleMarkdownOption option,
        IMarkdownDocumentTitleResolver titleProvider)
    {
        var basePath = Path.GetFullPath(registration.BasePath);

        if (!Directory.Exists(basePath))
        {
            return new MarkdownDocumentGroup
            {
                Key = registration.Key,
                Title = registration.Title,
                Description = registration.Description,
                BasePath = basePath,
                IsValid = false,
                DocumentCount = 0,
                RootNode = new TreeNode<MarkdownDocumentNodeData>(
                    new MarkdownDocumentNodeData(registration.Title, false))
            };
        }

        var extensions = new HashSet<string>(
            option.MarkdownFileExtensions,
            StringComparer.OrdinalIgnoreCase);
        var folderMetadataFileNames = BuildFolderMetadataFileNameSet(option);

        var excludedFolders = BuildExclusionSet(option, registration);

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        var files = Directory.EnumerateFiles(basePath, "*", enumerationOptions)
            .Where(filePath => ShouldScanFile(filePath, basePath, extensions, excludedFolders))
            .ToList();

        var documents = new List<MarkdownDocument>();
        var folderMetadata = new Dictionary<string, MarkdownFolderMetadata>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(basePath, filePath)
                .Replace('\\', '/');

            MarkdownFrontMatter? frontMatter = null;
            if (option.ParseFrontMatter)
            {
                frontMatter = await MarkdownFrontMatterParser
                    .ParseFromFileAsync(filePath);
            }

            if (IsFolderMetadataFile(filePath, folderMetadataFileNames))
            {
                AddFolderMetadata(basePath, filePath, frontMatter, folderMetadata);
                continue;
            }

            var title = titleProvider.ResolveTitle(filePath, frontMatter);
            var fileInfo = new FileInfo(filePath);

            documents.Add(new MarkdownDocument
            {
                GroupKey = registration.Key,
                Title = title,
                NavigationTitle = ResolveDocumentNavigationTitle(title, frontMatter),
                NavigationOrder = frontMatter?.SidebarPosition,
                FilePath = filePath,
                RelativePath = relativePath,
                Level = relativePath.Count(c => c == '/'),
                FrontMatter = frontMatter,
                FileSize = fileInfo.Length,
                LastModifiedUtc = fileInfo.LastWriteTimeUtc
            });
        }

        var rootNode = TreeBuilder.BuildFromPaths(
            items: documents,
            pathSelector: doc => doc.RelativePath,
            directoryDataFactory: dirName =>
                new MarkdownDocumentNodeData(dirName, IsDocument: false),
            leafDataFactory: (doc, name) =>
                new MarkdownDocumentNodeData(
                    name,
                    IsDocument: true,
                    Document: doc,
                    DisplayName: doc.NavigationTitle,
                    NavigationOrder: doc.NavigationOrder),
            separator: '/');
        rootNode.Data = new MarkdownDocumentNodeData(
            string.Empty,
            IsDocument: false,
            DisplayName: registration.Title);
        ApplyFolderMetadata(rootNode, folderMetadata);

        rootNode.SortChildrenRecursive(CompareNavigationNodes);

        return new MarkdownDocumentGroup
        {
            Key = registration.Key,
            Title = registration.Title,
            Description = registration.Description,
            BasePath = basePath,
            IsValid = true,
            DocumentCount = documents.Count,
            RootNode = rootNode
        };
    }

    private static bool ShouldScanFile(
        string filePath,
        string basePath,
        HashSet<string> extensions,
        HashSet<string> excludedFolders)
    {
        if (!extensions.Contains(Path.GetExtension(filePath)))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(basePath, filePath);
        var directoryPath = Path.GetDirectoryName(relativePath) ?? string.Empty;

        return !ShouldExcludeDirectory(directoryPath, excludedFolders);
    }

    private static string ResolveDocumentNavigationTitle(
        string title,
        MarkdownFrontMatter? frontMatter)
    {
        return string.IsNullOrWhiteSpace(frontMatter?.SidebarLabel)
            ? title
            : frontMatter.SidebarLabel;
    }

    private static void AddFolderMetadata(
        string basePath,
        string filePath,
        MarkdownFrontMatter? frontMatter,
        IDictionary<string, MarkdownFolderMetadata> folderMetadata)
    {
        if (frontMatter is null)
        {
            return;
        }

        var folderPath = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var relativeFolderPath = NormalizeDirectoryRelativePath(
            Path.GetRelativePath(basePath, folderPath));
        var displayName = ResolveFolderDisplayName(relativeFolderPath, frontMatter);

        folderMetadata[relativeFolderPath] = new MarkdownFolderMetadata(
            relativeFolderPath,
            displayName,
            frontMatter.SidebarPosition,
            frontMatter);
    }

    private static string ResolveFolderDisplayName(
        string relativeFolderPath,
        MarkdownFrontMatter frontMatter)
    {
        if (!string.IsNullOrWhiteSpace(frontMatter.SidebarLabel))
        {
            return frontMatter.SidebarLabel;
        }

        if (!string.IsNullOrWhiteSpace(frontMatter.Title))
        {
            return frontMatter.Title;
        }

        var folderName = TryGetMetadataString(frontMatter, "name", "label");
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            return folderName;
        }

        return Path.GetFileName(relativeFolderPath);
    }

    private static string? TryGetMetadataString(
        MarkdownFrontMatter frontMatter,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!frontMatter.RawMetadata.TryGetValue(key, out var rawValue))
            {
                continue;
            }

            var value = rawValue?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static void ApplyFolderMetadata(
        TreeNode<MarkdownDocumentNodeData> rootNode,
        IReadOnlyDictionary<string, MarkdownFolderMetadata> folderMetadata)
    {
        ApplyFolderMetadata(rootNode, folderMetadata, string.Empty);
    }

    private static void ApplyFolderMetadata(
        TreeNode<MarkdownDocumentNodeData> node,
        IReadOnlyDictionary<string, MarkdownFolderMetadata> folderMetadata,
        string currentPath)
    {
        foreach (var child in node.Children)
        {
            if (child.Data.IsDocument)
            {
                continue;
            }

            var childPath = CombinePath(currentPath, child.Data.Name);
            if (folderMetadata.TryGetValue(childPath, out var metadata))
            {
                child.Data = child.Data with
                {
                    DisplayName = metadata.DisplayName,
                    NavigationOrder = metadata.NavigationOrder
                };
            }

            ApplyFolderMetadata(child, folderMetadata, childPath);
        }
    }

    private static int CompareNavigationNodes(
        TreeNode<MarkdownDocumentNodeData> a,
        TreeNode<MarkdownDocumentNodeData> b)
    {
        var orderComparison = CompareNavigationOrder(
            a.Data.NavigationOrder,
            b.Data.NavigationOrder);
        if (orderComparison != 0)
        {
            return orderComparison;
        }

        var aIsDirectory = !a.Data.IsDocument;
        var bIsDirectory = !b.Data.IsDocument;
        if (aIsDirectory != bIsDirectory)
        {
            return bIsDirectory.CompareTo(aIsDirectory);
        }

        return string.Compare(
            a.Data.ResolvedDisplayName,
            b.Data.ResolvedDisplayName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareNavigationOrder(int? a, int? b)
    {
        if (a.HasValue && b.HasValue)
        {
            return a.Value.CompareTo(b.Value);
        }

        if (a.HasValue)
        {
            return -1;
        }

        return b.HasValue ? 1 : 0;
    }

    private static string NormalizeDirectoryRelativePath(string relativePath)
    {
        return relativePath is "." or ""
            ? string.Empty
            : relativePath.Replace('\\', '/');
    }

    private static string CombinePath(string prefix, string name)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return name;
        }

        return $"{prefix}/{name}";
    }

    private static bool IsFolderMetadataFile(
        string filePath,
        HashSet<string> folderMetadataFileNames)
    {
        return folderMetadataFileNames.Contains(Path.GetFileName(filePath));
    }

    private static HashSet<string> BuildFolderMetadataFileNameSet(
        ModuleMarkdownOption option)
    {
        return option.FolderMetadataFileNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if a directory path should be excluded based on exclusion patterns.
    /// </summary>
    private static bool ShouldExcludeDirectory(
        string directoryPath,
        HashSet<string> excludedFolders)
    {
        if (excludedFolders.Count == 0)
            return false;

        // Split path into segments and check each directory name
        var pathSegments = directoryPath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in pathSegments)
        {
            if (excludedFolders.Contains(segment))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Builds a combined set of excluded folder names from global and group-specific exclusions.
    /// </summary>
    private static HashSet<string> BuildExclusionSet(
        ModuleMarkdownOption option,
        MarkdownDocumentGroupRegistration registration)
    {
        var exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add global exclusions
        if (option.ExcludedFolders is { Length: > 0 })
        {
            foreach (var folder in option.ExcludedFolders)
            {
                if (!string.IsNullOrWhiteSpace(folder))
                    exclusions.Add(folder.Trim());
            }
        }

        // Add group-specific exclusions
        if (registration.ExcludedFolders is { Length: > 0 })
        {
            foreach (var folder in registration.ExcludedFolders)
            {
                if (!string.IsNullOrWhiteSpace(folder))
                    exclusions.Add(folder.Trim());
            }
        }

        return exclusions;
    }
}

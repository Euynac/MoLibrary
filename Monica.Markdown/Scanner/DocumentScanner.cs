using Monica.Markdown.Interfaces;
using Monica.Markdown.Models;
using Monica.Markdown.Modules;
using Monica.Tool.Algorithm.Tree;

namespace Monica.Markdown.Scanner;

/// <summary>
/// Scans a directory for markdown files and builds a document group
/// with hierarchical tree structure.
/// </summary>
public static class DocumentScanner
{
    /// <summary>
    /// Scans a document group registration and produces a runtime MarkdownDocumentGroup.
    /// </summary>
    public static async Task<MarkdownDocumentGroup> ScanAsync(
        DocumentGroupRegistration registration,
        ModuleMarkdownOption option,
        IDocumentTitleProvider titleProvider)
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

        // Build combined exclusion set
        var excludedFolders = BuildExclusionSet(option, registration);

        // Use EnumerationOptions for efficient filtering
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
        };

        // Enumerate files with directory filtering
        var files = Directory.EnumerateFiles(basePath, "*", enumerationOptions)
            .Where(filePath =>
            {
                // Check file extension
                if (!extensions.Contains(Path.GetExtension(filePath)))
                    return false;

                // Check if any parent directory should be excluded
                var relativePath = Path.GetRelativePath(basePath, filePath);
                var directoryPath = Path.GetDirectoryName(relativePath) ?? string.Empty;

                return !ShouldExcludeDirectory(directoryPath, excludedFolders);
            });

        var documents = new List<MarkdownDocument>();

        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(basePath, filePath)
                .Replace('\\', '/');
            var level = relativePath.Count(c => c == '/');

            MarkdownFrontMatter? frontMatter = null;
            if (option.ParseFrontMatter)
            {
                frontMatter = await FrontMatter.FrontMatterParser
                    .ParseFromFileAsync(filePath);
            }

            var title = titleProvider.ResolveTitle(filePath, frontMatter);
            var fileInfo = new FileInfo(filePath);

            documents.Add(new MarkdownDocument
            {
                GroupKey = registration.Key,
                Title = title,
                FilePath = filePath,
                RelativePath = relativePath,
                Level = level,
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
                new MarkdownDocumentNodeData(name, IsDocument: true, Document: doc),
            separator: '/');

        // Sort: directories first (alphabetical), then documents (alphabetical)
        rootNode.SortChildrenRecursive((a, b) =>
        {
            var aIsDir = !a.Data.IsDocument;
            var bIsDir = !b.Data.IsDocument;
            if (aIsDir != bIsDir) return bIsDir.CompareTo(aIsDir);
            return string.Compare(
                a.Data.Name, b.Data.Name, StringComparison.OrdinalIgnoreCase);
        });

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
        DocumentGroupRegistration registration)
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

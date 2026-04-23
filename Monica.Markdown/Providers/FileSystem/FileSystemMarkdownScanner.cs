using System.Globalization;
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
    /// Scans a document group registration and produces a runtime <see cref="MarkdownDocumentGroup"/>.
    /// </summary>
    public static async Task<MarkdownDocumentGroup> ScanAsync(
        MarkdownDocumentGroupRegistration registration,
        ModuleMarkdownOption option,
        IMarkdownDocumentTitleResolver titleProvider,
        ModuleLocalizationOption localizationOption)
    {
        var basePath = Path.GetFullPath(registration.BasePath);

        if (!Directory.Exists(basePath))
        {
            return CreateUnavailableGroup(registration, basePath);
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

        var multilingualContext = CreateMultilingualContext(
            basePath,
            option,
            localizationOption);
        var documents = new List<MarkdownDocument>();
        var rootFolderMetadata = new Dictionary<string, MarkdownFolderMetadata>(
            StringComparer.OrdinalIgnoreCase);
        var languageFolderMetadata = new Dictionary<string, Dictionary<string, MarkdownFolderMetadata>>(
            StringComparer.OrdinalIgnoreCase);
        var invalidMultilingualFiles = new List<string>();

        foreach (var filePath in files)
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(basePath, filePath));
            var documentCulture = ResolveDocumentCulture(
                relativePath,
                multilingualContext,
                out var navigationRelativePath);

            if (multilingualContext.HasLanguageRoots && documentCulture is null)
            {
                invalidMultilingualFiles.Add(relativePath);
                continue;
            }

            MarkdownFrontMatter? frontMatter = null;
            if (option.ParseFrontMatter)
            {
                frontMatter = await MarkdownFrontMatterParser.ParseFromFileAsync(filePath);
            }

            if (IsFolderMetadataFile(filePath, folderMetadataFileNames))
            {
                AddFolderMetadata(
                    relativePath,
                    navigationRelativePath,
                    documentCulture,
                    frontMatter,
                    rootFolderMetadata,
                    languageFolderMetadata);
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
                NavigationRelativePath = navigationRelativePath,
                Culture = documentCulture,
                Level = relativePath.Count(static c => c == '/'),
                FrontMatter = frontMatter,
                FileSize = fileInfo.Length,
                LastModifiedUtc = fileInfo.LastWriteTimeUtc
            });
        }

        var languages = BuildLanguages(multilingualContext, documents);
        if (multilingualContext.HasLanguageRoots && invalidMultilingualFiles.Count > 0)
        {
            return new MarkdownDocumentGroup
            {
                Key = registration.Key,
                Title = registration.Title,
                Description = registration.Description,
                BasePath = basePath,
                IsValid = true,
                DocumentCount = 0,
                IsMultilingual = true,
                Languages = languages,
                MultilingualLayoutError =
                    MarkdownMultilingualLayoutError.RootMarkdownFilesOutsideLanguageFolders,
                MultilingualLayoutErrorPaths = invalidMultilingualFiles
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                RootNode = CreateEmptyRootNode(registration.Title),
                Documents = [],
                LanguageRootNodes = new Dictionary<string, TreeNode<MarkdownDocumentNodeData>>(
                    StringComparer.OrdinalIgnoreCase),
                LanguageDocuments = new Dictionary<string, IReadOnlyList<MarkdownDocument>>(
                    StringComparer.OrdinalIgnoreCase)
            };
        }

        var rootNode = BuildDocumentTree(
            documents,
            static document => document.RelativePath,
            registration.Title);
        ApplyFolderMetadata(rootNode, rootFolderMetadata);
        ApplyLanguageRootMetadata(rootNode, languages);
        rootNode.SortChildrenRecursive(CompareNavigationNodes);

        var languageRootNodes = new Dictionary<string, TreeNode<MarkdownDocumentNodeData>>(
            StringComparer.OrdinalIgnoreCase);
        var languageDocuments = new Dictionary<string, IReadOnlyList<MarkdownDocument>>(
            StringComparer.OrdinalIgnoreCase);

        if (multilingualContext.HasLanguageRoots)
        {
            foreach (var language in languages)
            {
                var cultureDocuments = documents
                    .Where(document =>
                        string.Equals(document.Culture, language.Culture, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(static document => document.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                languageDocuments[language.Culture] = cultureDocuments;

                var languageTree = BuildDocumentTree(
                    cultureDocuments,
                    static document => document.NavigationRelativePath,
                    registration.Title);

                if (languageFolderMetadata.TryGetValue(language.Culture, out var folderMetadata))
                {
                    ApplyFolderMetadata(languageTree, folderMetadata);
                }

                languageTree.SortChildrenRecursive(CompareNavigationNodes);
                languageRootNodes[language.Culture] = languageTree;
            }
        }

        return new MarkdownDocumentGroup
        {
            Key = registration.Key,
            Title = registration.Title,
            Description = registration.Description,
            BasePath = basePath,
            IsValid = true,
            DocumentCount = documents.Count,
            IsMultilingual = multilingualContext.HasLanguageRoots,
            Languages = languages,
            RootNode = rootNode,
            Documents = documents
                .OrderBy(static document => document.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            LanguageRootNodes = languageRootNodes,
            LanguageDocuments = languageDocuments
        };
    }

    private static MarkdownDocumentGroup CreateUnavailableGroup(
        MarkdownDocumentGroupRegistration registration,
        string basePath)
    {
        return new MarkdownDocumentGroup
        {
            Key = registration.Key,
            Title = registration.Title,
            Description = registration.Description,
            BasePath = basePath,
            IsValid = false,
            DocumentCount = 0,
            RootNode = CreateEmptyRootNode(registration.Title),
            Documents = []
        };
    }

    private static TreeNode<MarkdownDocumentNodeData> BuildDocumentTree(
        IReadOnlyCollection<MarkdownDocument> documents,
        Func<MarkdownDocument, string> pathSelector,
        string rootDisplayName)
    {
        if (documents.Count == 0)
        {
            return CreateEmptyRootNode(rootDisplayName);
        }

        var rootNode = TreeBuilder.BuildFromPaths(
            items: documents,
            pathSelector: pathSelector,
            directoryDataFactory: dirName =>
                new MarkdownDocumentNodeData(dirName, IsDocument: false),
            leafDataFactory: (document, name) =>
                new MarkdownDocumentNodeData(
                    name,
                    IsDocument: true,
                    Document: document,
                    DisplayName: document.NavigationTitle,
                    NavigationOrder: document.NavigationOrder),
            separator: '/');

        rootNode.Data = new MarkdownDocumentNodeData(
            string.Empty,
            IsDocument: false,
            DisplayName: rootDisplayName);

        return rootNode;
    }

    private static TreeNode<MarkdownDocumentNodeData> CreateEmptyRootNode(string displayName)
    {
        return new TreeNode<MarkdownDocumentNodeData>(
            new MarkdownDocumentNodeData(
                string.Empty,
                IsDocument: false,
                DisplayName: displayName));
    }

    private static MultilingualScanContext CreateMultilingualContext(
        string basePath,
        ModuleMarkdownOption option,
        ModuleLocalizationOption localizationOption)
    {
        if (!option.EnableMultilingualDocuments)
        {
            return MultilingualScanContext.Disabled;
        }

        var supportedCultures = localizationOption.SupportedCultures
            .Where(static culture => !string.IsNullOrWhiteSpace(culture))
            .Select(static culture => culture.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (supportedCultures.Length == 0)
        {
            return MultilingualScanContext.Disabled;
        }

        var supportedCultureSet = supportedCultures.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var detectedCultureRoots = Directory.EnumerateDirectories(basePath, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Where(supportedCultureSet.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (detectedCultureRoots.Length == 0)
        {
            return new MultilingualScanContext(
                supportedCultures,
                supportedCultureSet,
                localizationOption.CultureDisplayNames,
                HasLanguageRoots: false);
        }

        return new MultilingualScanContext(
            supportedCultures,
            supportedCultureSet,
            localizationOption.CultureDisplayNames,
            HasLanguageRoots: true);
    }

    private static string? ResolveDocumentCulture(
        string relativePath,
        MultilingualScanContext context,
        out string navigationRelativePath)
    {
        navigationRelativePath = relativePath;
        if (!context.HasLanguageRoots)
        {
            return null;
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !context.SupportedCultureSet.Contains(segments[0]))
        {
            return null;
        }

        navigationRelativePath = string.Join('/', segments[1..]);
        return segments[0];
    }

    private static IReadOnlyList<MarkdownDocumentLanguage> BuildLanguages(
        MultilingualScanContext context,
        IReadOnlyCollection<MarkdownDocument> documents)
    {
        if (!context.HasLanguageRoots)
        {
            return [];
        }

        var counts = documents
            .Where(static document => !string.IsNullOrWhiteSpace(document.Culture))
            .GroupBy(static document => document.Culture!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.Count(),
                StringComparer.OrdinalIgnoreCase);

        var languages = new List<MarkdownDocumentLanguage>(counts.Count);
        foreach (var culture in context.SupportedCultures)
        {
            if (!counts.TryGetValue(culture, out var documentCount) || documentCount <= 0)
            {
                continue;
            }

            languages.Add(new MarkdownDocumentLanguage(
                culture,
                ResolveCultureDisplayName(culture, context.CultureDisplayNames),
                documentCount));
        }

        return languages;
    }

    private static string ResolveCultureDisplayName(
        string culture,
        IReadOnlyDictionary<string, string> cultureDisplayNames)
    {
        if (cultureDisplayNames.TryGetValue(culture, out var displayName)
            && !string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        try
        {
            return new CultureInfo(culture).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return culture;
        }
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
        string relativePath,
        string navigationRelativePath,
        string? culture,
        MarkdownFrontMatter? frontMatter,
        IDictionary<string, MarkdownFolderMetadata> rootFolderMetadata,
        IDictionary<string, Dictionary<string, MarkdownFolderMetadata>> languageFolderMetadata)
    {
        if (frontMatter is null)
        {
            return;
        }

        var rootFolderPath = NormalizeDirectoryRelativePath(
            Path.GetDirectoryName(relativePath) ?? string.Empty);
        rootFolderMetadata[rootFolderPath] = new MarkdownFolderMetadata(
            rootFolderPath,
            ResolveFolderDisplayName(rootFolderPath, frontMatter),
            frontMatter.SidebarPosition,
            frontMatter);

        if (string.IsNullOrWhiteSpace(culture))
        {
            return;
        }

        var languageFolderPath = NormalizeDirectoryRelativePath(
            Path.GetDirectoryName(navigationRelativePath) ?? string.Empty);

        if (!languageFolderMetadata.TryGetValue(culture, out var cultureMetadata))
        {
            cultureMetadata = new Dictionary<string, MarkdownFolderMetadata>(
                StringComparer.OrdinalIgnoreCase);
            languageFolderMetadata[culture] = cultureMetadata;
        }

        cultureMetadata[languageFolderPath] = new MarkdownFolderMetadata(
            languageFolderPath,
            ResolveFolderDisplayName(languageFolderPath, frontMatter),
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

    private static void ApplyLanguageRootMetadata(
        TreeNode<MarkdownDocumentNodeData> rootNode,
        IReadOnlyList<MarkdownDocumentLanguage> languages)
    {
        if (languages.Count == 0)
        {
            return;
        }

        var languageLookup = languages
            .Select((language, index) => new { Language = language, Index = index })
            .ToDictionary(
                static item => item.Language.Culture,
                static item => item,
                StringComparer.OrdinalIgnoreCase);

        foreach (var child in rootNode.Children.Where(static child => !child.Data.IsDocument))
        {
            if (!languageLookup.TryGetValue(child.Data.Name, out var language))
            {
                continue;
            }

            child.Data = child.Data with
            {
                DisplayName = language.Language.DisplayName,
                NavigationOrder = language.Index
            };
        }
    }

    private static int CompareNavigationNodes(
        TreeNode<MarkdownDocumentNodeData> left,
        TreeNode<MarkdownDocumentNodeData> right)
    {
        var orderComparison = CompareNavigationOrder(
            left.Data.NavigationOrder,
            right.Data.NavigationOrder);
        if (orderComparison != 0)
        {
            return orderComparison;
        }

        var leftIsDirectory = !left.Data.IsDocument;
        var rightIsDirectory = !right.Data.IsDocument;
        if (leftIsDirectory != rightIsDirectory)
        {
            return rightIsDirectory.CompareTo(leftIsDirectory);
        }

        return string.Compare(
            left.Data.ResolvedDisplayName,
            right.Data.ResolvedDisplayName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareNavigationOrder(int? left, int? right)
    {
        if (left.HasValue && right.HasValue)
        {
            return left.Value.CompareTo(right.Value);
        }

        if (left.HasValue)
        {
            return -1;
        }

        return right.HasValue ? 1 : 0;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
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
        {
            return false;
        }

        var pathSegments = directoryPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in pathSegments)
        {
            if (excludedFolders.Contains(segment))
            {
                return true;
            }
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

        if (option.ExcludedFolders is { Length: > 0 })
        {
            foreach (var folder in option.ExcludedFolders)
            {
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    exclusions.Add(folder.Trim());
                }
            }
        }

        if (registration.ExcludedFolders is not { Length: > 0 })
        {
            return exclusions;
        }

        foreach (var folder in registration.ExcludedFolders)
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                exclusions.Add(folder.Trim());
            }
        }

        return exclusions;
    }

    private sealed record MultilingualScanContext(
        IReadOnlyList<string> SupportedCultures,
        HashSet<string> SupportedCultureSet,
        IReadOnlyDictionary<string, string> CultureDisplayNames,
        bool HasLanguageRoots)
    {
        public static MultilingualScanContext Disabled { get; } = new(
            [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            HasLanguageRoots: false);
    }
}

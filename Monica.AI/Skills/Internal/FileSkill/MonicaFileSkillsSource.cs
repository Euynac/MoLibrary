using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Monica.AI.Skills.Internal.FileSkill;

internal sealed partial class MonicaFileSkillsSource
{
    private const string SKILL_FILE_NAME = "SKILL.md";
    private const int MAX_SEARCH_DEPTH = 2;
    private const string ROOT_DIRECTORY_INDICATOR = ".";

    private static readonly string[] DEFAULT_SCRIPT_EXTENSIONS = [".py", ".js", ".sh", ".ps1", ".cs", ".csx"];
    private static readonly string[] DEFAULT_RESOURCE_EXTENSIONS = [".md", ".json", ".yaml", ".yml", ".csv", ".xml", ".txt"];
    private static readonly string[] DEFAULT_SCRIPT_DIRECTORIES = ["scripts"];
    private static readonly string[] DEFAULT_RESOURCE_DIRECTORIES = ["references", "assets"];

    [GeneratedRegex(@"\A\uFEFF?^---\s*$(.+?)^---\s*$", RegexOptions.Multiline | RegexOptions.Singleline, 5000)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"^([\w-]+)\s*:\s*(?:[""'](.+?)[""']|(.+?))\s*$", RegexOptions.Multiline, 5000)]
    private static partial Regex YamlKeyValueRegex();

    [GeneratedRegex(@"^metadata\s*:\s*$\n((?:[ \t]+\S.*\n?)+)", RegexOptions.Multiline, 5000)]
    private static partial Regex YamlMetadataBlockRegex();

    [GeneratedRegex(@"^\s+([\w-]+)\s*:\s*(?:[""'](.+?)[""']|(.+?))\s*$", RegexOptions.Multiline, 5000)]
    private static partial Regex YamlIndentedKeyValueRegex();

    private readonly IReadOnlyList<string> _skillPaths;
    private readonly HashSet<string> _allowedResourceExtensions;
    private readonly HashSet<string> _allowedScriptExtensions;
    private readonly IReadOnlyList<string> _scriptDirectories;
    private readonly IReadOnlyList<string> _resourceDirectories;
    private readonly AgentFileSkillScriptRunner? _scriptRunner;
    private readonly IReadOnlyList<string> _configurationIssues;
    private readonly ILogger _logger;

    internal MonicaFileSkillsSource(
        IReadOnlyList<string> skillPaths,
        AgentFileSkillScriptRunner? scriptRunner,
        AgentFileSkillsSourceOptions? options,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(skillPaths);
        ArgumentNullException.ThrowIfNull(logger);

        ValidateExtensions(options?.AllowedResourceExtensions);
        ValidateExtensions(options?.AllowedScriptExtensions);

        var configurationIssues = new List<string>();
        _skillPaths = skillPaths;
        _allowedResourceExtensions = new HashSet<string>(
            options?.AllowedResourceExtensions ?? DEFAULT_RESOURCE_EXTENSIONS,
            StringComparer.OrdinalIgnoreCase);
        _allowedScriptExtensions = new HashSet<string>(
            options?.AllowedScriptExtensions ?? DEFAULT_SCRIPT_EXTENSIONS,
            StringComparer.OrdinalIgnoreCase);
        _scriptDirectories = options?.ScriptDirectories is not null
            ? ValidateAndNormalizeDirectoryNames(options.ScriptDirectories, logger, configurationIssues).ToList()
            : DEFAULT_SCRIPT_DIRECTORIES;
        _resourceDirectories = options?.ResourceDirectories is not null
            ? ValidateAndNormalizeDirectoryNames(options.ResourceDirectories, logger, configurationIssues).ToList()
            : DEFAULT_RESOURCE_DIRECTORIES;
        _scriptRunner = scriptRunner;
        _configurationIssues = configurationIssues;
        _logger = logger;
    }

    internal IReadOnlyList<AgentSkill> GetSkills()
    {
        return Discover().Skills;
    }

    internal MonicaFileSkillsDiscoveryResult Discover()
    {
        var discoveredPaths = DiscoverSkillDirectories(_skillPaths);
        var skills = new List<AgentSkill>();

        foreach (var skillPath in discoveredPaths)
        {
            if (!skillPath.ContainsSkillFile)
            {
                continue;
            }

            MonicaFileSkill? skill;
            try
            {
                skill = ParseSkillDirectory(skillPath.FullPath, skillPath.Source.Issues);
            }
            catch (Exception ex) when (IsDiscoveryException(ex))
            {
                var issue = $"Failed to read skill directory '{skillPath.FullPath}': {ex.Message}";
                skillPath.Source.Issues.Add(issue);
                _logger.LogError(ex, "Failed to read file-based AI skill from '{SkillPath}'.", skillPath.FullPath);
                continue;
            }

            if (skill is not null)
            {
                skills.Add(skill);
                skillPath.Source.LoadedSkillNames.Add(skill.Frontmatter.Name);

                if (_scriptRunner is null && skill.Scripts.Count > 0)
                {
                    skillPath.Source.Issues.Add(
                        $"Skill '{skill.Frontmatter.Name}' declares {skill.Scripts.Count} script(s), but no script runner is configured.");
                }
            }
        }

        var pathStatuses = discoveredPaths
            .Select(static path => path.Source)
            .Distinct()
            .ToList();

        foreach (var source in pathStatuses)
        {
            if (source.Exists && source.DiscoveredSkillDirectoryCount == 0)
            {
                source.Issues.Add(
                    $"No {SKILL_FILE_NAME} file was found under '{source.Path}' within search depth {MAX_SEARCH_DEPTH}.");
            }
        }

        return new MonicaFileSkillsDiscoveryResult(
            skills,
            pathStatuses.Select(static source => source.ToStatus()).ToList(),
            _configurationIssues);
    }

    private static List<DiscoveredSkillDirectory> DiscoverSkillDirectories(IEnumerable<string> skillPaths)
    {
        var discoveredPaths = new List<DiscoveredSkillDirectory>();

        foreach (var rootDirectory in skillPaths)
        {
            var source = new PathDiscoveryBuilder(rootDirectory);
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                source.Issues.Add("A configured file-skill path is empty.");
                discoveredPaths.Add(new DiscoveredSkillDirectory(string.Empty, source, ContainsSkillFile: false));
                continue;
            }

            string fullRootDirectory;
            try
            {
                fullRootDirectory = Path.GetFullPath(rootDirectory);
            }
            catch (Exception ex) when (IsDiscoveryException(ex))
            {
                source.Issues.Add($"File-skill path '{rootDirectory}' is invalid: {ex.Message}");
                discoveredPaths.Add(new DiscoveredSkillDirectory(rootDirectory, source, ContainsSkillFile: false));
                continue;
            }

            if (!Directory.Exists(fullRootDirectory))
            {
                source.Issues.Add($"Configured file-skill path '{rootDirectory}' does not exist.");
                discoveredPaths.Add(new DiscoveredSkillDirectory(fullRootDirectory, source, ContainsSkillFile: false));
                continue;
            }

            source.Exists = true;
            SearchDirectoriesForSkills(fullRootDirectory, discoveredPaths, source, currentDepth: 0);
            if (source.DiscoveredSkillDirectoryCount == 0)
            {
                discoveredPaths.Add(new DiscoveredSkillDirectory(fullRootDirectory, source, ContainsSkillFile: false));
            }
        }

        return discoveredPaths;
    }

    private static void SearchDirectoriesForSkills(
        string directory,
        List<DiscoveredSkillDirectory> results,
        PathDiscoveryBuilder source,
        int currentDepth)
    {
        if (File.Exists(Path.Combine(directory, SKILL_FILE_NAME)))
        {
            source.DiscoveredSkillDirectoryCount++;
            results.Add(new DiscoveredSkillDirectory(Path.GetFullPath(directory), source, ContainsSkillFile: true));
        }

        if (currentDepth >= MAX_SEARCH_DEPTH)
        {
            return;
        }

        try
        {
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                SearchDirectoriesForSkills(subdirectory, results, source, currentDepth + 1);
            }
        }
        catch (Exception ex) when (IsDiscoveryException(ex))
        {
            source.Issues.Add($"Could not scan directory '{directory}': {ex.Message}");
        }
    }

    private MonicaFileSkill? ParseSkillDirectory(string skillDirectoryFullPath, ICollection<string> issues)
    {
        var skillFilePath = Path.Combine(skillDirectoryFullPath, SKILL_FILE_NAME);
        var content = File.ReadAllText(skillFilePath, Encoding.UTF8);

        if (!TryParseFrontmatter(content, skillFilePath, issues, out var frontmatter) || frontmatter is null)
        {
            return null;
        }

        var normalizedSkillDirectoryFullPath = skillDirectoryFullPath + Path.DirectorySeparatorChar;
        var resources = DiscoverResourceFiles(normalizedSkillDirectoryFullPath, frontmatter.Name, issues);
        var scripts = DiscoverScriptFiles(normalizedSkillDirectoryFullPath, frontmatter.Name, issues);

        return new MonicaFileSkill(
            frontmatter,
            content,
            skillDirectoryFullPath,
            resources,
            scripts);
    }

    private bool TryParseFrontmatter(
        string content,
        string skillFilePath,
        ICollection<string> issues,
        out AgentSkillFrontmatter? frontmatter)
    {
        frontmatter = null;

        var match = FrontmatterRegex().Match(content);
        if (!match.Success)
        {
            issues.Add($"SKILL.md at '{skillFilePath}' does not contain valid YAML frontmatter.");
            _logger.LogError("SKILL.md at '{SkillFilePath}' does not contain valid YAML frontmatter.", skillFilePath);
            return false;
        }

        var yamlContent = match.Groups[1].Value.Trim();
        var fields = ParseFrontmatterFields(yamlContent);

        if (!AgentSkillFrontmatter.ValidateName(fields.Name, out var validationReason)
            || !AgentSkillFrontmatter.ValidateDescription(fields.Description, out validationReason))
        {
            issues.Add($"SKILL.md at '{skillFilePath}' has invalid frontmatter: {validationReason}");
            _logger.LogError(
                "SKILL.md at '{SkillFilePath}' has invalid frontmatter: {Reason}",
                skillFilePath,
                validationReason);
            return false;
        }

        frontmatter = new AgentSkillFrontmatter(fields.Name!, fields.Description!, fields.Compatibility)
        {
            License = fields.License,
            AllowedTools = fields.AllowedTools,
            Metadata = fields.Metadata
        };

        var directoryName = Path.GetFileName(Path.GetDirectoryName(skillFilePath)) ?? string.Empty;
        if (string.Equals(frontmatter.Name, directoryName, StringComparison.Ordinal))
        {
            return true;
        }

        issues.Add(
            $"SKILL.md at '{skillFilePath}': skill name '{frontmatter.Name}' does not match parent directory name '{directoryName}'.");
        _logger.LogError(
            "SKILL.md at '{SkillFilePath}': skill name '{SkillName}' does not match parent directory name '{DirectoryName}'.",
            skillFilePath,
            frontmatter.Name,
            directoryName);
        frontmatter = null;
        return false;
    }

    private static FrontmatterFields ParseFrontmatterFields(string yamlContent)
    {
        string? name = null;
        string? description = null;
        string? license = null;
        string? compatibility = null;
        string? allowedTools = null;

        foreach (Match kvMatch in YamlKeyValueRegex().Matches(yamlContent))
        {
            var key = kvMatch.Groups[1].Value;
            var value = kvMatch.Groups[2].Success ? kvMatch.Groups[2].Value : kvMatch.Groups[3].Value;
            if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
            {
                name = value;
            }
            else if (string.Equals(key, "description", StringComparison.OrdinalIgnoreCase))
            {
                description = value;
            }
            else if (string.Equals(key, "license", StringComparison.OrdinalIgnoreCase))
            {
                license = value;
            }
            else if (string.Equals(key, "compatibility", StringComparison.OrdinalIgnoreCase))
            {
                compatibility = value;
            }
            else if (string.Equals(key, "allowed-tools", StringComparison.OrdinalIgnoreCase))
            {
                allowedTools = value;
            }
        }

        return new FrontmatterFields(name, description, license, compatibility, allowedTools, ParseMetadata(yamlContent));
    }

    private static AdditionalPropertiesDictionary? ParseMetadata(string yamlContent)
    {
        var metadataMatch = YamlMetadataBlockRegex().Match(yamlContent);
        if (!metadataMatch.Success)
        {
            return null;
        }

        var metadata = new AdditionalPropertiesDictionary();
        foreach (Match kvMatch in YamlIndentedKeyValueRegex().Matches(metadataMatch.Groups[1].Value))
        {
            metadata[kvMatch.Groups[1].Value] = kvMatch.Groups[2].Success
                ? kvMatch.Groups[2].Value
                : kvMatch.Groups[3].Value;
        }

        return metadata;
    }

    private IReadOnlyList<AgentSkillResource> DiscoverResourceFiles(
        string skillDirectoryFullPath,
        string skillName,
        ICollection<string> issues)
    {
        var resources = DiscoverFiles(
                skillDirectoryFullPath,
                skillName,
                _resourceDirectories,
                _allowedResourceExtensions,
                isResource: true,
                issues)
            .ToList();

        return BuildResourceList(resources);
    }

    private IReadOnlyList<AgentSkillScript> DiscoverScriptFiles(
        string skillDirectoryFullPath,
        string skillName,
        ICollection<string> issues)
    {
        return DiscoverFiles(
                skillDirectoryFullPath,
                skillName,
                _scriptDirectories,
                _allowedScriptExtensions,
                isResource: false,
                issues)
            .Select(file => (AgentSkillScript)new MonicaFileSkillScript(file.RelativePath, file.FullPath, _scriptRunner))
            .ToList();
    }

    private IEnumerable<DiscoveredFile> DiscoverFiles(
        string skillDirectoryFullPath,
        string skillName,
        IEnumerable<string> directories,
        ISet<string> allowedExtensions,
        bool isResource,
        ICollection<string> issues)
    {
        foreach (var directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var isRootDirectory = string.Equals(directory, ROOT_DIRECTORY_INDICATOR, StringComparison.Ordinal);
            var targetDirectory = isRootDirectory
                ? skillDirectoryFullPath
                : Path.GetFullPath(Path.Combine(skillDirectoryFullPath, directory)) + Path.DirectorySeparatorChar;

            if (!Directory.Exists(targetDirectory)
                || (!isRootDirectory && HasSymlinkInPath(targetDirectory, skillDirectoryFullPath)))
            {
                continue;
            }

            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            foreach (var filePath in Directory.EnumerateFiles(targetDirectory, "*", enumerationOptions))
            {
                if (isResource
                    && string.Equals(Path.GetFileName(filePath), SKILL_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var extension = Path.GetExtension(filePath);
                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                {
                    continue;
                }

                var resolvedFilePath = Path.GetFullPath(filePath);
                if (!resolvedFilePath.StartsWith(targetDirectory, StringComparison.OrdinalIgnoreCase)
                    || HasSymlinkInPath(resolvedFilePath, targetDirectory))
                {
                    issues.Add(
                        $"Skipping {(isResource ? "resource" : "script")} '{filePath}' in skill '{skillName}' because it is outside the allowed directory.");
                    _logger.LogWarning(
                        "Skipping {FileKind} in skill '{SkillName}': '{FilePath}' is outside the allowed directory.",
                        isResource ? "resource" : "script",
                        skillName,
                        filePath);
                    continue;
                }

                yield return new DiscoveredFile(
                    NormalizePath(resolvedFilePath[skillDirectoryFullPath.Length..]),
                    resolvedFilePath);
            }
        }
    }

    private static bool HasSymlinkInPath(string pathToCheck, string trustedBasePath)
    {
        var relativePath = pathToCheck[trustedBasePath.Length..];
        var segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var currentPath = trustedBasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var segment in segments)
        {
            currentPath = Path.Combine(currentPath, segment);
            if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        if (path.StartsWith("./", StringComparison.Ordinal) || path.StartsWith(".\\", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        path = path.TrimEnd('/', '\\');
        return path.IndexOf('\\', StringComparison.Ordinal) >= 0
            ? path.Replace('\\', '/')
            : path;
    }

    private static void ValidateExtensions(IEnumerable<string>? extensions)
    {
        if (extensions is null)
        {
            return;
        }

        foreach (var extension in extensions)
        {
            if (string.IsNullOrWhiteSpace(extension) || !extension.StartsWith(".", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Each extension must start with '.'. Invalid value: '{extension}'.");
            }
        }
    }

    private static IEnumerable<string> ValidateAndNormalizeDirectoryNames(
        IEnumerable<string> directories,
        ILogger logger,
        ICollection<string> issues)
    {
        foreach (var directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Directory names must not be null or whitespace.", nameof(directories));
            }

            if (string.Equals(directory, ROOT_DIRECTORY_INDICATOR, StringComparison.Ordinal))
            {
                yield return directory;
                continue;
            }

            if (Path.IsPathRooted(directory) || ContainsParentTraversalSegment(directory))
            {
                issues.Add(
                    $"Skipping invalid file-skill directory name '{directory}': it must be relative and contain no '..' segments.");
                logger.LogWarning(
                    "Skipping invalid file-skill directory name '{DirectoryName}': it must be relative and contain no '..' segments.",
                    directory);
                continue;
            }

            yield return NormalizePath(directory);
        }
    }

    private static bool ContainsParentTraversalSegment(string directory)
    {
        return directory.Split(["/", "\\"], StringSplitOptions.None).Any(static segment => segment == "..");
    }

    private static IReadOnlyList<AgentSkillResource> BuildResourceList(IReadOnlyList<DiscoveredFile> files)
    {
        var resources = new List<AgentSkillResource>();
        var fileNameCounts = files
            .GroupBy(static file => Path.GetFileName(file.RelativePath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var file in files.OrderBy(static file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(file.RelativePath);
            var hasUniqueFileName = fileNameCounts[fileName] == 1;
            var canonicalName = hasUniqueFileName ? fileName : file.RelativePath;
            var description = string.Equals(canonicalName, file.RelativePath, StringComparison.Ordinal)
                ? null
                : $"File resource at {file.RelativePath}.";

            resources.Add(new MonicaFileSkillResource(
                canonicalName,
                file.FullPath,
                description,
                canonicalName));

            if (hasUniqueFileName && !string.Equals(fileName, file.RelativePath, StringComparison.Ordinal))
            {
                resources.Add(new MonicaFileSkillResource(
                    file.RelativePath,
                    file.FullPath,
                    $"Alias for {fileName}.",
                    canonicalName: fileName));
            }
        }

        return resources;
    }

    private static bool IsDiscoveryException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException;
    }

    private sealed record FrontmatterFields(
        string? Name,
        string? Description,
        string? License,
        string? Compatibility,
        string? AllowedTools,
        AdditionalPropertiesDictionary? Metadata);

    private sealed record DiscoveredSkillDirectory(
        string FullPath,
        PathDiscoveryBuilder Source,
        bool ContainsSkillFile);

    private sealed class PathDiscoveryBuilder(string path)
    {
        internal string Path { get; } = path;

        internal bool Exists { get; set; }

        internal int DiscoveredSkillDirectoryCount { get; set; }

        internal List<string> LoadedSkillNames { get; } = [];

        internal List<string> Issues { get; } = [];

        internal MonicaFileSkillPathDiscoveryStatus ToStatus()
        {
            var loadedSkillNames = LoadedSkillNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new MonicaFileSkillPathDiscoveryStatus(
                Path,
                Exists,
                loadedSkillNames.Count,
                loadedSkillNames,
                Issues.Distinct(StringComparer.Ordinal).ToList());
        }
    }

    private sealed record DiscoveredFile(string RelativePath, string FullPath);
}

using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.Skills.Internal;

namespace Monica.AI.Skills.Internal.FileSkill;

/// <summary>
/// Loads file skills through Agent Framework and inspects only filesystem discovery status for Monica management views.
/// </summary>
internal sealed class FileSkillCatalogLoader(
    IReadOnlyList<string> skillPaths,
    AgentFileSkillScriptRunner? scriptRunner,
    AgentFileSkillsSourceOptions? options,
    ILoggerFactory loggerFactory)
{
    private const string SKILL_FILE_NAME = "SKILL.md";
    private const int MAX_SKILL_SEARCH_DEPTH = 2;

    internal MonicaFileSkillsDiscoveryResult Discover()
    {
        using var source = new AgentFileSkillsSource(skillPaths, scriptRunner, options, loggerFactory);

        // AgentFileSkillsSource 1.13 does not consume its context. Catalog discovery occurs before a chat agent exists,
        // while runtime filtering remains context-aware in the provider pipeline.
        var skills = source.GetSkillsAsync(context: null!).GetAwaiter().GetResult().ToList();
        var paths = skillPaths
            .Select(path => InspectPath(path, skills, scriptRunner is not null))
            .ToList();

        return new MonicaFileSkillsDiscoveryResult(skills, paths, []);
    }

    private static MonicaFileSkillPathDiscoveryStatus InspectPath(
        string configuredPath,
        IReadOnlyList<AgentSkill> loadedSkills,
        bool hasScriptRunner)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new MonicaFileSkillPathDiscoveryStatus(
                configuredPath,
                false,
                0,
                [],
                ["A configured file-skill path is empty."]);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(configuredPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return new MonicaFileSkillPathDiscoveryStatus(
                configuredPath,
                false,
                0,
                [],
                [$"File-skill path '{configuredPath}' is invalid: {ex.Message}"]);
        }

        if (!Directory.Exists(fullPath))
        {
            return new MonicaFileSkillPathDiscoveryStatus(
                fullPath,
                false,
                0,
                [],
                [$"Configured file-skill path '{configuredPath}' does not exist."]);
        }

        var candidateDirectories = DiscoverCandidateDirectories(fullPath);
        var skills = loadedSkills
            .OfType<AgentFileSkill>()
            .Where(skill => IsWithinPath(skill.Path, fullPath))
            .ToList();
        var loadedPaths = skills
            .Select(static skill => Path.GetFullPath(skill.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var issues = candidateDirectories
            .Where(candidate => !loadedPaths.Contains(candidate))
            .Select(candidate => $"Agent Framework rejected the skill manifest at '{Path.Combine(candidate, SKILL_FILE_NAME)}'.")
            .ToList();

        if (!hasScriptRunner)
        {
            issues.AddRange(skills
                .Where(skill => AgentSkillSnapshot.Create(skill).Scripts.Count > 0)
                .Select(skill => $"Skill '{skill.Frontmatter.Name}' declares scripts, but no script runner is configured."));
        }

        if (candidateDirectories.Count == 0)
        {
            issues.Add(
                $"No {SKILL_FILE_NAME} file was found under '{configuredPath}' within search depth {MAX_SKILL_SEARCH_DEPTH}.");
        }

        var names = skills
            .Select(static skill => skill.Frontmatter.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new MonicaFileSkillPathDiscoveryStatus(fullPath, true, names.Count, names, issues);
    }

    private static List<string> DiscoverCandidateDirectories(string rootPath)
    {
        var results = new List<string>();
        Search(rootPath, currentDepth: 0);
        return results;

        void Search(string path, int currentDepth)
        {
            if (File.Exists(Path.Combine(path, SKILL_FILE_NAME)))
            {
                results.Add(Path.GetFullPath(path));
                return;
            }

            if (currentDepth >= MAX_SKILL_SEARCH_DEPTH)
            {
                return;
            }

            try
            {
                var enumerationOptions = new EnumerationOptions
                {
                    RecurseSubdirectories = false,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };
                foreach (var directory in Directory.EnumerateDirectories(path, "*", enumerationOptions))
                {
                    Search(directory, currentDepth + 1);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Agent Framework logs inaccessible directories. This inspector intentionally remains diagnostic-only.
            }
        }
    }

    private static bool IsWithinPath(string candidatePath, string rootPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        return relativePath == "."
               || (!Path.IsPathRooted(relativePath)
                   && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                   && relativePath != "..");
    }
}

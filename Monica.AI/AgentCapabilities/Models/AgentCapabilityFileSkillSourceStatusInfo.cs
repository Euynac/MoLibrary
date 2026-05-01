namespace Monica.AI.AgentCapabilities.Models;

/// <summary>
/// Management status for one registered file-based skill source.
/// </summary>
/// <param name="ConfiguredPaths">Filesystem roots configured by the host application.</param>
/// <param name="HasScriptRunner">Whether scripts from this source have a runner configured.</param>
/// <param name="UsesSubprocessRunner">Whether this source uses Monica's built-in subprocess script runner.</param>
/// <param name="LoadedSkillCount">Number of skills successfully loaded from this source.</param>
/// <param name="LoadedSkillNames">Names of skills successfully loaded from this source.</param>
/// <param name="Paths">Per-root discovery status.</param>
/// <param name="Issues">Source-level configuration or discovery issues.</param>
public sealed record AgentCapabilityFileSkillSourceStatusInfo(
    IReadOnlyList<string> ConfiguredPaths,
    bool HasScriptRunner,
    bool UsesSubprocessRunner,
    int LoadedSkillCount,
    IReadOnlyList<string> LoadedSkillNames,
    IReadOnlyList<AgentCapabilityFileSkillPathStatusInfo> Paths,
    IReadOnlyList<string> Issues)
{
    /// <summary>
    /// Gets whether any source-level or path-level issue was reported.
    /// </summary>
    public bool HasIssues => Issues.Count > 0 || Paths.Any(static path => path.Issues.Count > 0);
}

/// <summary>
/// Discovery status for one configured file-skill root path.
/// </summary>
/// <param name="Path">Configured filesystem root.</param>
/// <param name="Exists">Whether the configured root existed during discovery.</param>
/// <param name="LoadedSkillCount">Number of skills successfully loaded from this root.</param>
/// <param name="LoadedSkillNames">Names of skills successfully loaded from this root.</param>
/// <param name="Issues">Path-level discovery issues.</param>
public sealed record AgentCapabilityFileSkillPathStatusInfo(
    string Path,
    bool Exists,
    int LoadedSkillCount,
    IReadOnlyList<string> LoadedSkillNames,
    IReadOnlyList<string> Issues);

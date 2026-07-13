using Microsoft.Agents.AI;

namespace Monica.AI.Skills.Models;

/// <summary>
/// Describes one group of file-based Agent Framework skills registered with Monica.
/// </summary>
/// <param name="SkillPaths">
/// Filesystem roots searched for skill directories. Each root may be a single skill directory containing
/// <c>SKILL.md</c> or a package directory containing multiple child skill directories.
/// </param>
/// <param name="ScriptRunner">
/// Optional runner used when discovered skills contain scripts. Scripts without a runner remain discoverable
/// but fail when invoked by Agent Framework.
/// </param>
/// <param name="Options">
/// Optional Agent Framework discovery options for search depth, extension allow-lists, and resource/script filters.
/// </param>
/// <param name="UsesSubprocessRunner">Whether this registration uses Monica's built-in subprocess script runner.</param>
public sealed record ExternalFileSkillRegistration(
    IReadOnlyList<string> SkillPaths,
    AgentFileSkillScriptRunner? ScriptRunner,
    AgentFileSkillsSourceOptions? Options,
    bool UsesSubprocessRunner);

using Microsoft.Agents.AI;

namespace Monica.AI.Skills.Internal.FileSkill;

internal sealed record MonicaFileSkillsDiscoveryResult(
    IReadOnlyList<AgentSkill> Skills,
    IReadOnlyList<MonicaFileSkillPathDiscoveryStatus> Paths,
    IReadOnlyList<string> Issues);

internal sealed record MonicaFileSkillPathDiscoveryStatus(
    string Path,
    bool Exists,
    int LoadedSkillCount,
    IReadOnlyList<string> LoadedSkillNames,
    IReadOnlyList<string> Issues);

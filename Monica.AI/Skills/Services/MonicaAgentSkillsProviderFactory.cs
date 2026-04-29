using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Creates the singleton Microsoft agent skills provider from Monica's discovered skill catalog.
/// </summary>
public sealed class MonicaAgentSkillsProviderFactory(
    MonicaSkillCatalog skillCatalog,
    ILoggerFactory loggerFactory)
{
    private readonly Lazy<AgentSkillsProvider> _provider = new(() =>
    {
        return new AgentSkillsProviderBuilder()
            .UseSkills(skillCatalog.GetActiveSkills())
            .UseLoggerFactory(loggerFactory)
            .Build();
    });

    /// <summary>
    /// Gets the process-static skills provider.
    /// </summary>
    public AgentSkillsProvider GetProvider() => _provider.Value;
}

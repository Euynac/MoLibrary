using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Creates Microsoft agent skills providers from Monica's discovered skill catalog.
/// </summary>
public sealed class MonicaAgentSkillsProviderFactory(
    MonicaSkillCatalog skillCatalog,
    ILoggerFactory loggerFactory)
{
    /// <summary>
    /// Creates a provider for the currently enabled skills.
    /// </summary>
    public AgentSkillsProvider CreateProvider(AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new AgentSkillsProviderBuilder()
            .UseSkills(skillCatalog.GetActiveSkills(state))
            .UseLoggerFactory(loggerFactory)
            .Build();
    }
}

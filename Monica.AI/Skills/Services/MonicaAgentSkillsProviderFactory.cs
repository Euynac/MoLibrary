using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Creates Microsoft agent skills providers from Monica's discovered skill catalog.
/// </summary>
internal sealed class MonicaAgentSkillsProviderFactory(
    MonicaSkillCatalog skillCatalog,
    ILoggerFactory loggerFactory)
{
    /// <summary>
    /// Creates a provider for the currently enabled skills.
    /// </summary>
    internal AgentSkillsProvider CreateProvider(AgentCapabilityState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new AgentSkillsProviderBuilder()
            .UseSkills(skillCatalog.GetAvailableSkills())
            .UseFilter((skill, _) => skillCatalog.IsEnabled(state, skill.Frontmatter.Name))
            .UseOptions(options =>
            {
                options.DisableLoadSkillApproval = true;
                options.DisableReadSkillResourceApproval = true;
            })
            .UseLoggerFactory(loggerFactory)
            .Build();
    }
}

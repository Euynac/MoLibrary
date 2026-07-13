using Microsoft.Agents.AI;
using Monica.AI.Abstractions;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Contributes the enabled Monica skill catalog to each composed chat agent.
/// </summary>
internal sealed class SkillChatAgentContributor(
    MonicaAgentSkillsProviderFactory providerFactory,
    MonicaSkillCatalog skillCatalog)
    : IAIChatAgentContributor
{
    /// <inheritdoc />
    public ValueTask ContributeAsync(
        AIChatAgentContributionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        context.AddContextProvider(providerFactory.CreateProvider(context.CapabilityState));
        context.AddToolAutoApprovalRule(functionCall => ValueTask.FromResult(
            string.Equals(
                functionCall.Name,
                AgentSkillsProvider.RunSkillScriptToolName,
                StringComparison.Ordinal)
            && TryGetSkillName(functionCall, out var skillName)
            && skillCatalog.IsTrustedCodeSkill(skillName)));
        return ValueTask.CompletedTask;
    }

    private static bool TryGetSkillName(
        Microsoft.Extensions.AI.FunctionCallContent functionCall,
        out string skillName)
    {
        skillName = string.Empty;
        if (functionCall.Arguments is null)
        {
            return false;
        }

        if (!functionCall.Arguments.TryGetValue("skillName", out var value)
            && !functionCall.Arguments.TryGetValue("skill_name", out value))
        {
            return false;
        }

        skillName = value?.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(skillName);
    }
}

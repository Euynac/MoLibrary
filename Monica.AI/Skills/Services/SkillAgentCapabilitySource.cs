using Monica.AI.AgentCapabilities.Abstractions;
using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.Skills.Services;

/// <summary>
/// Projects the discovered Monica skill catalog into the shared capability-management model.
/// </summary>
internal sealed class SkillAgentCapabilitySource(MonicaSkillCatalog catalog) : IAgentCapabilitySource
{
    public AgentCapabilityKind Kind => AgentCapabilityKind.Skill;

    public Task<AgentCapabilitySourceSnapshot> GetSnapshotAsync(
        AgentCapabilityState state,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new AgentCapabilitySourceSnapshot(
            catalog.GetEntries(state),
            catalog.GetFileSkillSourceStatuses()));
    }

    public Task InvalidateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

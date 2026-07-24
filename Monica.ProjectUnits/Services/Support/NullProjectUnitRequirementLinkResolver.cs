using Monica.ProjectUnits.Abstractions;
using Monica.ProjectUnits.Models;

namespace Monica.ProjectUnits.Services.Support;

internal sealed class NullProjectUnitRequirementLinkResolver : IProjectUnitRequirementLinkResolver
{
    public ValueTask<ProjectUnitRequirementLink?> ResolveAsync(
        string requirementId,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<ProjectUnitRequirementLink?>(null);
    }
}

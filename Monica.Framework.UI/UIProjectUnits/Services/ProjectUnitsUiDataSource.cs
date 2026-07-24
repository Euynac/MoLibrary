using Monica.Core.Results;
using Monica.ProjectUnits.Facades;
using Monica.ProjectUnits.Models;

namespace Monica.Framework.UI.UIProjectUnits.Services;

internal interface IProjectUnitsUiDataSource
{
    Task<Res<ProjectUnitDashboardSnapshot>> GetDashboardAsync();

    Task<Res<ProjectUnitDetail>> GetDetailAsync(
        string key,
        CancellationToken cancellationToken = default);
}

internal sealed class ProjectUnitsUiDataSource(ProjectUnitsFacade facade) : IProjectUnitsUiDataSource
{
    public Task<Res<ProjectUnitDashboardSnapshot>> GetDashboardAsync()
    {
        return facade.GetDashboardAsync();
    }

    public Task<Res<ProjectUnitDetail>> GetDetailAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return facade.GetProjectUnitDetailAsync(key, cancellationToken);
    }
}

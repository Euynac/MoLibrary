using MoLibrary.Configuration.Model;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Interfaces;

public interface IMoConfigurationDashboard
{
    /// <summary>
    /// 重新排布默认规则
    /// </summary>
    /// <returns></returns>
    public Task<Res<List<DtoDomainConfigs>>> DashboardDisplayMode(List<DtoDomainConfigs> configs, string? mode);
}

public class DefaultArrangeDashboard : IMoConfigurationDashboard
{
    public async Task<Res<List<DtoDomainConfigs>>> DashboardDisplayMode(List<DtoDomainConfigs> configs, string? mode)
    {
        return configs;
    }
}
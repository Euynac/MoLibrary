using MoLibrary.Configuration.Model;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Interfaces;

public interface IMoConfigurationDashboard
{
    /// <summary>
    /// 重新排布默认规则
    /// </summary>
    /// <returns></returns>
    public Task<Res<dynamic>> DashboardDisplayMode(List<DtoDomainConfigs> configs, string? mode);
}

public class DefaultArrangeDashboard : IMoConfigurationDashboard
{
    public async Task<Res<dynamic>> DashboardDisplayMode(List<DtoDomainConfigs> configs, string? mode)
    {
        return configs;
    }
}
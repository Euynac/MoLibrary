using Monica.Configuration.Model;
using Monica.Tool.MoResponse;

namespace Monica.Configuration.UI.Interfaces;

public interface IMoConfigurationDashboard
{
    /// <summary>
    /// 重新排布默认规则
    /// </summary>
    /// <returns></returns>
    public Task<Res<List<DtoDomainGroup>>> DashboardDisplayMode(List<DtoDomainGroup> configs, string? mode);
}

public class DefaultArrangeDashboard : IMoConfigurationDashboard
{
    public async Task<Res<List<DtoDomainGroup>>> DashboardDisplayMode(List<DtoDomainGroup> configs, string? mode)
    {
        return configs;
    }
}
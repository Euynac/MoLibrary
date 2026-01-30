using Monica.RegisterCentre.Models;
using MudBlazor;

namespace Monica.Framework.UI.UIRegisterCentre.Extensions;

/// <summary>
/// ServiceStatus 枚举的扩展方法，提供统一的颜色、文本、图标映射
/// </summary>
public static class ServiceStatusExtensions
{
    private static readonly Dictionary<ServiceStatus, StatusDisplayInfo> Map = new()
    {
        [ServiceStatus.Running] = new(Color.Success, "#4caf50", "运行中", Icons.Material.Filled.CheckCircle),
        [ServiceStatus.Updating] = new(Color.Info, "#2196f3", "更新中", Icons.Material.Filled.Update),
        [ServiceStatus.Offline] = new(Color.Default, "#9e9e9e", "离线", Icons.Material.Filled.CloudOff),
        [ServiceStatus.Error] = new(Color.Error, "#f44336", "异常", Icons.Material.Filled.Error),
        [ServiceStatus.Unhealthy] = new(Color.Warning, "#ff9800", "不健康", Icons.Material.Filled.HealthAndSafety),
    };

    /// <summary>
    /// 默认显示信息，用于处理未知情况（如新增枚举值未更新映射）
    /// </summary>
    private static readonly StatusDisplayInfo DefaultInfo =
        new(Color.Default, "#757575", "未知", Icons.Material.Filled.Help);

    /// <summary>
    /// 获取状态的完整显示信息
    /// </summary>
    public static StatusDisplayInfo GetDisplayInfo(this ServiceStatus status)
        => Map.TryGetValue(status, out var info) ? info : DefaultInfo;

    /// <summary>
    /// 获取 MudBlazor Color 枚举值
    /// </summary>
    public static Color GetColor(this ServiceStatus status) => status.GetDisplayInfo().Color;

    /// <summary>
    /// 获取十六进制颜色值
    /// </summary>
    public static string GetHexColor(this ServiceStatus status) => status.GetDisplayInfo().HexColor;

    /// <summary>
    /// 获取显示文本
    /// </summary>
    public static string GetText(this ServiceStatus status) => status.GetDisplayInfo().Text;

    /// <summary>
    /// 获取图标路径
    /// </summary>
    public static string GetIcon(this ServiceStatus status) => status.GetDisplayInfo().Icon;

    /// <summary>
    /// 获取所有状态值（用于 UI 过滤器）
    /// </summary>
    public static IEnumerable<ServiceStatus> GetAllStatuses() => Enum.GetValues<ServiceStatus>();
}

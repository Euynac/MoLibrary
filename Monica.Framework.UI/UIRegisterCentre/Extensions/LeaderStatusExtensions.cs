using Monica.RegisterCentre.Models;
using MudBlazor;

namespace Monica.Framework.UI.UIRegisterCentre.Extensions;

/// <summary>
/// LeaderStatus 枚举的扩展方法，提供统一的颜色、文本、图标映射
/// </summary>
public static class LeaderStatusExtensions
{
    private static readonly Dictionary<LeaderStatus, StatusDisplayInfo> Map = new()
    {
        [LeaderStatus.Leader] = new(Color.Warning, "#ff9800", "Leader", Icons.Material.Filled.Star),
        [LeaderStatus.Follower] = new(Color.Default, "#9e9e9e", "Follower", Icons.Material.Filled.Circle),
        [LeaderStatus.Looking] = new(Color.Info, "#2196f3", "Looking", Icons.Material.Filled.Search),
    };

    /// <summary>
    /// 默认显示信息
    /// </summary>
    private static readonly StatusDisplayInfo DefaultInfo =
        new(Color.Default, "#757575", "未知", Icons.Material.Filled.Help);

    /// <summary>
    /// 获取状态的完整显示信息
    /// </summary>
    public static StatusDisplayInfo GetDisplayInfo(this LeaderStatus status)
        => Map.TryGetValue(status, out var info) ? info : DefaultInfo;

    /// <summary>
    /// 获取 MudBlazor Color 枚举值
    /// </summary>
    public static Color GetColor(this LeaderStatus status) => status.GetDisplayInfo().Color;

    /// <summary>
    /// 获取十六进制颜色值
    /// </summary>
    public static string GetHexColor(this LeaderStatus status) => status.GetDisplayInfo().HexColor;

    /// <summary>
    /// 获取显示文本
    /// </summary>
    public static string GetText(this LeaderStatus status) => status.GetDisplayInfo().Text;

    /// <summary>
    /// 获取图标路径
    /// </summary>
    public static string GetIcon(this LeaderStatus status) => status.GetDisplayInfo().Icon;
}

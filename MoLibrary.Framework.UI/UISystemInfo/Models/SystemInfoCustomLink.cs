namespace MoLibrary.Framework.UI.UISystemInfo.Models;

/// <summary>
/// 系统信息页面自定义快捷链接
/// </summary>
public record SystemInfoCustomLink
{
    /// <summary>
    /// 链接显示名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 链接URL（支持相对路径和绝对URL）
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// MudBlazor Material Icon 名称（例如: Icons.Material.Filled.Dashboard）
    /// </summary>
    public string Icon { get; init; } = MudBlazor.Icons.Material.Filled.Link;

    /// <summary>
    /// 链接描述/提示文本
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// 分类/分组名称（相同 Category 的链接会分组显示）
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// 显示顺序（数字越小越靠前，默认为0）
    /// </summary>
    public int Order { get; init; } = 0;

    /// <summary>
    /// 是否启用（默认为 true）
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 链接打开方式（默认 _blank 新标签页打开）
    /// </summary>
    public string Target { get; init; } = "_blank";
}

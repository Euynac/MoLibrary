namespace MoLibrary.UI.Components.ContextMenu;

/// <summary>
/// 右键菜单项 - 简化版本
/// </summary>
public class ContextMenuItem<TItem>
{
    /// <summary>
    /// 菜单项文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 菜单项图标
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 是否禁用
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// 是否为分隔线
    /// </summary>
    public bool IsDivider { get; set; }

    /// <summary>
    /// 点击事件处理
    /// </summary>
    public Func<TItem?, Task>? OnClick { get; set; }

    /// <summary>
    /// 快捷键提示文本（仅显示，不实际绑定）
    /// </summary>
    public string? ShortcutText { get; set; }

    /// <summary>
    /// 子菜单项（用于多级菜单）
    /// </summary>
    public List<ContextMenuItem<TItem>>? SubItems { get; set; }

    /// <summary>
    /// 是否有子菜单
    /// </summary>
    public bool HasSubMenu => SubItems != null && SubItems.Any();
}
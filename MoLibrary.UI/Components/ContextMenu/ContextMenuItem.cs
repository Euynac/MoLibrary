namespace MoLibrary.UI.Components.ContextMenu;

/// <summary>
/// 右键菜单项
/// </summary>
/// <typeparam name="TItem">关联的数据项类型</typeparam>
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
    /// 自定义数据
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 子菜单项（用于多级菜单）
    /// </summary>
    public List<ContextMenuItem<TItem>>? SubItems { get; set; }

    /// <summary>
    /// 是否有子菜单
    /// </summary>
    public bool HasSubMenu => SubItems != null && SubItems.Any();

    /// <summary>
    /// 创建菜单项
    /// </summary>
    public static ContextMenuItem<TItem> Create(string text, string? icon = null, Func<TItem?, Task>? onClick = null)
    {
        return new ContextMenuItem<TItem>
        {
            Text = text,
            Icon = icon,
            OnClick = onClick
        };
    }

    /// <summary>
    /// 创建分隔线
    /// </summary>
    public static ContextMenuItem<TItem> Divider()
    {
        return new ContextMenuItem<TItem> { IsDivider = true };
    }
}
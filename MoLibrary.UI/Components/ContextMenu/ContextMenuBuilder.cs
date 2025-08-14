namespace MoLibrary.UI.Components.ContextMenu;

/// <summary>
/// 右键菜单构建器 - 简化版本
/// </summary>
public class ContextMenuBuilder<TItem>
{
    private readonly List<ContextMenuItem<TItem>> _items = new();

    /// <summary>
    /// 添加菜单项
    /// </summary>
    public ContextMenuBuilder<TItem> AddItem(string text, string? icon = null, Func<TItem?, Task>? onClick = null, string? shortcut = null)
    {
        var item = new ContextMenuItem<TItem>
        {
            Text = text,
            Icon = icon,
            OnClick = onClick,
            ShortcutText = shortcut
        };
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// 添加子菜单项
    /// </summary>
    public ContextMenuBuilder<TItem> AddSubMenu(string text, string? icon, Action<ContextMenuBuilder<TItem>> configureSubMenu)
    {
        var subMenuBuilder = new ContextMenuBuilder<TItem>();
        configureSubMenu(subMenuBuilder);
        
        var item = new ContextMenuItem<TItem>
        {
            Text = text,
            Icon = icon,
            SubItems = subMenuBuilder.Build()
        };
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// 添加分隔线
    /// </summary>
    public ContextMenuBuilder<TItem> AddDivider()
    {
        _items.Add(new ContextMenuItem<TItem> { IsDivider = true });
        return this;
    }

    /// <summary>
    /// 条件添加菜单项
    /// </summary>
    public ContextMenuBuilder<TItem> AddItemIf(bool condition, string text, string? icon = null, Func<TItem?, Task>? onClick = null, string? shortcut = null)
    {
        if (condition)
        {
            AddItem(text, icon, onClick, shortcut);
        }
        return this;
    }

    /// <summary>
    /// 构建菜单项列表
    /// </summary>
    public List<ContextMenuItem<TItem>> Build() => _items;

    /// <summary>
    /// 创建一个新的构建器
    /// </summary>
    public static ContextMenuBuilder<TItem> Create() => new();
}
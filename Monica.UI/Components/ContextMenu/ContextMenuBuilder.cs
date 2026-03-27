namespace Monica.UI.Components.ContextMenu;

/// <summary>
/// Right-click menu builder - simplified version
/// </summary>
public class ContextMenuBuilder<TItem>
{
    private readonly List<ContextMenuItem<TItem>> _items = new();

    /// <summary>
    /// Add menu item
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
    /// Add submenu item
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
    /// add divider
    /// </summary>
    public ContextMenuBuilder<TItem> AddDivider()
    {
        _items.Add(new ContextMenuItem<TItem> { IsDivider = true });
        return this;
    }

    /// <summary>
    /// Conditionally add menu items
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
    /// Build a list of menu items
    /// </summary>
    public List<ContextMenuItem<TItem>> Build() => _items;

    /// <summary>
    /// Create a new builder
    /// </summary>
    public static ContextMenuBuilder<TItem> Create() => new();
}
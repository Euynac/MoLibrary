namespace Monica.UI.Components.ContextMenu;

/// <summary>
/// Right-click menu item - simplified version
/// </summary>
public class ContextMenuItem<TItem>
{
    /// <summary>
    /// Menu item text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// menu item icon
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Whether to disable
    /// </summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Whether it is a dividing line
    /// </summary>
    public bool IsDivider { get; set; }

    /// <summary>
    /// Click event handling
    /// </summary>
    public Func<TItem?, Task>? OnClick { get; set; }

    /// <summary>
    /// Shortcut key prompt text (only displayed, not actually bound)
    /// </summary>
    public string? ShortcutText { get; set; }

    /// <summary>
    /// Submenu items (for multi-level menus)
    /// </summary>
    public List<ContextMenuItem<TItem>>? SubItems { get; set; }

    /// <summary>
    /// Is there a submenu?
    /// </summary>
    public bool HasSubMenu => SubItems != null && SubItems.Any();
}
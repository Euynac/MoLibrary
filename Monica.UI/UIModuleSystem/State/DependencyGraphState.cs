using Monica.Core.Modularity.Diagnostics.Models;
using Monica.UI.Shared.Components.ContextMenu;

namespace Monica.UI.UIModuleSystem.State;

/// <summary>
/// Owns the mutable UI state for the dependency graph component.
/// </summary>
public sealed class DependencyGraphState
{
    private static readonly string[] AvailableTypeFilters = ["built-in", "ui", "third-party", "web", "web-required", "disabled", "cycle"];

    /// <summary>
    /// Gets the selected graph layout.
    /// </summary>
    public string SelectedLayout { get; private set; } = "force";

    /// <summary>
    /// Gets the selected edge filter.
    /// </summary>
    public string SelectedEdgeFilter { get; private set; } = "all";

    /// <summary>
    /// Gets the selected node type filters.
    /// </summary>
    public IReadOnlyCollection<string> SelectedTypeFilters { get; private set; } = CreateAllTypeFilters();

    /// <summary>
    /// Gets the current search text.
    /// </summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the related node filter key.
    /// </summary>
    public string? RelatedNodeId { get; private set; }

    /// <summary>
    /// Gets whether the context menu is open.
    /// </summary>
    public bool ContextMenuOpen { get; private set; }

    /// <summary>
    /// Gets the context menu horizontal position.
    /// </summary>
    public double ContextMenuX { get; private set; }

    /// <summary>
    /// Gets the context menu vertical position.
    /// </summary>
    public double ContextMenuY { get; private set; }

    /// <summary>
    /// Gets the node currently targeted by the context menu.
    /// </summary>
    public ModuleDependencyNode? ContextMenuNode { get; private set; }

    /// <summary>
    /// Gets the currently rendered context menu items.
    /// </summary>
    public List<ContextMenuItem<ModuleDependencyNode>> ContextMenuItems { get; private set; } = [];

    /// <summary>
    /// Updates the selected layout.
    /// </summary>
    public void SetLayout(string layout)
    {
        SelectedLayout = string.IsNullOrWhiteSpace(layout) ? "force" : layout;
    }

    /// <summary>
    /// Updates the selected edge filter.
    /// </summary>
    public void SetEdgeFilter(string filter)
    {
        SelectedEdgeFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter;
    }

    /// <summary>
    /// Updates the selected type filters.
    /// </summary>
    public void SetTypeFilters(IReadOnlyCollection<string>? filters)
    {
        SelectedTypeFilters = filters == null
            ? []
            : filters.Where(IsSupportedTypeFilter).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Selects every supported node type filter.
    /// </summary>
    public void SelectAllTypeFilters()
    {
        SelectedTypeFilters = CreateAllTypeFilters();
    }

    /// <summary>
    /// Clears every node type filter.
    /// </summary>
    public void ClearTypeFilters()
    {
        SelectedTypeFilters = [];
    }

    /// <summary>
    /// Updates the search text.
    /// </summary>
    public void SetSearchText(string? value)
    {
        SearchText = value ?? string.Empty;
    }

    /// <summary>
    /// Updates the related-node filter.
    /// </summary>
    public void SetRelatedNode(string? nodeId)
    {
        RelatedNodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
    }

    /// <summary>
    /// Resets every graph filter to the default state.
    /// </summary>
    public void ClearAllFilters()
    {
        SearchText = string.Empty;
        SelectedTypeFilters = CreateAllTypeFilters();
        SelectedEdgeFilter = "all";
        RelatedNodeId = null;
    }

    /// <summary>
    /// Opens the context menu for the provided node.
    /// </summary>
    public void OpenContextMenu(
        ModuleDependencyNode node,
        double x,
        double y,
        List<ContextMenuItem<ModuleDependencyNode>> items)
    {
        ContextMenuNode = node;
        ContextMenuX = x;
        ContextMenuY = y;
        ContextMenuItems = items;
        ContextMenuOpen = true;
    }

    /// <summary>
    /// Closes the current context menu.
    /// </summary>
    public void CloseContextMenu()
    {
        ContextMenuOpen = false;
    }

    /// <summary>
    /// Creates the payload consumed by the JS graph filter API.
    /// </summary>
    public object CreateFilterPayload()
    {
        return new
        {
            edgeFilter = SelectedEdgeFilter,
            typeFilters = SelectedTypeFilters,
            searchText = SearchText,
            relatedNodeId = RelatedNodeId
        };
    }

    private static IReadOnlyCollection<string> CreateAllTypeFilters()
    {
        return [.. AvailableTypeFilters];
    }

    private static bool IsSupportedTypeFilter(string filter)
    {
        return AvailableTypeFilters.Contains(filter, StringComparer.Ordinal);
    }
}

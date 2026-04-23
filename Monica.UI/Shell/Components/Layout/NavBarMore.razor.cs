using Microsoft.AspNetCore.Components;
using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Components.Layout;

public partial class NavBarMore
{
    [Parameter] public Dictionary<string, List<NavigationItem>> Categories { get; set; } = new();
    [Parameter] public bool CompactMode { get; set; }

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private string RootCssClass => CompactMode
        ? "navbar-more navbar-more-compact"
        : "navbar-more";

    private string GetMorePopoverClass() => CompactMode
        ? "mo-nav-menu-popover mo-nav-menu-popover-more mo-nav-menu-popover-scrollable mo-nav-menu-popover-compact"
        : "mo-nav-menu-popover mo-nav-menu-popover-more mo-nav-menu-popover-scrollable";

    private string GetCategoryPopoverClass() => CompactMode
        ? "mo-nav-menu-popover mo-nav-menu-popover-submenu mo-nav-menu-popover-scrollable mo-nav-menu-popover-compact"
        : "mo-nav-menu-popover mo-nav-menu-popover-submenu mo-nav-menu-popover-scrollable";

    private IReadOnlyList<KeyValuePair<string, List<NavigationItem>>> GetOrderedCategories() =>
        Categories.OrderBy(category => category.Key).ToList();
}

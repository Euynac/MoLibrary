using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Monica.Core.Localization.Abstractions;
using Monica.Modules;
using Monica.UI.Localization;
using Monica.UI.Shell.Models;
using Monica.UI.Shell.Support;

namespace Monica.UI.Shell.Components.Layout;

public partial class NavBar : IAsyncDisposable
{
    private const string LayoutModulePath = "./_content/Monica.UI/js/navbar-layout.js";
    private const int LayoutSafetyMarginPx = 8;

    [Inject] private IPageCatalog PageCatalog { get; set; } = default!;
    [Inject] private IOptions<ModuleShellUIOption> Options { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = default!;
    [Inject] private ILocalizationCatalog LocalizationCatalog { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private readonly List<NavigationGroup> _navigationGroups = [];

    private DotNetObjectReference<NavBar>? _selfReference;
    private ElementReference _desktopNavRef;
    private ElementReference _measurementRailRef;
    private IJSObjectReference? _layoutModule;
    private IJSObjectReference? _layoutObserver;
    private int _visibleCategoryCount;

    private int MaxVisibleCategories => Options.Value.MaxVisibleCategories;

    private IEnumerable<NavigationGroup> VisibleCategories =>
        _navigationGroups.Take(EffectiveVisibleCategoryCount);

    private bool HasMoreCategories => _navigationGroups.Count > EffectiveVisibleCategoryCount;

    private bool HasAnyCategories => _navigationGroups.Count > 0;

    private IReadOnlyList<NavigationGroup> AllCategories => _navigationGroups;

    private IReadOnlyList<NavigationGroup> MoreCategories =>
        _navigationGroups
            .Skip(EffectiveVisibleCategoryCount)
            .ToList();

    private int EffectiveVisibleCategoryCount =>
        Math.Clamp(_visibleCategoryCount, 0, Math.Min(MaxVisibleCategories, _navigationGroups.Count));

    protected override void OnInitialized()
    {
        var itemsByCategory = PageCatalog.GetNavItems()
            .GroupBy(static item => item.CategoryId)
            .ToDictionary(static group => group.Key, static group => (IReadOnlyList<NavigationItem>)group.ToList());

        foreach (var category in PageCatalog.GetNavigationCategories())
        {
            if (!itemsByCategory.TryGetValue(category.Id, out var items))
            {
                continue;
            }

            _navigationGroups.Add(
                new NavigationGroup(
                    category.Id,
                    category.ResolveDisplayName(LocalizationCatalog),
                    items));
        }

        _visibleCategoryCount = Math.Min(MaxVisibleCategories, _navigationGroups.Count);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _navigationGroups.Count == 0)
        {
            return;
        }

        _layoutModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", LayoutModulePath);
        _selfReference = DotNetObjectReference.Create(this);

        // Observe the desktop rail width so localized labels move into "More"
        // before the navigation spills into the action area.
        _layoutObserver = await _layoutModule.InvokeAsync<IJSObjectReference>(
            "createNavBarLayoutObserver",
            _desktopNavRef,
            _measurementRailRef,
            MaxVisibleCategories,
            LayoutSafetyMarginPx,
            _selfReference);
    }

    [JSInvokable]
    public Task UpdateVisibleCategoryCountAsync(int visibleCategoryCount)
    {
        var clampedVisibleCategoryCount = Math.Clamp(
            visibleCategoryCount,
            0,
            Math.Min(MaxVisibleCategories, _navigationGroups.Count));

        if (clampedVisibleCategoryCount == _visibleCategoryCount)
        {
            return Task.CompletedTask;
        }

        _visibleCategoryCount = clampedVisibleCategoryCount;
        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        if (_layoutObserver != null)
        {
            try
            {
                await _layoutObserver.InvokeVoidAsync("dispose");
                await _layoutObserver.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already disconnected.
            }
        }

        if (_layoutModule != null)
        {
            try
            {
                await _layoutModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already disconnected.
            }
        }

        _selfReference?.Dispose();
    }
}

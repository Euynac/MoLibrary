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

    [Inject] private IPageRegistry UIRegistry { get; set; } = default!;
    [Inject] private IOptions<ModuleShellUIOption> Options { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = default!;
    [Inject] private ILocalizationCatalog LocalizationCatalog { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private readonly Dictionary<string, List<NavigationItem>> _categorizedNavItems = [];
    private readonly List<string> _orderedCategoryNames = [];

    private DotNetObjectReference<NavBar>? _selfReference;
    private ElementReference _desktopNavRef;
    private ElementReference _measurementRailRef;
    private IJSObjectReference? _layoutModule;
    private IJSObjectReference? _layoutObserver;
    private int _visibleCategoryCount;

    private int MaxVisibleCategories => Options.Value.MaxVisibleCategories;

    private IEnumerable<KeyValuePair<string, List<NavigationItem>>> OrderedCategories =>
        _orderedCategoryNames.Select(name => new KeyValuePair<string, List<NavigationItem>>(name, _categorizedNavItems[name]));

    private IEnumerable<KeyValuePair<string, List<NavigationItem>>> VisibleCategories =>
        OrderedCategories.Take(EffectiveVisibleCategoryCount);

    private bool HasMoreCategories => _orderedCategoryNames.Count > EffectiveVisibleCategoryCount;

    private bool HasAnyCategories => _orderedCategoryNames.Count > 0;

    private Dictionary<string, List<NavigationItem>> AllCategories =>
        OrderedCategories.ToDictionary(entry => entry.Key, entry => entry.Value);

    private Dictionary<string, List<NavigationItem>> MoreCategories =>
        OrderedCategories
            .Skip(EffectiveVisibleCategoryCount)
            .ToDictionary(entry => entry.Key, entry => entry.Value);

    private int EffectiveVisibleCategoryCount =>
        Math.Clamp(_visibleCategoryCount, 0, Math.Min(MaxVisibleCategories, _orderedCategoryNames.Count));

    protected override void OnInitialized()
    {
        var allNavItems = UIRegistry.GetNavItems();

        foreach (var group in allNavItems
                     .GroupBy(item =>
                     {
                         return item.ResolveCategory(LocalizationCatalog) ??
                                L["ModuleSystem:Common:Labels:Uncategorized"];
                     })
                     .OrderBy(group => group.Key))
        {
            _categorizedNavItems[group.Key] = group.ToList();
            _orderedCategoryNames.Add(group.Key);
        }

        _visibleCategoryCount = Math.Min(MaxVisibleCategories, _orderedCategoryNames.Count);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _orderedCategoryNames.Count == 0)
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
            Math.Min(MaxVisibleCategories, _orderedCategoryNames.Count));

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

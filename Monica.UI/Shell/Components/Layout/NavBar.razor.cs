using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
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
    private const int LAYOUT_SAFETY_MARGIN_PX = 8;

    [Inject] private IPageCatalog PageCatalog { get; set; } = default!;
    [Inject] private IOptions<ModuleShellUIOption> Options { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> L { get; set; } = default!;
    [Inject] private ILocalizationCatalog LocalizationCatalog { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private PageAccessEvaluator PageAccess { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;

    private readonly List<NavigationGroup> _navigationGroups = [];
    private readonly List<Task> _pendingCallbacks = [];
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private ElementReference _desktopNavRef;
    private ElementReference _measurementRailRef;
    private readonly object _disposeSync = new();
    private NavBarLayoutInteropSession? _layoutInterop;
    private int _visibleCategoryCount;
    private AuthenticationStateProvider? _authenticationStateProvider;
    private Task? _disposeTask;
    private int _navigationRefreshVersion;
    private bool _disposed;

    private int MaxVisibleCategories => Options.Value.MaxVisibleCategories;

    private string CurrentRoute => NavigationManager.ToBaseRelativePath(NavigationManager.Uri);

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

    protected override async Task OnInitializedAsync()
    {
        _layoutInterop = new NavBarLayoutInteropSession(JSRuntime, this);
        NavigationManager.LocationChanged += HandleLocationChanged;
        _authenticationStateProvider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (_authenticationStateProvider is not null)
        {
            _authenticationStateProvider.AuthenticationStateChanged += HandleAuthenticationStateChanged;
        }

        await RebuildNavigationAsync(_lifetimeCancellation.Token);
    }

    private async Task<bool> RebuildNavigationAsync(CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            return false;
        }

        var refreshVersion = Interlocked.Increment(ref _navigationRefreshVersion);
        var navigationItems = new List<NavigationItem>();
        foreach (var navigationItem in PageCatalog.GetNavItems())
        {
            if (await PageAccess.IsAuthorizedAsync(navigationItem, cancellationToken))
            {
                navigationItems.Add(navigationItem);
            }
        }

        if (_disposed || refreshVersion != Volatile.Read(ref _navigationRefreshVersion))
        {
            return false;
        }

        _navigationGroups.Clear();
        var itemsByCategory = navigationItems
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
        return true;
    }

    private void HandleAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
    {
        QueueCallback(async () =>
        {
            try
            {
                await authenticationStateTask;
            }
            catch
            {
                // A failed authentication-state transition is treated as denied by the page policies below.
            }

            if (await RebuildNavigationAsync(_lifetimeCancellation.Token) && !_disposed)
            {
                StateHasChanged();
            }
        });
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        QueueCallback(() =>
        {
            StateHasChanged();
            return Task.CompletedTask;
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _navigationGroups.Count == 0 || _layoutInterop is not { } layoutInterop)
        {
            return;
        }

        // Observe the desktop rail width so localized labels move into "More"
        // before the navigation spills into the action area.
        await layoutInterop.InitializeAsync(
            _desktopNavRef,
            _measurementRailRef,
            MaxVisibleCategories,
            LAYOUT_SAFETY_MARGIN_PX);
    }

    [JSInvokable]
    public Task UpdateVisibleCategoryCountAsync(int visibleCategoryCount)
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

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

    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            return new ValueTask(_disposeTask ??= BeginDispose());
        }
    }

    private Task BeginDispose()
    {
        _disposed = true;
        Interlocked.Increment(ref _navigationRefreshVersion);
        _lifetimeCancellation.Cancel();
        NavigationManager.LocationChanged -= HandleLocationChanged;
        if (_authenticationStateProvider is not null)
        {
            _authenticationStateProvider.AuthenticationStateChanged -= HandleAuthenticationStateChanged;
        }

        var pendingCallbacks = _pendingCallbacks.ToArray();
        var layoutInterop = _layoutInterop;
        _layoutInterop = null;
        return DisposeCoreAsync(pendingCallbacks, layoutInterop);
    }

    private async Task DisposeCoreAsync(
        IReadOnlyList<Task> pendingCallbacks,
        NavBarLayoutInteropSession? layoutInterop)
    {
        try
        {
            try
            {
                await Task.WhenAll(pendingCallbacks);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
            }
        }
        finally
        {
            if (layoutInterop is not null)
            {
                await layoutInterop.DisposeAsync();
            }

            _lifetimeCancellation.Dispose();
        }
    }

    private void QueueCallback(Func<Task> callback)
    {
        lock (_disposeSync)
        {
            if (_disposed)
            {
                return;
            }

            _pendingCallbacks.RemoveAll(static task => task.IsCompleted);
            _pendingCallbacks.Add(InvokeAsync(callback));
        }
    }
}

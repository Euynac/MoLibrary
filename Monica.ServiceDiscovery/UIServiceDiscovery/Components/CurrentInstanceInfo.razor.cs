using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Monica.ServiceDiscovery.Localization;
using Monica.ServiceDiscovery.Models;
using Monica.ServiceDiscovery.UIServiceDiscovery.State;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.Components;

public partial class CurrentInstanceInfo : IDisposable
{
    [Inject] private CurrentInstanceInfoState State { get; set; } = default!;
    [Inject] private IStringLocalizer<ServiceDiscoveryResource> L { get; set; } = default!;

    private bool _isLoading => State.IsLoading;
    private bool _autoRefreshEnabled
    {
        get => State.AutoRefreshEnabled;
        set => State.AutoRefreshEnabled = value;
    }

    private int _refreshInterval
    {
        get => State.RefreshInterval;
        set => State.RefreshInterval = value;
    }

    private string? _errorMessage => State.ErrorMessage;
    private CurrentInstanceSnapshot? _snapshot => State.Snapshot;
    private InstanceState? _currentInstance => _snapshot?.CurrentInstance;
    private LeaderState? _clusterLeaderState => _snapshot?.ClusterLeaderState;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        State.ConfigureAutoRefresh(() => InvokeAsync(() => RefreshAsyncCore(false)));
        await RefreshAsyncCore();
    }

    private Task RefreshAsync()
    {
        return RefreshAsyncCore();
    }

    private async Task RefreshAsyncCore(bool showLoading = true)
    {
        _ = await State.RefreshAsync(showLoading);
        StateHasChanged();
    }

    private void OnAutoRefreshToggled()
    {
        State.UpdateAutoRefresh();
    }

    private void OnRefreshIntervalChanged()
    {
        State.UpdateAutoRefresh();
    }

    public void Dispose()
    {
        State.Dispose();
    }
}

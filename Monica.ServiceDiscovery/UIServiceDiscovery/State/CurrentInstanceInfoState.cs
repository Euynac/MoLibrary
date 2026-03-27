using Monica.ServiceDiscovery.Facades;
using Monica.ServiceDiscovery.Models;
using Monica.Tool.Results;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.State;

public sealed class CurrentInstanceInfoState(ServiceDiscoveryFacade serviceDiscoveryFacade)
    : IDisposable
{
    private Func<Task>? _refreshCallback;
    private Timer? _refreshTimer;

    public bool IsLoading { get; private set; }

    public bool AutoRefreshEnabled { get; set; } = true;

    public int RefreshInterval { get; set; } = 3;

    public string? ErrorMessage { get; private set; }

    public CurrentInstanceSnapshot? Snapshot { get; private set; }

    public void ConfigureAutoRefresh(Func<Task> refreshCallback)
    {
        _refreshCallback = refreshCallback;
        UpdateAutoRefresh();
    }

    public void UpdateAutoRefresh()
    {
        RefreshInterval = Math.Clamp(RefreshInterval, 1, 60);

        _refreshTimer?.Dispose();
        _refreshTimer = null;

        if (!AutoRefreshEnabled || _refreshCallback is null)
        {
            return;
        }

        _refreshTimer = new Timer(
            async _ => await _refreshCallback(),
            null,
            TimeSpan.FromSeconds(RefreshInterval),
            TimeSpan.FromSeconds(RefreshInterval));
    }

    public async Task<Res<CurrentInstanceSnapshot>> RefreshAsync(bool showLoading = true)
    {
        if (showLoading)
        {
            IsLoading = true;
        }

        ErrorMessage = null;

        try
        {
            var result = await serviceDiscoveryFacade.GetCurrentInstanceSnapshotAsync();
            if (result.IsFailed(out var error, out var snapshot))
            {
                ErrorMessage = error.Message;
                Snapshot = null;
                return result;
            }

            Snapshot = snapshot;
            return result;
        }
        finally
        {
            if (showLoading)
            {
                IsLoading = false;
            }
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}

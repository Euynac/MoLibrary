using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Monica.Framework.UI.UIRegisterCentre.Services;
using Monica.RegisterCentre.Interfaces;
using Monica.RegisterCentre.Models;
using Monica.Tool.MoResponse;
using MudBlazor;

namespace Monica.Framework.UI.UIRegisterCentre.Components;

public partial class CurrentInstanceInfo : IDisposable
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = default!;
    [Inject] private RegisterCentreService RegisterCentreService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private bool _isLoading = false;
    private bool _autoRefreshEnabled = true;
    private int _refreshInterval = 3;
    private Timer? _refreshTimer;
    private string? _errorMessage;

    private InstanceState? _currentInstance;
    private ILeaderElectionService? _leaderElectionService;
    private LeaderState? _clusterLeaderState;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RefreshAsync();
            SetupAutoRefresh();
        }
    }

    private async Task RefreshAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            // Get current instance info
            var clientInfo = ServiceProvider.GetService<IRegisterCentreClientInfo>();
            if (clientInfo == null)
            {
                _errorMessage = "当前服务未配置注册中心客户端信息";
                _currentInstance = null;
            }
            else
            {
                _currentInstance = clientInfo.GetServiceStatus();
            }

            // Get leader election service
            _leaderElectionService = ServiceProvider.GetService<ILeaderElectionService>();

            // Get cluster-wide leader state
            if (_currentInstance != null)
            {
                var leaderStateResult = await RegisterCentreService.GetLeaderStateAsync();
                if (!leaderStateResult.IsFailed(out var error, out var leaderState))
                {
                    _clusterLeaderState = leaderState;
                }
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"获取实例信息失败: {ex.Message}";
            _currentInstance = null;
        }

        _isLoading = false;
        StateHasChanged();
    }

    private void SetupAutoRefresh()
    {
        _refreshTimer?.Dispose();
        if (_autoRefreshEnabled)
        {
            _refreshTimer = new Timer(async _ => await InvokeAsync(RefreshAsync), null,
                TimeSpan.FromSeconds(_refreshInterval), TimeSpan.FromSeconds(_refreshInterval));
        }
    }

    private void OnAutoRefreshToggled()
    {
        SetupAutoRefresh();
    }

    private void OnRefreshIntervalChanged()
    {
        if (_autoRefreshEnabled)
        {
            SetupAutoRefresh();
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}

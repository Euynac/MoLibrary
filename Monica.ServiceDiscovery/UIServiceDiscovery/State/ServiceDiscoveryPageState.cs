using Monica.ServiceDiscovery.Facades;
using Monica.ServiceDiscovery.Models;
using Monica.ServiceDiscovery.UIServiceDiscovery.Support;
using Monica.Core.Results;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.State;

public sealed class ServiceDiscoveryPageState(
    ServiceDiscoveryFacade serviceDiscoveryFacade,
    ServiceInstanceEvictionTracker evictionTracker)
    : IDisposable
{
    private Func<Task>? _refreshCallback;
    private Timer? _refreshTimer;

    public string UnknownDomainLabel { get; set; } = "Unknown";

    public bool IsLoading { get; private set; }

    public List<RegisteredServiceStatus> Services { get; private set; } = [];

    public List<RegisteredServiceStatus> FilteredServices { get; private set; } = [];

    public bool AutoRefreshEnabled { get; set; } = true;

    public int RefreshInterval { get; set; } = 3;

    public DomainInfo? SelectedDomain { get; set; }

    public int ActiveTabIndex { get; set; }

    public string FilterText { get; set; } = string.Empty;

    public HashSet<ServiceStatus> SelectedStatuses { get; set; } = [];

    public HashSet<string> SelectedDomains { get; set; } = [];

    public bool ShowOnlyWithInstances { get; set; }

    public void Initialize(string unknownDomainLabel, Func<Task> refreshCallback)
    {
        UnknownDomainLabel = unknownDomainLabel;
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

    public async Task<Res<List<RegisteredServiceStatus>>> RefreshServicesAsync(bool showLoading = true)
    {
        if (showLoading)
        {
            IsLoading = true;
        }

        try
        {
            var result = await serviceDiscoveryFacade.GetMergedServicesStatusAsync();
            if (result.IsFailed(out _, out var services))
            {
                Services = [];
                ApplyFilters();
                return result;
            }

            Services = evictionTracker.Apply(services ?? []);
            ApplyFilters();
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

    public void SetFilterText(string text)
    {
        FilterText = text;
    }

    public void SetSelectedStatuses(IEnumerable<ServiceStatus> statuses)
    {
        SelectedStatuses = statuses.ToHashSet();
    }

    public void SetSelectedDomains(IEnumerable<string> domains)
    {
        SelectedDomains = domains.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void SetShowOnlyWithInstances(bool value)
    {
        ShowOnlyWithInstances = value;
    }

    public void SelectDomain(DomainInfo domain)
    {
        SelectedDomain = domain;
    }

    public void ClearSelectedDomain()
    {
        SelectedDomain = null;
    }

    public void OpenDomainManagement(DomainInfo domain, int tabIndex)
    {
        SelectedDomain = domain;
        ActiveTabIndex = tabIndex;
    }

    public void ApplyFilters()
    {
        FilteredServices = Services
            .Where(service =>
            {
                if (!string.IsNullOrWhiteSpace(FilterText))
                {
                    var searchText = FilterText.Trim();
                    var matchesText =
                        service.AppName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                        service.AppId.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(service.ProjectName) &&
                         service.ProjectName.Contains(searchText, StringComparison.OrdinalIgnoreCase)) ||
                        GetServiceDomainName(service).Contains(searchText, StringComparison.OrdinalIgnoreCase);

                    if (!matchesText)
                    {
                        return false;
                    }
                }

                if (SelectedStatuses.Count > 0 && !SelectedStatuses.Contains(service.OverallStatus))
                {
                    return false;
                }

                if (SelectedDomains.Count > 0 && !SelectedDomains.Contains(GetServiceDomainName(service)))
                {
                    return false;
                }

                if (ShowOnlyWithInstances && service.TotalInstanceCount == 0)
                {
                    return false;
                }

                return true;
            })
            .ToList();
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }

    private string GetServiceDomainName(RegisteredServiceStatus service)
        => service.DomainName ?? UnknownDomainLabel;
}

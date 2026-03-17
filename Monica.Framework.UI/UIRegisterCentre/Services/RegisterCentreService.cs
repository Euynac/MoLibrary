using Microsoft.Extensions.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Framework.UI.Localization;
using Monica.Modules;
using Monica.RegisterCentre.Interfaces;
using Monica.RegisterCentre.Models;
using Monica.Tool.MoResponse;

namespace Monica.Framework.UI.UIRegisterCentre.Services;

public class RegisterCentreService(
    ILogger<RegisterCentreService> logger,
    IServiceProvider serviceProvider,
    IOptions<ModuleRegisterCentreUIOption> uiOptions,
    IStringLocalizer<RegisterCentreResource> localizer)
{
    private static readonly Dictionary<string, string> _domainColors = new();
    private static List<DomainInfo> _cachedDomains = [];

    // Eviction tracking state
    private static Dictionary<string, InstanceState> _previousInstancesSnapshot = new();
    private static Dictionary<string, Queue<EvictedInstanceInfo>> _evictedInstances = new();
    private static readonly object _evictionLock = new();

    public async Task<Res<List<RegisteredServiceStatus>>> GetServicesStatusAsync()
    {
        try
        {
            var stateManager = serviceProvider.GetService<IRegistrationStateManager>();
            if (stateManager == null)
            {
                return Res.Fail(localizer["Service:Errors:StateManagerNotConfigured"].Value);
            }

            var instances = await stateManager.GetAllInstancesAsync();
            return ConvertToRegisteredServiceStatus(instances);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get service status.");
            return Res.Fail(localizer["Service:Errors:GetServicesStatusFailed", ex.Message].Value);
        }
    }

    /// <summary>
    /// Converts <see cref="InstanceState"/> items to grouped <see cref="RegisteredServiceStatus"/> values.
    /// </summary>
    private static List<RegisteredServiceStatus> ConvertToRegisteredServiceStatus(List<InstanceState> instances)
    {
        var result = new List<RegisteredServiceStatus>();

        // Group by ServiceName (AppId)
        var groupedByAppId = instances.GroupBy(i => i.ServiceName);

        foreach (var group in groupedByAppId)
        {
            var firstInstance = group.First();

            var serviceStatus = new RegisteredServiceStatus
            {
                AppId = firstInstance.ServiceName,
                AppName = firstInstance.AppName,
                DomainName = firstInstance.DomainName,
                ProjectName = firstInstance.ProjectName,
                DependentSubDomains = firstInstance.DependentSubDomains,
                Instances = group.ToDictionary(
                    i => i.InstanceId,
                    i =>
                    {
                        // Presence in the state store means the instance is online.
                        i.Status = ServiceStatus.Running;
                        return i;
                    })
            };

            result.Add(serviceStatus);
        }

        return result;
    }

    /// <summary>
    /// Gets the merged service status list including predefined and registered services.
    /// </summary>
    /// <returns>The merged service status list.</returns>
    public async Task<Res<List<RegisteredServiceStatus>>> GetMergedServicesStatusAsync()
    {
        try
        {
            var stateManager = serviceProvider.GetService<IRegistrationStateManager>();
            var infoProvider = serviceProvider.GetService<IRegisterCentreCatalogProvider>();

            // Load registered services.
            List<RegisteredServiceStatus> registeredServices = [];
            if (stateManager != null)
            {
                var instances = await stateManager.GetAllInstancesAsync();
                registeredServices = ConvertToRegisteredServiceStatus(instances);
            }

            // Load predefined services.
            List<PredefinedServiceInfo> preloadedServices = [];
            if (infoProvider != null)
            {
                preloadedServices = await infoProvider.GetPreloadedServicesAsync();
            }

            // Merge both service sets.
            var mergedServices = new List<RegisteredServiceStatus>(registeredServices);

            // Add predefined services that are not registered yet.
            foreach (var preloadedService in preloadedServices)
            {
                var existingService = registeredServices.FirstOrDefault(r => r.AppId == preloadedService.AppId);
                if (existingService == null)
                {
                    mergedServices.Add(new RegisteredServiceStatus
                    {
                        AppId = preloadedService.AppId,
                        AppName = preloadedService.AppName ?? preloadedService.AppId,
                    });
                }
            }

            return mergedServices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get merged service status.");
            return Res.Fail(localizer["Service:Errors:GetMergedServicesStatusFailed", ex.Message].Value);
        }
    }

    /// <summary>
    /// Gets merged services with evicted instance tracking.
    /// </summary>
    public async Task<Res<List<RegisteredServiceStatus>>> GetMergedServicesWithEvictionTrackingAsync()
    {
        var result = await GetMergedServicesStatusAsync();
        if (result.IsFailed(out var error, out var services))
            return error;

        DetectAndTrackEvictions(services);
        MergeEvictedInstances(services);

        return services;
    }

    private void DetectAndTrackEvictions(List<RegisteredServiceStatus> currentServices)
    {
        lock (_evictionLock)
        {
            // Build current instances map.
            var currentInstances = new Dictionary<string, InstanceState>();
            foreach (var service in currentServices)
            {
                foreach (var instance in service.Instances.Values)
                {
                    var key = $"{instance.ServiceName}:{instance.InstanceId}";
                    currentInstances[key] = instance;
                }
            }

            // Detect evictions by comparing with the previous snapshot.
            foreach (var (key, previousInstance) in _previousInstancesSnapshot)
            {
                if (!currentInstances.ContainsKey(key))
                {
                    var serviceName = previousInstance.ServiceName;

                    if (!_evictedInstances.ContainsKey(serviceName))
                        _evictedInstances[serviceName] = new Queue<EvictedInstanceInfo>();

                    var queue = _evictedInstances[serviceName];

                    queue.Enqueue(new EvictedInstanceInfo
                    {
                        InstanceId = previousInstance.InstanceId,
                        ServiceName = previousInstance.ServiceName,
                        AppName = previousInstance.AppName,
                        ProjectName = previousInstance.ProjectName,
                        DomainName = previousInstance.DomainName,
                        Status = previousInstance.Status,
                        EvictionTime = DateTime.Now,
                        LastHeartbeatTime = previousInstance.LastHeartbeatTime,
                        RegistrationTime = previousInstance.RegistrationTime,
                        AssemblyVersion = previousInstance.AssemblyVersion,
                        ReleaseVersion = previousInstance.ReleaseVersion,
                        BuildTime = previousInstance.BuildTime,
                        IsLeader = previousInstance.IsLeader
                    });

                    // Enforce max retention count.
                    var maxCount = uiOptions.Value.MaxEvictedServiceRetentionCount;
                    while (queue.Count > maxCount)
                        queue.Dequeue();
                }
            }

            // Update snapshot for the next comparison.
            _previousInstancesSnapshot = currentInstances;
        }
    }

    private void MergeEvictedInstances(List<RegisteredServiceStatus> services)
    {
        lock (_evictionLock)
        {
            foreach (var service in services)
            {
                if (_evictedInstances.TryGetValue(service.AppId, out var evictedQueue))
                {
                    service.EvictedInstances = evictedQueue.ToList();
                }
            }
        }
    }

    /// <summary>
    /// Gets domains and initializes color assignments.
    /// </summary>
    /// <returns>The domain list.</returns>
    public async Task<Res<List<DomainInfo>>> GetDomainsWithColorsAsync()
    {
        try
        {
            var infoProvider = serviceProvider.GetService<IRegisterCentreCatalogProvider>();
            if (infoProvider == null)
            {
                return Res.Fail(localizer["Service:Errors:InfoProviderNotConfigured"].Value);
            }

            var domains = await infoProvider.GetAllDomainsAsync();

            // Rebuild colors when the domain set changes.
            if (_cachedDomains.Count != domains.Count ||
                !domains.All(d => _cachedDomains.Any(c => c.Name == d.Name)))
            {
                _cachedDomains = domains.ToList();
                InitializeDomainColors(_cachedDomains);
                logger.LogInformation("Reinitialized domain color assignments. Domain count: {Count}", domains.Count);
            }

            return domains.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get domains.");
            return Res.Fail(localizer["Service:Errors:GetDomainsFailed", ex.Message].Value);
        }
    }

    /// <summary>
    /// Gets the configured color for a domain.
    /// </summary>
    /// <param name="domainName">The domain name.</param>
    /// <returns>The domain color or the default color.</returns>
    public string GetDomainColor(string domainName)
    {
        if (string.IsNullOrEmpty(domainName))
        {
            return "#666666";
        }

        return _domainColors.GetValueOrDefault(domainName, "#666666");
    }

    /// <summary>
    /// Gets all domain color mappings.
    /// </summary>
    /// <returns>The domain color mappings.</returns>
    public Dictionary<string, string> GetAllDomainColors()
    {
        return _domainColors;
    }

    /// <summary>
    /// Initializes domain color assignments.
    /// </summary>
    /// <param name="domains">The domains.</param>
    private static void InitializeDomainColors(List<DomainInfo> domains)
    {
        _domainColors.Clear();

        if (!domains.Any()) return;

        var colors = GenerateDistinctColors(domains.Count);
        for (var i = 0; i < domains.Count; i++)
        {
            _domainColors[domains[i].Name] = colors[i];
        }
    }

    /// <summary>
    /// Generates visually distinct colors.
    /// </summary>
    /// <param name="count">The number of colors to generate.</param>
    /// <returns>The generated colors.</returns>
    private static List<string> GenerateDistinctColors(int count)
    {
        var colors = new List<string>();
        if (count <= 0) return colors;

        var hueStep = 360.0 / count;

        for (var i = 0; i < count; i++)
        {
            var hue = (i * hueStep) % 360;
            var saturation = 65 + (i % 3) * 15;
            var lightness = 50 + (i % 2) * 10;
            colors.Add($"hsl({hue:F0}, {saturation}%, {lightness}%)");
        }

        return colors;
    }

    /// <summary>
    /// Gets services that belong to or depend on the specified domain.
    /// </summary>
    /// <param name="domainName">The domain name.</param>
    /// <returns>The related services.</returns>
    public async Task<Res<List<RegisteredServiceStatus>>> GetDomainRelatedServicesAsync(string domainName)
    {
        try
        {
            if (string.IsNullOrEmpty(domainName))
            {
                return Res.Fail(localizer["Service:Errors:DomainNameRequired"].Value);
            }

            var servicesResult = await GetMergedServicesStatusAsync();
            if (servicesResult.IsFailed(out var error, out var services))
            {
                return error;
            }

            var domainServices = services
                .Where(s => string.Equals(s.DomainName, domainName, StringComparison.OrdinalIgnoreCase) ||
                           (s.DependentSubDomains?.Contains(domainName, StringComparer.OrdinalIgnoreCase) == true))
                .ToList();

            return domainServices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get related domain services.");
            return Res.Fail(localizer["Service:Errors:GetDomainRelatedServicesFailed", ex.Message].Value);
        }
    }

    /// <summary>
    /// Gets domain details including related services.
    /// </summary>
    /// <param name="domainName">The domain name.</param>
    /// <returns>The domain details.</returns>
    public async Task<Res<DomainDetailInfo>> GetDomainDetailAsync(string domainName)
    {
        try
        {
            if (string.IsNullOrEmpty(domainName))
            {
                return Res.Fail(localizer["Service:Errors:DomainNameRequired"].Value);
            }

            var domainsResult = await GetDomainsWithColorsAsync();
            if (domainsResult.IsFailed(out var error, out var domains))
            {
                return error;
            }

            var domain = domains.FirstOrDefault(d => string.Equals(d.Name, domainName, StringComparison.OrdinalIgnoreCase));
            if (domain == null)
            {
                return Res.Fail(localizer["Service:Errors:DomainNotFound", domainName].Value);
            }

            var servicesResult = await GetDomainRelatedServicesAsync(domainName);
            if (servicesResult.IsFailed(out var servicesError, out var services))
            {
                return servicesError;
            }

            var domainDetail = new DomainDetailInfo
            {
                Domain = domain,
                RelatedServices = services,
                Color = GetDomainColor(domainName)
            };

            return domainDetail;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get domain details.");
            return Res.Fail(localizer["Service:Errors:GetDomainDetailFailed", ex.Message].Value);
        }
    }

    /// <summary>
    /// Gets the current leader state.
    /// </summary>
    /// <returns>The leader state.</returns>
    public async Task<Res<LeaderState?>> GetLeaderStateAsync()
    {
        try
        {
            var stateManager = serviceProvider.GetService<IRegistrationStateManager>();
            if (stateManager == null)
            {
                return Res.Fail(localizer["Service:Errors:StateManagerNotConfigured"].Value);
            }

            return await stateManager.GetLeaderStateAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get leader state.");
            return Res.Fail(localizer["Service:Errors:GetLeaderStateFailed", ex.Message].Value);
        }
    }

    /// <summary>
    /// Forces deletion of the leader key for the specified service.
    /// </summary>
    /// <param name="serviceName">The service name (AppId).</param>
    /// <returns>The operation result.</returns>
    public async Task<Res> ForceDeleteLeaderAsync(string serviceName)
    {
        try
        {
            var stateManager = serviceProvider.GetService<IRegistrationStateManager>();
            if (stateManager == null)
                return Res.Fail(localizer["Service:Errors:StateManagerNotConfigured"].Value);

            await stateManager.ForceDeleteLeaderKeyAsync(serviceName);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to force delete leader for service {ServiceName}.", serviceName);
            return Res.Fail(localizer["Service:Errors:ForceDeleteLeaderFailed", ex.Message].Value);
        }
    }
}

/// <summary>
/// Represents domain details for the Register Centre UI.
/// </summary>
public class DomainDetailInfo
{
    /// <summary>
    /// Gets or sets the domain metadata.
    /// </summary>
    public required DomainInfo Domain { get; set; }

    /// <summary>
    /// Gets or sets the related services.
    /// </summary>
    public List<RegisteredServiceStatus> RelatedServices { get; set; } = [];

    /// <summary>
    /// Gets or sets the domain color.
    /// </summary>
    public string Color { get; set; } = "#666666";
}

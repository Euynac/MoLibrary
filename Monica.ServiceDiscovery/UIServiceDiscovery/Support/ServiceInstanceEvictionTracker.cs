using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.UIServiceDiscovery.Support;

public sealed class ServiceInstanceEvictionTracker(IOptions<ModuleServiceDiscoveryUIOption> uiOptions)
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, InstanceState> _previousInstancesSnapshot = new();
    private readonly Dictionary<string, Queue<EvictedInstanceInfo>> _evictedInstances = new(StringComparer.OrdinalIgnoreCase);

    public List<RegisteredServiceStatus> Apply(List<RegisteredServiceStatus> services)
    {
        lock (_syncRoot)
        {
            DetectAndTrackEvictions(services);
            MergeEvictedInstances(services);
            return services;
        }
    }

    private void DetectAndTrackEvictions(List<RegisteredServiceStatus> currentServices)
    {
        var currentInstances = new Dictionary<string, InstanceState>();

        foreach (var service in currentServices)
        {
            foreach (var instance in service.Instances.Values)
            {
                currentInstances[BuildInstanceKey(instance)] = instance;
            }
        }

        foreach (var (key, previousInstance) in _previousInstancesSnapshot)
        {
            if (currentInstances.ContainsKey(key))
            {
                continue;
            }

            if (!_evictedInstances.TryGetValue(previousInstance.AppId, out var queue))
            {
                queue = new Queue<EvictedInstanceInfo>();
                _evictedInstances[previousInstance.AppId] = queue;
            }

            queue.Enqueue(new EvictedInstanceInfo
            {
                InstanceId = previousInstance.InstanceId,
                AppId = previousInstance.AppId,
                AppName = previousInstance.AppName,
                ProjectName = previousInstance.ProjectName,
                DomainName = previousInstance.DomainName,
                Status = previousInstance.Status,
                EvictionTime = DateTime.UtcNow,
                LastHeartbeatTime = previousInstance.LastHeartbeatTime,
                RegistrationTime = previousInstance.RegistrationTime,
                AssemblyVersion = previousInstance.AssemblyVersion,
                ReleaseVersion = previousInstance.ReleaseVersion,
                BuildTime = previousInstance.BuildTime,
                IsLeader = previousInstance.IsLeader
            });

            var maxCount = Math.Max(0, uiOptions.Value.MaxEvictedServiceRetentionCount);
            while (queue.Count > maxCount)
            {
                queue.Dequeue();
            }
        }

        _previousInstancesSnapshot.Clear();
        foreach (var (key, instance) in currentInstances)
        {
            _previousInstancesSnapshot[key] = instance;
        }
    }

    private void MergeEvictedInstances(List<RegisteredServiceStatus> services)
    {
        foreach (var service in services)
        {
            service.EvictedInstances = _evictedInstances.TryGetValue(service.AppId, out var queue)
                ? queue.ToList()
                : [];
        }
    }

    private static string BuildInstanceKey(InstanceState instance)
        => $"{instance.AppId}:{instance.InstanceId}";
}

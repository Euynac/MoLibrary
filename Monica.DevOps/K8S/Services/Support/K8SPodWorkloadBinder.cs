using Monica.DevOps.K8S.Exceptions;
using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Services.Support;

internal static class K8SPodWorkloadBinder
{
    internal static K8SPodSummary BindPodToWorkload(K8SPodSummary pod, IReadOnlyCollection<K8SWorkloadReference> workloads)
    {
        var workload = workloads.FirstOrDefault(candidate => PodMatchesWorkload(pod, candidate));
        return workload == null ? pod : pod.WithWorkloadBinding(workload);
    }

    internal static List<K8SWorkloadReference> ApplyPodStartFallback(
        IReadOnlyCollection<K8SWorkloadReference> workloads,
        IReadOnlyCollection<K8SPodSummary> pods)
    {
        return workloads
            .Select(workload =>
            {
                var podStartTime = pods
                    .Where(pod => string.Equals(pod.WorkloadKind, workload.Kind, StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(pod.WorkloadName, workload.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(pod => pod.StartTimeUtc)
                    .Where(startTime => startTime.HasValue)
                    .Select(startTime => startTime!.Value)
                    .DefaultIfEmpty()
                    .Max();

                return workload.WithPodStartFallback(podStartTime == default ? null : podStartTime);
            })
            .ToList();
    }

    internal static List<K8SWorkloadReference> ResolveServiceWorkloads(
        K8SServiceSummary service,
        IReadOnlyCollection<K8SWorkloadReference> workloads,
        bool failOnMissing)
    {
        if (service.Selector.Count == 0)
        {
            if (failOnMissing)
            {
                throw K8SOperationException.ServiceSelectorMissing(service.Name);
            }

            return [];
        }

        var matched = workloads
            .Where(workload => SelectorMatches(service.Selector, workload.Labels))
            .OrderBy(workload => workload.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (matched.Count > 0)
        {
            return matched;
        }

        matched = workloads
            .Where(workload => string.Equals(workload.Name, service.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(workload => workload.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (matched.Count > 0)
        {
            return matched;
        }

        if (failOnMissing)
        {
            throw K8SOperationException.ServiceBackingWorkloadsMissing(service.Name);
        }

        return [];
    }

    private static bool PodMatchesWorkload(K8SPodSummary pod, K8SWorkloadReference workload)
    {
        if (string.Equals(pod.OwnerKind, workload.Kind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pod.OwnerName, workload.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(workload.Kind, "Deployment", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pod.OwnerKind, "ReplicaSet", StringComparison.OrdinalIgnoreCase) &&
            pod.OwnerName.StartsWith($"{workload.Name}-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(workload.Kind, "StatefulSet", StringComparison.OrdinalIgnoreCase) &&
            pod.Name.StartsWith($"{workload.Name}-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return workload.Labels.Count > 0 && SelectorMatches(workload.Labels, pod.Labels);
    }

    private static bool SelectorMatches(IReadOnlyDictionary<string, string> selector, IReadOnlyDictionary<string, string> labels)
    {
        return selector.All(pair =>
            labels.TryGetValue(pair.Key, out var value) &&
            string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase));
    }
}

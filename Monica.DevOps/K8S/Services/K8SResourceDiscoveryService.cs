using System.Text.Json;
using Monica.DevOps.K8S.Abstractions;
using Monica.DevOps.K8S.Models;
using Monica.DevOps.K8S.Services.Support;

namespace Monica.DevOps.K8S.Services;

public class K8SResourceDiscoveryService(IK8SProvider provider)
{
    internal static readonly K8SResourceType[] RestartableWorkloadTypes =
    [
        K8SResourceType.Deployment,
        K8SResourceType.StatefulSet,
        K8SResourceType.DaemonSet
    ];

    internal async Task<List<K8SServiceSummary>> ListServiceSummariesAsync(
        K8SRuntimeConfig runtimeConfig,
        string namespaceName,
        CancellationToken cancellationToken)
    {
        using var document = await ExecuteJsonAsync(
            runtimeConfig,
            $"get service -n {KubectlCommandBuilder.ShellQuote(namespaceName)} -o json",
            cancellationToken);

        return document.RootElement.TryGetProperty("items", out var items)
            ? items.EnumerateArray()
                .Select(KubectlJsonReader.ReadServiceSummary)
                .OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];
    }

    internal async Task<List<K8SWorkloadReference>> ListWorkloadsAsync(
        K8SRuntimeConfig runtimeConfig,
        string namespaceName,
        IReadOnlyCollection<K8SResourceType> resourceTypes,
        CancellationToken cancellationToken,
        bool includePodStartFallback = false)
    {
        if (resourceTypes.Count == 0)
        {
            return [];
        }

        var resources = string.Join(",", resourceTypes.Select(resourceType => resourceType.ToKubectlResource()));
        using var document = await ExecuteJsonAsync(
            runtimeConfig,
            $"get {resources} -n {KubectlCommandBuilder.ShellQuote(namespaceName)} -o json",
            cancellationToken);

        var workloads = KubectlJsonReader.ReadWorkloads(document.RootElement);
        if (includePodStartFallback && workloads.Count > 0)
        {
            var pods = await ListPodsForWorkloadsAsync(runtimeConfig, namespaceName, workloads, cancellationToken);
            workloads = K8SPodWorkloadBinder.ApplyPodStartFallback(workloads, pods);
        }

        return workloads
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal async Task<List<K8SPodSummary>> ListPodsForWorkloadsAsync(
        K8SRuntimeConfig runtimeConfig,
        string namespaceName,
        IReadOnlyCollection<K8SWorkloadReference> workloads,
        CancellationToken cancellationToken)
    {
        if (workloads.Count == 0)
        {
            return [];
        }

        using var document = await ExecuteJsonAsync(
            runtimeConfig,
            $"get pod -n {KubectlCommandBuilder.ShellQuote(namespaceName)} -o json",
            cancellationToken);

        return KubectlJsonReader.ReadPods(document.RootElement)
            .Select(pod => K8SPodWorkloadBinder.BindPodToWorkload(pod, workloads))
            .Where(pod => !string.IsNullOrWhiteSpace(pod.WorkloadName))
            .GroupBy(pod => pod.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(pod => pod.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<JsonDocument> ExecuteJsonAsync(
        K8SRuntimeConfig runtimeConfig,
        string kubectlArguments,
        CancellationToken cancellationToken)
    {
        var json = await provider.ExecuteKubectlAsync(runtimeConfig, kubectlArguments, cancellationToken);
        return KubectlJsonReader.ParseDocument(json);
    }
}

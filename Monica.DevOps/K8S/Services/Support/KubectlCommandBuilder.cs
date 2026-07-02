using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Services.Support;

internal static class KubectlCommandBuilder
{
    internal const string PREVIOUS_REPLICAS_ANNOTATION = "monica.devops/previous-replicas";

    public static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'")}'";
    }

    public static List<string> BuildRestartCommands(string namespaceName, IEnumerable<K8SWorkloadReference> workloads)
    {
        return workloads
            .GroupBy(workload => workload.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(workload => workload.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(workload => $"rollout restart {workload.Kind.ToLowerInvariant()}/{ShellQuote(workload.Name)} -n {ShellQuote(namespaceName)}")
            .ToList();
    }

    public static List<string> BuildScaleCommands(
        string namespaceName,
        K8SScaleOperation operation,
        string workloadKind,
        string workloadName,
        int currentReplicas,
        int targetReplicas)
    {
        var qualifiedWorkloadName = $"{workloadKind.ToLowerInvariant()}/{ShellQuote(workloadName)}";
        var namespaceArgument = ShellQuote(namespaceName);
        var scaleCommand = $"scale {qualifiedWorkloadName} --replicas={targetReplicas} -n {namespaceArgument}";

        if (operation == K8SScaleOperation.ScaleUp)
        {
            return [scaleCommand];
        }

        var annotation = ShellQuote($"{PREVIOUS_REPLICAS_ANNOTATION}={currentReplicas}");
        return
        [
            $"annotate {qualifiedWorkloadName} {annotation} --overwrite -n {namespaceArgument}",
            scaleCommand
        ];
    }

    public static List<string> NormalizeRequestedNames(IReadOnlyCollection<string> resourceNames)
    {
        return resourceNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

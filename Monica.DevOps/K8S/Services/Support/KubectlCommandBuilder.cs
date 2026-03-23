using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Services.Support;

internal static class KubectlCommandBuilder
{
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

    public static List<string> NormalizeRequestedNames(IReadOnlyCollection<string> resourceNames)
    {
        return resourceNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

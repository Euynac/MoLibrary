using System.Text;
using Monica.DevOps.K8S.Exceptions;

namespace Monica.DevOps.K8S.Models;

public enum K8SProviderKind
{
    SshRemoteKubectl
}

public enum K8SResourceType
{
    Service,
    Deployment,
    StatefulSet,
    DaemonSet
}

public static class K8SResourceTypeExtensions
{
    public static bool TryParse(string? value, out K8SResourceType resourceType)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "service":
            case "services":
                resourceType = K8SResourceType.Service;
                return true;
            case "deployment":
            case "deployments":
                resourceType = K8SResourceType.Deployment;
                return true;
            case "statefulset":
            case "statefulsets":
                resourceType = K8SResourceType.StatefulSet;
                return true;
            case "daemonset":
            case "daemonsets":
                resourceType = K8SResourceType.DaemonSet;
                return true;
            default:
                return Enum.TryParse(value, ignoreCase: true, out resourceType);
        }
    }

    public static string ToDisplayName(this K8SResourceType resourceType)
    {
        return resourceType switch
        {
            K8SResourceType.Service => "Service",
            K8SResourceType.Deployment => "Deployment",
            K8SResourceType.StatefulSet => "StatefulSet",
            K8SResourceType.DaemonSet => "DaemonSet",
            _ => resourceType.ToString()
        };
    }

    public static string ToPluralDisplayName(this K8SResourceType resourceType)
    {
        return resourceType switch
        {
            K8SResourceType.Service => "Services",
            K8SResourceType.Deployment => "Deployments",
            K8SResourceType.StatefulSet => "StatefulSets",
            K8SResourceType.DaemonSet => "DaemonSets",
            _ => resourceType.ToString()
        };
    }

    public static string ToKubectlResource(this K8SResourceType resourceType)
    {
        return resourceType switch
        {
            K8SResourceType.Service => "service",
            K8SResourceType.Deployment => "deployment",
            K8SResourceType.StatefulSet => "statefulset",
            K8SResourceType.DaemonSet => "daemonset",
            _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, null)
        };
    }
}

public class K8SRuntimeConfig
{
    public K8SProviderKind Provider { get; set; } = K8SProviderKind.SshRemoteKubectl;

    public List<string> MasterNodes { get; set; } = [];

    public List<string> WorkerNodes { get; set; } = [];

    public string ExecutionNode { get; set; } = string.Empty;

    public string UserName { get; set; } = "root";

    public string Password { get; set; } = string.Empty;

    public List<string> NamespaceScope { get; set; } = [];

    public List<string> SensitiveOperationNameRestrictions { get; set; } = [];

    public K8SRuntimeConfig Clone()
    {
        return new K8SRuntimeConfig
        {
            Provider = Provider,
            MasterNodes = [.. MasterNodes],
            WorkerNodes = [.. WorkerNodes],
            ExecutionNode = ExecutionNode,
            UserName = UserName,
            Password = Password,
            NamespaceScope = [.. NamespaceScope],
            SensitiveOperationNameRestrictions = [.. SensitiveOperationNameRestrictions]
        };
    }

    public K8SRuntimeConfig Normalize()
    {
        MasterNodes = NormalizeList(MasterNodes);
        WorkerNodes = NormalizeList(WorkerNodes);
        NamespaceScope = NormalizeList(NamespaceScope);
        SensitiveOperationNameRestrictions = NormalizeList(SensitiveOperationNameRestrictions);
        ExecutionNode = ExecutionNode.Trim();
        UserName = UserName.Trim();

        if (string.IsNullOrWhiteSpace(ExecutionNode))
        {
            ExecutionNode = MasterNodes.FirstOrDefault() ?? WorkerNodes.FirstOrDefault() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            UserName = "root";
        }

        return this;
    }

    public void ValidateForConnection()
    {
        if (string.IsNullOrWhiteSpace(ExecutionNode))
        {
            throw K8SOperationException.ExecutionNodeRequired();
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            throw K8SOperationException.SshUserNameRequired();
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw K8SOperationException.SshPasswordRequired();
        }
    }

    public void EnsureNamespaceAllowed(string namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            throw K8SOperationException.NamespaceRequired();
        }

        if (NamespaceScope.Count == 0)
        {
            return;
        }

        if (NamespaceScope.Contains(namespaceName, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw K8SOperationException.NamespaceOutsideScope(namespaceName);
    }

    public bool IsSensitiveOperationAllowed(string candidateName, out List<string> matchedKeywords)
    {
        matchedKeywords = [];

        if (SensitiveOperationNameRestrictions.Count == 0)
        {
            return true;
        }

        matchedKeywords = SensitiveOperationNameRestrictions
            .Where(keyword => candidateName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return matchedKeywords.Count > 0;
    }

    public static List<string> ParseMultiValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        return rawValue
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string ToMultilineText(IEnumerable<string> values)
    {
        return string.Join(Environment.NewLine, NormalizeList(values));
    }

    private static List<string> NormalizeList(IEnumerable<string>? values)
    {
        var combined = values == null
            ? string.Empty
            : string.Join(Environment.NewLine, values);

        return ParseMultiValue(combined);
    }
}

public class K8SNamespaceInfo
{
    public string Name { get; init; } = string.Empty;

    public bool Exists { get; init; } = true;

    public bool IsInScope { get; init; }

    public string DisplayName => Exists ? Name : $"{Name} (missing)";
}

public class K8SServicePort
{
    public string Name { get; init; } = string.Empty;

    public string Protocol { get; init; } = "TCP";

    public int Port { get; init; }

    public string TargetPort { get; init; } = "-";

    public int? NodePort { get; init; }

    public string DisplayText
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(Name) ? Protocol : $"{Name}/{Protocol}";
            var builder = new StringBuilder($"{label}: {Port}->{TargetPort}");

            if (NodePort is { } nodePort)
            {
                builder.Append($" (node {nodePort})");
            }

            return builder.ToString();
        }
    }
}

public class K8SServiceSummary
{
    public string Name { get; init; } = string.Empty;

    public string Namespace { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string ClusterIp { get; init; } = string.Empty;

    public Dictionary<string, string> Selector { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public List<K8SServicePort> Ports { get; init; } = [];

    public string SelectorDisplay => Selector.Count == 0
        ? "-"
        : string.Join(", ", Selector.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}"));

    public string PortsDisplay => Ports.Count == 0
        ? "-"
        : string.Join(", ", Ports.Select(port => port.DisplayText));
}

public class K8SResourceSummary
{
    public K8SResourceType ResourceType { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Namespace { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string ClusterIp { get; init; } = string.Empty;

    public Dictionary<string, string> Selector { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public List<K8SServicePort> Ports { get; init; } = [];

    public List<string> Images { get; init; } = [];

    public int DesiredReplicas { get; init; }

    public int ReadyReplicas { get; init; }

    public DateTimeOffset? LastRestartedAtUtc { get; init; }

    public bool RestartSupported { get; init; }

    public string SelectorDisplay => Selector.Count == 0
        ? "-"
        : string.Join(", ", Selector.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => $"{pair.Key}={pair.Value}"));

    public string PortsDisplay => Ports.Count == 0
        ? "-"
        : string.Join(", ", Ports.Select(port => port.DisplayText));

    public string ImagesDisplay => Images.Count == 0
        ? "-"
        : string.Join(", ", Images);

    public string ReplicaStatus => DesiredReplicas > 0 || ReadyReplicas > 0 ? $"{ReadyReplicas}/{DesiredReplicas}" : "-";

    public string LastRestartedAtDisplay => LastRestartedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public string SearchText
    {
        get
        {
            var tokens =
                new List<string> { Name, Namespace, Kind, Type, ClusterIp, SelectorDisplay, PortsDisplay, ImagesDisplay, ReplicaStatus, LastRestartedAtDisplay };

            tokens.AddRange(Selector.Select(pair => $"{pair.Key} {pair.Value}"));
            tokens.AddRange(Ports.Select(port => port.DisplayText));
            tokens.AddRange(Images);

            return string.Join(" ", tokens.Where(token => !string.IsNullOrWhiteSpace(token)));
        }
    }
}

public class K8SWorkloadReference
{
    public string Kind { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Namespace { get; init; } = string.Empty;

    public int DesiredReplicas { get; init; }

    public int ReadyReplicas { get; init; }

    public DateTimeOffset? LastRestartedAtUtc { get; init; }

    public Dictionary<string, string> Labels { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Images { get; init; } = [];

    public string DisplayName => $"{Kind}/{Name}";

    public string ReplicaStatus => DesiredReplicas > 0 || ReadyReplicas > 0 ? $"{ReadyReplicas}/{DesiredReplicas}" : "-";

    public string LastRestartedAtDisplay => LastRestartedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public K8SWorkloadReference WithPodStartFallback(DateTimeOffset? podStartTime)
    {
        if (LastRestartedAtUtc.HasValue || !podStartTime.HasValue)
        {
            return this;
        }

        return new K8SWorkloadReference
        {
            Kind = Kind,
            Name = Name,
            Namespace = Namespace,
            DesiredReplicas = DesiredReplicas,
            ReadyReplicas = ReadyReplicas,
            LastRestartedAtUtc = podStartTime,
            Labels = Labels,
            Images = Images
        };
    }
}

public class K8SPodSummary
{
    public string Name { get; init; } = string.Empty;

    public string Namespace { get; init; } = string.Empty;

    public string Phase { get; init; } = string.Empty;

    public int ReadyContainers { get; init; }

    public int TotalContainers { get; init; }

    public int RestartCount { get; init; }

    public string NodeName { get; init; } = string.Empty;

    public string PodIp { get; init; } = string.Empty;

    public string OwnerKind { get; init; } = string.Empty;

    public string OwnerName { get; init; } = string.Empty;

    public string WorkloadKind { get; init; } = string.Empty;

    public string WorkloadName { get; init; } = string.Empty;

    public DateTimeOffset? StartTimeUtc { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public bool IsReady { get; init; }

    public Dictionary<string, string> Labels { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string PhaseDisplay => string.IsNullOrWhiteSpace(Phase) ? "-" : Phase;

    public string ReadyDisplay => TotalContainers > 0 ? $"{ReadyContainers}/{TotalContainers}" : "-";

    public string StartTimeDisplay => StartTimeUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    public string WorkloadDisplayName => !string.IsNullOrWhiteSpace(WorkloadKind) && !string.IsNullOrWhiteSpace(WorkloadName)
        ? $"{WorkloadKind}/{WorkloadName}"
        : !string.IsNullOrWhiteSpace(OwnerKind) && !string.IsNullOrWhiteSpace(OwnerName)
            ? $"{OwnerKind}/{OwnerName}"
            : "-";

    public bool HasIssue => !IsReady ||
                            !string.Equals(Phase, "Running", StringComparison.OrdinalIgnoreCase) ||
                            !string.IsNullOrWhiteSpace(Reason) ||
                            !string.IsNullOrWhiteSpace(Message);

    public string IssueSummary
    {
        get
        {
            var segments = new List<string>();

            if (!string.IsNullOrWhiteSpace(Reason))
            {
                segments.Add(Reason);
            }

            if (!string.IsNullOrWhiteSpace(Message))
            {
                segments.Add(Message);
            }

            return segments.Count == 0 ? string.Empty : string.Join(" | ", segments);
        }
    }

    public K8SPodSummary WithWorkloadBinding(K8SWorkloadReference workload)
    {
        ArgumentNullException.ThrowIfNull(workload);

        return new K8SPodSummary
        {
            Name = Name,
            Namespace = Namespace,
            Phase = Phase,
            ReadyContainers = ReadyContainers,
            TotalContainers = TotalContainers,
            RestartCount = RestartCount,
            NodeName = NodeName,
            PodIp = PodIp,
            OwnerKind = OwnerKind,
            OwnerName = OwnerName,
            WorkloadKind = workload.Kind,
            WorkloadName = workload.Name,
            StartTimeUtc = StartTimeUtc,
            Reason = Reason,
            Message = Message,
            IsReady = IsReady,
            Labels = Labels
        };
    }
}

public class K8SPodDetails
{
    public K8SPodSummary Summary { get; init; } = new();

    public string DescriptionText { get; init; } = string.Empty;
}

public class K8SServiceDetails
{
    public K8SServiceSummary Summary { get; init; } = new();

    public string DescriptionText { get; init; } = string.Empty;

    public List<K8SWorkloadReference> BackingWorkloads { get; init; } = [];

    public List<K8SPodSummary> RelatedPods { get; init; } = [];
}

public class K8SResourceDetails
{
    public K8SResourceSummary Summary { get; init; } = new();

    public string DescriptionText { get; init; } = string.Empty;

    public List<K8SWorkloadReference> RelatedWorkloads { get; init; } = [];

    public List<K8SPodSummary> RelatedPods { get; init; } = [];
}

public class K8SResourceListResult
{
    public string Namespace { get; init; } = string.Empty;

    public K8SResourceType ResourceType { get; init; }

    public List<K8SResourceSummary> Items { get; init; } = [];

    public DateTimeOffset FetchedAtUtc { get; init; }
}

public class K8SRestartTarget
{
    public K8SResourceType ResourceType { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Namespace { get; init; } = string.Empty;

    public string DisplayName => $"{Kind}/{Name}";
}

public class K8SRestartIgnoredTarget : K8SRestartTarget
{
    public K8SMessageCode? ReasonCode { get; init; }

    public List<string> ReasonArguments { get; init; } = [];

    public string Reason { get; internal set; } = string.Empty;

    public string DisplayText => $"{DisplayName}: {Reason}";

    public void ApplyLocalizedReason(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Reason = reason;
    }
}

public class K8SRestartPreview
{
    public string Namespace { get; init; } = string.Empty;

    public string ScopeName { get; init; } = string.Empty;

    public K8SResourceType? ResourceType { get; init; }

    public DateTimeOffset GeneratedAtUtc { get; init; }

    public List<string> RestrictionKeywords { get; init; } = [];

    public List<K8SRestartTarget> RequestedTargets { get; init; } = [];

    public List<K8SRestartTarget> EligibleTargets { get; init; } = [];

    public List<K8SRestartIgnoredTarget> IgnoredTargets { get; init; } = [];

    public List<K8SWorkloadReference> Workloads { get; init; } = [];

    public List<string> CommandPreview { get; init; } = [];

    public bool HasRestartableTargets => Workloads.Count > 0;

    public K8SRestartPreview LocalizeIgnoredTargets(Func<K8SRestartIgnoredTarget, string> reasonFactory)
    {
        ArgumentNullException.ThrowIfNull(reasonFactory);

        foreach (var ignoredTarget in IgnoredTargets)
        {
            ignoredTarget.ApplyLocalizedReason(reasonFactory(ignoredTarget));
        }

        return this;
    }
}

public class K8SRestartResult
{
    public string Namespace { get; init; } = string.Empty;

    public string ScopeName { get; init; } = string.Empty;

    public K8SResourceType? ResourceType { get; init; }

    public List<string> AffectedTargets { get; init; } = [];

    public List<K8SWorkloadReference> Workloads { get; init; } = [];

    public List<string> ExecutedCommands { get; init; } = [];

    public List<K8SRestartIgnoredTarget> IgnoredTargets { get; init; } = [];

    public DateTimeOffset CompletedAtUtc { get; init; }

    public K8SRestartResult LocalizeIgnoredTargets(Func<K8SRestartIgnoredTarget, string> reasonFactory)
    {
        ArgumentNullException.ThrowIfNull(reasonFactory);

        foreach (var ignoredTarget in IgnoredTargets)
        {
            ignoredTarget.ApplyLocalizedReason(reasonFactory(ignoredTarget));
        }

        return this;
    }
}

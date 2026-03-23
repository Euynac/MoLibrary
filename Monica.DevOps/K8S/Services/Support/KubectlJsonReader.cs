using System.Text.Json;
using Monica.DevOps.K8S.Exceptions;
using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Services.Support;

internal static class KubectlJsonReader
{
    internal static JsonDocument ParseDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw K8SOperationException.KubectlJsonParseFailed(ex);
        }
    }

    internal static K8SServiceSummary ReadServiceSummary(JsonElement element)
    {
        var metadata = element.GetProperty("metadata");
        var spec = element.GetProperty("spec");

        return new K8SServiceSummary
        {
            Name = metadata.GetProperty("name").GetString() ?? string.Empty,
            Namespace = metadata.TryGetProperty("namespace", out var namespaceElement) ? namespaceElement.GetString() ?? string.Empty : string.Empty,
            Type = spec.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "ClusterIP" : "ClusterIP",
            ClusterIp = spec.TryGetProperty("clusterIP", out var clusterIpElement) ? clusterIpElement.GetString() ?? "-" : "-",
            Selector = ReadStringMap(spec, "selector"),
            Ports = ReadPorts(spec)
        };
    }

    internal static List<K8SWorkloadReference> ReadWorkloads(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(item =>
            {
                var metadata = item.GetProperty("metadata");
                var status = item.TryGetProperty("status", out var statusElement) ? statusElement : default;
                var spec = item.TryGetProperty("spec", out var specElement) ? specElement : default;
                var kind = item.TryGetProperty("kind", out var kindElement) ? kindElement.GetString() ?? string.Empty : string.Empty;

                return new K8SWorkloadReference
                {
                    Kind = kind,
                    Name = metadata.GetProperty("name").GetString() ?? string.Empty,
                    Namespace = metadata.TryGetProperty("namespace", out var namespaceElement) ? namespaceElement.GetString() ?? string.Empty : string.Empty,
                    DesiredReplicas = ReadDesiredReplicas(kind, spec, status),
                    ReadyReplicas = ReadReadyReplicas(kind, status),
                    LastRestartedAtUtc = ReadLastRestartedAt(spec, metadata),
                    Labels = ReadTemplateLabels(spec, metadata),
                    Images = ReadTemplateImages(spec)
                };
            })
            .Where(workload => !string.IsNullOrWhiteSpace(workload.Kind) && !string.IsNullOrWhiteSpace(workload.Name))
            .ToList();
    }

    internal static List<K8SPodSummary> ReadPods(JsonElement rootElement)
    {
        if (!rootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(ReadPodSummary)
            .Where(pod => !string.IsNullOrWhiteSpace(pod.Name))
            .ToList();
    }

    internal static K8SPodSummary ReadPodSummary(JsonElement item)
    {
        var metadata = item.GetProperty("metadata");
        var spec = item.TryGetProperty("spec", out var specElement) ? specElement : default;
        var status = item.TryGetProperty("status", out var statusElement) ? statusElement : default;
        var labels = ReadStringMap(metadata, "labels");
        var (ownerKind, ownerName) = ReadControllerOwnerReference(metadata);
        var readyContainers = ReadReadyContainerCount(status);
        var totalContainers = ReadContainerCount(status, "containerStatuses");
        var restartCount = ReadRestartCount(status);
        var phase = status.TryGetProperty("phase", out var phaseElement) ? phaseElement.GetString() ?? string.Empty : string.Empty;
        var isReady = ReadPodReady(status, readyContainers, totalContainers, phase);
        var (reason, message) = ReadPodIssue(status);

        return new K8SPodSummary
        {
            Name = metadata.GetProperty("name").GetString() ?? string.Empty,
            Namespace = metadata.TryGetProperty("namespace", out var namespaceElement) ? namespaceElement.GetString() ?? string.Empty : string.Empty,
            Phase = phase,
            ReadyContainers = readyContainers,
            TotalContainers = totalContainers,
            RestartCount = restartCount,
            NodeName = spec.TryGetProperty("nodeName", out var nodeNameElement) ? nodeNameElement.GetString() ?? string.Empty : string.Empty,
            PodIp = status.TryGetProperty("podIP", out var podIpElement) ? podIpElement.GetString() ?? string.Empty : string.Empty,
            OwnerKind = ownerKind,
            OwnerName = ownerName,
            StartTimeUtc = ParseDateTimeOffsetOrNull(status.TryGetProperty("startTime", out var startTimeElement) ? startTimeElement.GetString() : null),
            Reason = reason,
            Message = message,
            IsReady = isReady,
            Labels = labels
        };
    }

    private static Dictionary<string, string> ReadStringMap(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in property.EnumerateObject())
        {
            values[child.Name] = child.Value.GetString() ?? string.Empty;
        }

        return values;
    }

    private static List<K8SServicePort> ReadPorts(JsonElement spec)
    {
        if (!spec.TryGetProperty("ports", out var portsElement) || portsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return portsElement
            .EnumerateArray()
            .Select(portElement => new K8SServicePort
            {
                Name = portElement.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty,
                Protocol = portElement.TryGetProperty("protocol", out var protocolElement) ? protocolElement.GetString() ?? "TCP" : "TCP",
                Port = portElement.TryGetProperty("port", out var portValue) ? portValue.GetInt32() : 0,
                TargetPort = ReadTargetPort(portElement),
                NodePort = portElement.TryGetProperty("nodePort", out var nodePortElement) ? nodePortElement.GetInt32() : null
            })
            .ToList();
    }

    private static string ReadTargetPort(JsonElement portElement)
    {
        if (!portElement.TryGetProperty("targetPort", out var targetPortElement))
        {
            return "-";
        }

        return targetPortElement.ValueKind switch
        {
            JsonValueKind.Number => targetPortElement.GetInt32().ToString(),
            JsonValueKind.String => targetPortElement.GetString() ?? "-",
            _ => "-"
        };
    }

    private static Dictionary<string, string> ReadTemplateLabels(JsonElement spec, JsonElement metadata)
    {
        if (spec.ValueKind == JsonValueKind.Object &&
            spec.TryGetProperty("template", out var template) &&
            template.ValueKind == JsonValueKind.Object &&
            template.TryGetProperty("metadata", out var templateMetadata))
        {
            var templateLabels = ReadStringMap(templateMetadata, "labels");
            if (templateLabels.Count > 0)
            {
                return templateLabels;
            }
        }

        return ReadStringMap(metadata, "labels");
    }

    private static List<string> ReadTemplateImages(JsonElement spec)
    {
        if (spec.ValueKind != JsonValueKind.Object ||
            !spec.TryGetProperty("template", out var template) ||
            template.ValueKind != JsonValueKind.Object ||
            !template.TryGetProperty("spec", out var templateSpec) ||
            templateSpec.ValueKind != JsonValueKind.Object ||
            !templateSpec.TryGetProperty("containers", out var containers) ||
            containers.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return containers.EnumerateArray()
            .Select(container => container.TryGetProperty("image", out var imageElement) ? imageElement.GetString() ?? string.Empty : string.Empty)
            .Where(image => !string.IsNullOrWhiteSpace(image))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ReadDesiredReplicas(string kind, JsonElement spec, JsonElement status)
    {
        return kind switch
        {
            "DaemonSet" when status.ValueKind == JsonValueKind.Object && status.TryGetProperty("desiredNumberScheduled", out var desiredScheduled) => desiredScheduled.GetInt32(),
            _ when spec.ValueKind == JsonValueKind.Object && spec.TryGetProperty("replicas", out var replicas) => replicas.GetInt32(),
            _ => 0
        };
    }

    private static int ReadReadyReplicas(string kind, JsonElement status)
    {
        if (status.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return kind switch
        {
            "DaemonSet" when status.TryGetProperty("numberReady", out var numberReady) => numberReady.GetInt32(),
            _ when status.TryGetProperty("readyReplicas", out var readyReplicas) => readyReplicas.GetInt32(),
            _ => 0
        };
    }

    private static (string ownerKind, string ownerName) ReadControllerOwnerReference(JsonElement metadata)
    {
        if (!metadata.TryGetProperty("ownerReferences", out var ownerReferences) || ownerReferences.ValueKind != JsonValueKind.Array)
        {
            return (string.Empty, string.Empty);
        }

        JsonElement? fallback = null;
        foreach (var ownerReference in ownerReferences.EnumerateArray())
        {
            fallback ??= ownerReference;
            if (ownerReference.TryGetProperty("controller", out var controllerElement) &&
                controllerElement.ValueKind == JsonValueKind.True)
            {
                return ReadOwnerReference(ownerReference);
            }
        }

        return fallback.HasValue ? ReadOwnerReference(fallback.Value) : (string.Empty, string.Empty);
    }

    private static (string ownerKind, string ownerName) ReadOwnerReference(JsonElement ownerReference)
    {
        var ownerKind = ownerReference.TryGetProperty("kind", out var kindElement) ? kindElement.GetString() ?? string.Empty : string.Empty;
        var ownerName = ownerReference.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
        return (ownerKind, ownerName);
    }

    private static int ReadReadyContainerCount(JsonElement status)
    {
        if (status.ValueKind != JsonValueKind.Object ||
            !status.TryGetProperty("containerStatuses", out var containerStatuses) ||
            containerStatuses.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return containerStatuses.EnumerateArray()
            .Count(containerStatus => containerStatus.TryGetProperty("ready", out var readyElement) && readyElement.ValueKind == JsonValueKind.True);
    }

    private static int ReadContainerCount(JsonElement status, string propertyName)
    {
        return status.ValueKind == JsonValueKind.Object &&
               status.TryGetProperty(propertyName, out var containers) &&
               containers.ValueKind == JsonValueKind.Array
            ? containers.GetArrayLength()
            : 0;
    }

    private static int ReadRestartCount(JsonElement status)
    {
        return ReadRestartCount(status, "initContainerStatuses") + ReadRestartCount(status, "containerStatuses");
    }

    private static int ReadRestartCount(JsonElement status, string propertyName)
    {
        if (status.ValueKind != JsonValueKind.Object ||
            !status.TryGetProperty(propertyName, out var containerStatuses) ||
            containerStatuses.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return containerStatuses.EnumerateArray()
            .Where(containerStatus => containerStatus.TryGetProperty("restartCount", out _))
            .Sum(containerStatus => containerStatus.GetProperty("restartCount").GetInt32());
    }

    private static bool ReadPodReady(JsonElement status, int readyContainers, int totalContainers, string phase)
    {
        if (status.ValueKind == JsonValueKind.Object &&
            status.TryGetProperty("conditions", out var conditions) &&
            conditions.ValueKind == JsonValueKind.Array)
        {
            foreach (var condition in conditions.EnumerateArray())
            {
                if (!condition.TryGetProperty("type", out var typeElement) ||
                    !string.Equals(typeElement.GetString(), "Ready", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return condition.TryGetProperty("status", out var statusElement) &&
                       string.Equals(statusElement.GetString(), "True", StringComparison.OrdinalIgnoreCase);
            }
        }

        return totalContainers > 0 &&
               readyContainers == totalContainers &&
               string.Equals(phase, "Running", StringComparison.OrdinalIgnoreCase);
    }

    private static (string reason, string message) ReadPodIssue(JsonElement status)
    {
        if (TryReadContainerIssue(status, "initContainerStatuses", out var initReason, out var initMessage))
        {
            return (initReason, initMessage);
        }

        if (TryReadContainerIssue(status, "containerStatuses", out var containerReason, out var containerMessage))
        {
            return (containerReason, containerMessage);
        }

        if (TryReadConditionIssue(status, "Ready", out var readyReason, out var readyMessage))
        {
            return (readyReason, readyMessage);
        }

        if (TryReadConditionIssue(status, "ContainersReady", out var containersReadyReason, out var containersReadyMessage))
        {
            return (containersReadyReason, containersReadyMessage);
        }

        var reason = status.ValueKind == JsonValueKind.Object && status.TryGetProperty("reason", out var reasonElement)
            ? reasonElement.GetString() ?? string.Empty
            : string.Empty;
        var message = status.ValueKind == JsonValueKind.Object && status.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString() ?? string.Empty
            : string.Empty;

        return (reason, message);
    }

    private static bool TryReadContainerIssue(JsonElement status, string propertyName, out string reason, out string message)
    {
        reason = string.Empty;
        message = string.Empty;

        if (status.ValueKind != JsonValueKind.Object ||
            !status.TryGetProperty(propertyName, out var containerStatuses) ||
            containerStatuses.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var containerStatus in containerStatuses.EnumerateArray())
        {
            if (!containerStatus.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var propertyNameCandidate in new[] { "waiting", "terminated" })
            {
                if (!state.TryGetProperty(propertyNameCandidate, out var containerState) || containerState.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                reason = containerState.TryGetProperty("reason", out var reasonElement) ? reasonElement.GetString() ?? string.Empty : string.Empty;
                message = containerState.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? string.Empty : string.Empty;

                if (string.Equals(propertyNameCandidate, "terminated", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reason, "Completed", StringComparison.OrdinalIgnoreCase) &&
                    (!containerState.TryGetProperty("exitCode", out var exitCodeElement) || exitCodeElement.GetInt32() == 0))
                {
                    reason = string.Empty;
                    message = string.Empty;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(reason) && string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private static bool TryReadConditionIssue(JsonElement status, string conditionType, out string reason, out string message)
    {
        reason = string.Empty;
        message = string.Empty;

        if (status.ValueKind != JsonValueKind.Object ||
            !status.TryGetProperty("conditions", out var conditions) ||
            conditions.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var condition in conditions.EnumerateArray())
        {
            if (!condition.TryGetProperty("type", out var typeElement) ||
                !string.Equals(typeElement.GetString(), conditionType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var conditionStatus = condition.TryGetProperty("status", out var statusElement) ? statusElement.GetString() ?? string.Empty : string.Empty;
            if (string.Equals(conditionStatus, "True", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            reason = condition.TryGetProperty("reason", out var reasonElement) ? reasonElement.GetString() ?? string.Empty : string.Empty;
            message = condition.TryGetProperty("message", out var messageElement) ? messageElement.GetString() ?? string.Empty : string.Empty;

            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = conditionType;
            }

            return true;
        }

        return false;
    }

    private static DateTimeOffset? ReadLastRestartedAt(JsonElement spec, JsonElement metadata)
    {
        var annotationValues = new List<string>();

        if (spec.ValueKind == JsonValueKind.Object &&
            spec.TryGetProperty("template", out var template) &&
            template.ValueKind == JsonValueKind.Object &&
            template.TryGetProperty("metadata", out var templateMetadata))
        {
            CollectRestartAnnotations(templateMetadata, annotationValues);
        }

        CollectRestartAnnotations(metadata, annotationValues);
        return annotationValues
            .Select(ParseDateTimeOffsetOrNull)
            .Where(value => value.HasValue)
            .OrderByDescending(value => value!.Value)
            .FirstOrDefault();
    }

    private static void CollectRestartAnnotations(JsonElement metadata, List<string> values)
    {
        var annotations = ReadStringMap(metadata, "annotations");
        foreach (var key in new[] { "kubectl.kubernetes.io/restartedAt", "kubesphere.io/restartedAt" })
        {
            if (annotations.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }
    }

    private static DateTimeOffset? ParseDateTimeOffsetOrNull(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsedValue)
            ? parsedValue
            : null;
    }
}

using Monica.DevOps.K8S.Abstractions;
using Monica.DevOps.K8S.Exceptions;
using Monica.DevOps.K8S.Models;
using Monica.DevOps.K8S.Services.Support;

namespace Monica.DevOps.K8S.Services;

public class K8SScaleService(
    K8SResourceDiscoveryService discoveryService,
    IK8SRuntimeConfigStore runtimeConfigStore,
    IK8SProvider provider)
{
    private static readonly K8SResourceType[] DiscoverableWorkloadTypes =
    [
        K8SResourceType.Deployment,
        K8SResourceType.StatefulSet,
        K8SResourceType.DaemonSet
    ];

    public Task<K8SScalePreview> GetResourceScalePreviewAsync(
        string namespaceName,
        K8SResourceType resourceType,
        string resourceName,
        K8SScaleOperation operation,
        CancellationToken cancellationToken = default)
    {
        return GetBatchScalePreviewAsync(namespaceName, resourceType, [resourceName], operation, cancellationToken);
    }

    public async Task<K8SScalePreview> GetBatchScalePreviewAsync(
        string namespaceName,
        K8SResourceType resourceType,
        IReadOnlyCollection<string> resourceNames,
        K8SScaleOperation operation,
        CancellationToken cancellationToken = default)
    {
        var runtimeConfig = runtimeConfigStore.GetCurrent();
        runtimeConfig.EnsureNamespaceAllowed(namespaceName);

        var normalizedNames = KubectlCommandBuilder.NormalizeRequestedNames(resourceNames);
        if (normalizedNames.Count == 0)
        {
            throw K8SOperationException.ResourceNamesRequired();
        }

        return resourceType == K8SResourceType.Service
            ? await BuildServiceScalePreviewAsync(runtimeConfig, namespaceName, normalizedNames, operation, "Selected Services (Backing Workloads)", cancellationToken)
            : await BuildWorkloadScalePreviewAsync(runtimeConfig, namespaceName, resourceType, normalizedNames, operation, cancellationToken);
    }

    public async Task<K8SScalePreview> GetNamespaceScalePreviewAsync(
        string namespaceName,
        K8SScaleOperation operation,
        CancellationToken cancellationToken = default)
    {
        var runtimeConfig = runtimeConfigStore.GetCurrent();
        runtimeConfig.EnsureNamespaceAllowed(namespaceName);

        var serviceNames = (await discoveryService.ListServiceSummariesAsync(runtimeConfig, namespaceName, cancellationToken))
            .Select(service => service.Name)
            .ToList();

        return await BuildServiceScalePreviewAsync(
            runtimeConfig,
            namespaceName,
            serviceNames,
            operation,
            "Namespace Services (Backing Workloads)",
            cancellationToken);
    }

    public async Task<K8SScaleResult> ScaleResourcesAsync(
        string namespaceName,
        K8SResourceType resourceType,
        IReadOnlyCollection<string> resourceNames,
        K8SScaleOperation operation,
        CancellationToken cancellationToken = default)
    {
        var preview = await GetBatchScalePreviewAsync(namespaceName, resourceType, resourceNames, operation, cancellationToken);
        return await ExecuteScalePreviewAsync(preview, cancellationToken);
    }

    public async Task<K8SScaleResult> ScaleNamespaceAsync(
        string namespaceName,
        K8SScaleOperation operation,
        CancellationToken cancellationToken = default)
    {
        var preview = await GetNamespaceScalePreviewAsync(namespaceName, operation, cancellationToken);
        return await ExecuteScalePreviewAsync(preview, cancellationToken);
    }

    private async Task<K8SScalePreview> BuildServiceScalePreviewAsync(
        K8SRuntimeConfig runtimeConfig,
        string namespaceName,
        IReadOnlyCollection<string> serviceNames,
        K8SScaleOperation operation,
        string scopeName,
        CancellationToken cancellationToken)
    {
        runtimeConfig.EnsureNamespaceAllowed(namespaceName);

        var serviceLookup = (await discoveryService.ListServiceSummariesAsync(runtimeConfig, namespaceName, cancellationToken))
            .ToDictionary(service => service.Name, StringComparer.OrdinalIgnoreCase);
        var workloads = await discoveryService.ListWorkloadsAsync(runtimeConfig, namespaceName, DiscoverableWorkloadTypes, cancellationToken);

        var requestedTargets = new List<K8SScaleTarget>();
        var eligibleTargets = new List<K8SScaleTarget>();
        var ignoredTargets = new List<K8SScaleIgnoredTarget>();
        var affectedWorkloads = new Dictionary<string, K8SScaleWorkloadPlan>(StringComparer.OrdinalIgnoreCase);

        foreach (var serviceName in serviceNames)
        {
            requestedTargets.Add(CreateTarget(K8SResourceType.Service, K8SResourceType.Service.ToDisplayName(), serviceName, namespaceName));

            if (!serviceLookup.TryGetValue(serviceName, out var service))
            {
                ignoredTargets.Add(CreateIgnoredTarget(
                    K8SResourceType.Service,
                    K8SResourceType.Service.ToDisplayName(),
                    serviceName,
                    namespaceName,
                    K8SOperationException.ServiceNotFound(serviceName, namespaceName)));
                continue;
            }

            if (!runtimeConfig.IsSensitiveOperationAllowed(service.Name, out _))
            {
                ignoredTargets.Add(CreateIgnoredTarget(
                    K8SResourceType.Service,
                    K8SResourceType.Service.ToDisplayName(),
                    service.Name,
                    namespaceName,
                    K8SOperationException.SensitiveRestrictionMismatch(runtimeConfig.SensitiveOperationNameRestrictions)));
                continue;
            }

            List<K8SWorkloadReference> serviceWorkloads;
            try
            {
                serviceWorkloads = K8SPodWorkloadBinder.ResolveServiceWorkloads(service, workloads, failOnMissing: true);
            }
            catch (K8SOperationException ex)
            {
                ignoredTargets.Add(CreateIgnoredTarget(K8SResourceType.Service, K8SResourceType.Service.ToDisplayName(), service.Name, namespaceName, ex));
                continue;
            }

            var hasScalablePlan = false;
            foreach (var workload in serviceWorkloads)
            {
                if (!TryCreateScalePlan(workload, operation, namespaceName, out var plan, out _))
                {
                    continue;
                }

                affectedWorkloads.TryAdd(plan.DisplayName, plan);
                hasScalablePlan = true;
            }

            if (hasScalablePlan)
            {
                eligibleTargets.Add(CreateTarget(K8SResourceType.Service, K8SResourceType.Service.ToDisplayName(), service.Name, namespaceName));
                continue;
            }

            ignoredTargets.Add(CreateIgnoredTarget(
                K8SResourceType.Service,
                K8SResourceType.Service.ToDisplayName(),
                service.Name,
                namespaceName,
                K8SOperationException.NoScalableTargetsRemain()));
        }

        return BuildScalePreview(
            namespaceName,
            scopeName,
            K8SResourceType.Service,
            operation,
            requestedTargets,
            eligibleTargets,
            ignoredTargets,
            affectedWorkloads.Values,
            runtimeConfig.SensitiveOperationNameRestrictions);
    }

    private async Task<K8SScalePreview> BuildWorkloadScalePreviewAsync(
        K8SRuntimeConfig runtimeConfig,
        string namespaceName,
        K8SResourceType resourceType,
        IReadOnlyCollection<string> resourceNames,
        K8SScaleOperation operation,
        CancellationToken cancellationToken)
    {
        runtimeConfig.EnsureNamespaceAllowed(namespaceName);

        var workloads = (await discoveryService.ListWorkloadsAsync(runtimeConfig, namespaceName, [resourceType], cancellationToken))
            .ToDictionary(workload => workload.Name, StringComparer.OrdinalIgnoreCase);

        var requestedTargets = new List<K8SScaleTarget>();
        var eligibleTargets = new List<K8SScaleTarget>();
        var ignoredTargets = new List<K8SScaleIgnoredTarget>();
        var affectedWorkloads = new Dictionary<string, K8SScaleWorkloadPlan>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in resourceNames)
        {
            requestedTargets.Add(CreateTarget(resourceType, resourceType.ToDisplayName(), resourceName, namespaceName));

            if (!workloads.TryGetValue(resourceName, out var workload))
            {
                ignoredTargets.Add(CreateIgnoredTarget(
                    resourceType,
                    resourceType.ToDisplayName(),
                    resourceName,
                    namespaceName,
                    K8SOperationException.ResourceNotFound(resourceType, resourceName, namespaceName)));
                continue;
            }

            if (!runtimeConfig.IsSensitiveOperationAllowed(workload.Name, out _))
            {
                ignoredTargets.Add(CreateIgnoredTarget(
                    resourceType,
                    workload.Kind,
                    workload.Name,
                    namespaceName,
                    K8SOperationException.SensitiveRestrictionMismatch(runtimeConfig.SensitiveOperationNameRestrictions)));
                continue;
            }

            if (!TryCreateScalePlan(workload, operation, namespaceName, out var plan, out var ignoredReason))
            {
                ignoredTargets.Add(CreateIgnoredTarget(resourceType, workload.Kind, workload.Name, namespaceName, ignoredReason));
                continue;
            }

            eligibleTargets.Add(CreateTarget(resourceType, workload.Kind, workload.Name, namespaceName));
            affectedWorkloads.TryAdd(plan.DisplayName, plan);
        }

        return BuildScalePreview(
            namespaceName,
            $"Selected {resourceType.ToPluralDisplayName()}",
            resourceType,
            operation,
            requestedTargets,
            eligibleTargets,
            ignoredTargets,
            affectedWorkloads.Values,
            runtimeConfig.SensitiveOperationNameRestrictions);
    }

    private static K8SScalePreview BuildScalePreview(
        string namespaceName,
        string scopeName,
        K8SResourceType resourceType,
        K8SScaleOperation operation,
        List<K8SScaleTarget> requestedTargets,
        List<K8SScaleTarget> eligibleTargets,
        List<K8SScaleIgnoredTarget> ignoredTargets,
        IEnumerable<K8SScaleWorkloadPlan> workloads,
        IReadOnlyCollection<string> restrictionKeywords)
    {
        var workloadList = workloads
            .GroupBy(workload => workload.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(workload => workload.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new K8SScalePreview
        {
            Namespace = namespaceName,
            ScopeName = scopeName,
            ResourceType = resourceType,
            Operation = operation,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            RestrictionKeywords = [.. restrictionKeywords],
            RequestedTargets = requestedTargets,
            EligibleTargets = eligibleTargets,
            IgnoredTargets = ignoredTargets,
            Workloads = workloadList,
            CommandPreview = workloadList.SelectMany(workload => workload.CommandPreview).ToList()
        };
    }

    private async Task<K8SScaleResult> ExecuteScalePreviewAsync(
        K8SScalePreview preview,
        CancellationToken cancellationToken)
    {
        EnsurePreviewHasScalableTargets(preview);

        var runtimeConfig = runtimeConfigStore.GetCurrent();
        var commands = await ExecuteScaleCommandsAsync(runtimeConfig, preview.Workloads, cancellationToken);
        return new K8SScaleResult
        {
            Namespace = preview.Namespace,
            ScopeName = preview.ScopeName,
            ResourceType = preview.ResourceType,
            Operation = preview.Operation,
            AffectedTargets = preview.EligibleTargets.Select(item => item.Name).ToList(),
            Workloads = preview.Workloads,
            ExecutedCommands = commands,
            IgnoredTargets = preview.IgnoredTargets,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static void EnsurePreviewHasScalableTargets(K8SScalePreview preview)
    {
        if (preview.Workloads.Count > 0)
        {
            return;
        }

        if (preview.IgnoredTargets.Count > 0)
        {
            throw K8SOperationException.NoScalableTargetsRemain();
        }

        throw K8SOperationException.NoScalableTargetsFound();
    }

    private async Task<List<string>> ExecuteScaleCommandsAsync(
        K8SRuntimeConfig runtimeConfig,
        IEnumerable<K8SScaleWorkloadPlan> workloads,
        CancellationToken cancellationToken)
    {
        var commands = workloads.SelectMany(workload => workload.CommandPreview).ToList();
        foreach (var command in commands)
        {
            await provider.ExecuteKubectlAsync(runtimeConfig, command, cancellationToken);
        }

        return commands;
    }

    private static bool TryCreateScalePlan(
        K8SWorkloadReference workload,
        K8SScaleOperation operation,
        string namespaceName,
        out K8SScaleWorkloadPlan plan,
        out K8SOperationException ignoredReason)
    {
        plan = new K8SScaleWorkloadPlan();
        ignoredReason = K8SOperationException.NoScalableTargetsRemain();

        if (!IsScalableWorkload(workload.Kind))
        {
            ignoredReason = K8SOperationException.WorkloadNotScalable(workload.Kind, workload.Name);
            return false;
        }

        var targetReplicas = ResolveTargetReplicas(workload, operation);
        if (IsTargetAlreadySatisfied(workload.DesiredReplicas, targetReplicas, operation))
        {
            ignoredReason = K8SOperationException.ScaleTargetAlreadySatisfied(workload.Kind, workload.Name, targetReplicas);
            return false;
        }

        plan = new K8SScaleWorkloadPlan
        {
            Operation = operation,
            Kind = workload.Kind,
            Name = workload.Name,
            Namespace = namespaceName,
            CurrentReplicas = workload.DesiredReplicas,
            TargetReplicas = targetReplicas,
            PreviousReplicas = workload.PreviousReplicas,
            CommandPreview = KubectlCommandBuilder.BuildScaleCommands(
                namespaceName,
                operation,
                workload.Kind,
                workload.Name,
                workload.DesiredReplicas,
                targetReplicas)
        };
        return true;
    }

    private static bool IsScalableWorkload(string workloadKind)
    {
        return string.Equals(workloadKind, K8SResourceType.Deployment.ToDisplayName(), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(workloadKind, K8SResourceType.StatefulSet.ToDisplayName(), StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveTargetReplicas(K8SWorkloadReference workload, K8SScaleOperation operation)
    {
        return operation switch
        {
            K8SScaleOperation.ScaleDown => 0,
            K8SScaleOperation.ScaleUp => Math.Max(workload.PreviousReplicas ?? 1, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static bool IsTargetAlreadySatisfied(
        int currentReplicas,
        int targetReplicas,
        K8SScaleOperation operation)
    {
        return operation switch
        {
            K8SScaleOperation.ScaleDown => currentReplicas == targetReplicas,
            K8SScaleOperation.ScaleUp => currentReplicas >= targetReplicas,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
    }

    private static K8SScaleTarget CreateTarget(
        K8SResourceType resourceType,
        string kind,
        string name,
        string namespaceName)
    {
        return new K8SScaleTarget
        {
            ResourceType = resourceType,
            Kind = kind,
            Name = name,
            Namespace = namespaceName
        };
    }

    private static K8SScaleIgnoredTarget CreateIgnoredTarget(
        K8SResourceType resourceType,
        string kind,
        string name,
        string namespaceName,
        K8SOperationException exception)
    {
        return new K8SScaleIgnoredTarget
        {
            ResourceType = resourceType,
            Kind = kind,
            Name = name,
            Namespace = namespaceName,
            ReasonCode = exception.MessageCode,
            ReasonArguments = [.. exception.MessageArguments],
            Reason = exception.Message
        };
    }
}

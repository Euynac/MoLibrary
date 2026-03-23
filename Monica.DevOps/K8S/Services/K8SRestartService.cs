using Monica.DevOps.K8S.Abstractions;
using Monica.DevOps.K8S.Exceptions;
using Monica.DevOps.K8S.Models;
using Monica.DevOps.K8S.Services.Support;

namespace Monica.DevOps.K8S.Services;

public class K8SRestartService(
    K8SResourceDiscoveryService discoveryService,
    IK8SRuntimeConfigStore runtimeConfigStore,
    IK8SProvider provider)
{
    public Task<K8SRestartPreview> GetRestartPreviewAsync(string namespaceName, string serviceName, CancellationToken cancellationToken = default)
    {
        return BuildServiceRestartPreviewAsync(
            runtimeConfigStore.GetCurrent(),
            namespaceName,
            [serviceName],
            "Selected Services (Backing Workloads)",
            cancellationToken);
    }

    public Task<K8SRestartPreview> GetResourceRestartPreviewAsync(
        string namespaceName,
        K8SResourceType resourceType,
        string resourceName,
        CancellationToken cancellationToken = default)
    {
        return GetBatchRestartPreviewAsync(namespaceName, resourceType, [resourceName], cancellationToken);
    }

    public async Task<K8SRestartPreview> GetBatchRestartPreviewAsync(
        string namespaceName,
        K8SResourceType resourceType,
        IReadOnlyCollection<string> resourceNames,
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
            ? await BuildServiceRestartPreviewAsync(runtimeConfig, namespaceName, normalizedNames, "Selected Services (Backing Workloads)", cancellationToken)
            : await BuildWorkloadRestartPreviewAsync(runtimeConfig, namespaceName, resourceType, normalizedNames, cancellationToken);
    }

    public Task<K8SRestartPreview> GetNamespaceRestartPreviewAsync(string namespaceName, CancellationToken cancellationToken = default)
    {
        return GetNamespaceRestartPreviewAsync(namespaceName, K8SResourceType.Service, cancellationToken);
    }

    public async Task<K8SRestartPreview> GetNamespaceRestartPreviewAsync(
        string namespaceName,
        K8SResourceType resourceType,
        CancellationToken cancellationToken = default)
    {
        var runtimeConfig = runtimeConfigStore.GetCurrent();
        runtimeConfig.EnsureNamespaceAllowed(namespaceName);

        var names = resourceType == K8SResourceType.Service
            ? (await discoveryService.ListServiceSummariesAsync(runtimeConfig, namespaceName, cancellationToken))
                .Select(item => item.Name)
                .ToList()
            : (await discoveryService.ListWorkloadsAsync(runtimeConfig, namespaceName, [resourceType], cancellationToken))
                .Select(item => item.Name)
                .ToList();

        return resourceType == K8SResourceType.Service
            ? await BuildServiceRestartPreviewAsync(
                runtimeConfig,
                namespaceName,
                names,
                "Namespace Services (Backing Workloads)",
                cancellationToken)
            : await BuildWorkloadRestartPreviewAsync(runtimeConfig, namespaceName, resourceType, names, cancellationToken);
    }

    public Task<K8SRestartResult> RestartServiceAsync(string namespaceName, string serviceName, CancellationToken cancellationToken = default)
    {
        return RestartResourcesAsync(namespaceName, K8SResourceType.Service, [serviceName], cancellationToken);
    }

    public async Task<K8SRestartResult> RestartResourcesAsync(
        string namespaceName,
        K8SResourceType resourceType,
        IReadOnlyCollection<string> resourceNames,
        CancellationToken cancellationToken = default)
    {
        var runtimeConfig = runtimeConfigStore.GetCurrent();
        runtimeConfig.EnsureNamespaceAllowed(namespaceName);

        var preview = await GetBatchRestartPreviewAsync(namespaceName, resourceType, resourceNames, cancellationToken);
        EnsurePreviewHasRestartableTargets(preview);

        var commands = await ExecuteRestartCommandsAsync(runtimeConfig, namespaceName, preview.Workloads, cancellationToken);
        return new K8SRestartResult
        {
            Namespace = namespaceName,
            ScopeName = preview.ScopeName,
            ResourceType = resourceType,
            AffectedTargets = preview.EligibleTargets.Select(item => item.Name).ToList(),
            Workloads = preview.Workloads,
            ExecutedCommands = commands,
            IgnoredTargets = preview.IgnoredTargets,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
    }

    public Task<K8SRestartResult> RestartNamespaceAsync(string namespaceName, CancellationToken cancellationToken = default)
    {
        return RestartNamespaceAsync(namespaceName, K8SResourceType.Service, cancellationToken);
    }

    public async Task<K8SRestartResult> RestartNamespaceAsync(
        string namespaceName,
        K8SResourceType resourceType,
        CancellationToken cancellationToken = default)
    {
        var preview = await GetNamespaceRestartPreviewAsync(namespaceName, resourceType, cancellationToken);
        EnsurePreviewHasRestartableTargets(preview);

        return await RestartResourcesAsync(
            namespaceName,
            resourceType,
            preview.RequestedTargets.Select(item => item.Name).ToList(),
            cancellationToken);
    }

    private async Task<K8SRestartPreview> BuildServiceRestartPreviewAsync(
        K8SRuntimeConfig runtimeConfig,
        string namespaceName,
        IReadOnlyCollection<string> serviceNames,
        string scopeName,
        CancellationToken cancellationToken)
    {
        runtimeConfig.EnsureNamespaceAllowed(namespaceName);

        var serviceLookup = (await discoveryService.ListServiceSummariesAsync(runtimeConfig, namespaceName, cancellationToken))
            .ToDictionary(service => service.Name, StringComparer.OrdinalIgnoreCase);
        var workloads = await discoveryService.ListWorkloadsAsync(
            runtimeConfig,
            namespaceName,
            K8SResourceDiscoveryService.RestartableWorkloadTypes,
            cancellationToken);

        var requestedTargets = new List<K8SRestartTarget>();
        var eligibleTargets = new List<K8SRestartTarget>();
        var ignoredTargets = new List<K8SRestartIgnoredTarget>();
        var affectedWorkloads = new Dictionary<string, K8SWorkloadReference>(StringComparer.OrdinalIgnoreCase);

        foreach (var serviceName in serviceNames)
        {
            requestedTargets.Add(new K8SRestartTarget
            {
                ResourceType = K8SResourceType.Service,
                Kind = K8SResourceType.Service.ToDisplayName(),
                Name = serviceName,
                Namespace = namespaceName
            });

            if (!serviceLookup.TryGetValue(serviceName, out var service))
            {
                ignoredTargets.Add(CreateIgnoredTarget(
                    K8SResourceType.Service,
                    serviceName,
                    namespaceName,
                    K8SOperationException.ServiceNotFound(serviceName, namespaceName)));
                continue;
            }

            if (!runtimeConfig.IsSensitiveOperationAllowed(service.Name, out _))
            {
                ignoredTargets.Add(CreateIgnoredTarget(
                    K8SResourceType.Service,
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
                ignoredTargets.Add(CreateIgnoredTarget(K8SResourceType.Service, service.Name, namespaceName, ex));
                continue;
            }

            eligibleTargets.Add(new K8SRestartTarget
            {
                ResourceType = K8SResourceType.Service,
                Kind = K8SResourceType.Service.ToDisplayName(),
                Name = service.Name,
                Namespace = namespaceName
            });

            foreach (var workload in serviceWorkloads)
            {
                affectedWorkloads.TryAdd(workload.DisplayName, workload);
            }
        }

        return BuildRestartPreview(
            namespaceName,
            scopeName,
            K8SResourceType.Service,
            requestedTargets,
            eligibleTargets,
            ignoredTargets,
            affectedWorkloads.Values,
            runtimeConfig.SensitiveOperationNameRestrictions);
    }

    private async Task<K8SRestartPreview> BuildWorkloadRestartPreviewAsync(
        K8SRuntimeConfig runtimeConfig,
        string namespaceName,
        K8SResourceType resourceType,
        IReadOnlyCollection<string> resourceNames,
        CancellationToken cancellationToken)
    {
        runtimeConfig.EnsureNamespaceAllowed(namespaceName);

        var workloads = (await discoveryService.ListWorkloadsAsync(runtimeConfig, namespaceName, [resourceType], cancellationToken))
            .ToDictionary(workload => workload.Name, StringComparer.OrdinalIgnoreCase);

        var requestedTargets = new List<K8SRestartTarget>();
        var eligibleTargets = new List<K8SRestartTarget>();
        var ignoredTargets = new List<K8SRestartIgnoredTarget>();
        var affectedWorkloads = new Dictionary<string, K8SWorkloadReference>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in resourceNames)
        {
            requestedTargets.Add(new K8SRestartTarget
            {
                ResourceType = resourceType,
                Kind = resourceType.ToDisplayName(),
                Name = resourceName,
                Namespace = namespaceName
            });

            if (!workloads.TryGetValue(resourceName, out var workload))
            {
                ignoredTargets.Add(CreateIgnoredTarget(
                    resourceType,
                    resourceName,
                    namespaceName,
                    K8SOperationException.ResourceNotFound(resourceType, resourceName, namespaceName)));
                continue;
            }

            if (!runtimeConfig.IsSensitiveOperationAllowed(workload.Name, out _))
            {
                ignoredTargets.Add(CreateIgnoredTarget(
                    resourceType,
                    workload.Name,
                    namespaceName,
                    K8SOperationException.SensitiveRestrictionMismatch(runtimeConfig.SensitiveOperationNameRestrictions)));
                continue;
            }

            eligibleTargets.Add(new K8SRestartTarget
            {
                ResourceType = resourceType,
                Kind = resourceType.ToDisplayName(),
                Name = workload.Name,
                Namespace = namespaceName
            });

            affectedWorkloads.TryAdd(workload.DisplayName, workload);
        }

        return BuildRestartPreview(
            namespaceName,
            $"Selected {resourceType.ToPluralDisplayName()}",
            resourceType,
            requestedTargets,
            eligibleTargets,
            ignoredTargets,
            affectedWorkloads.Values,
            runtimeConfig.SensitiveOperationNameRestrictions);
    }

    private static K8SRestartPreview BuildRestartPreview(
        string namespaceName,
        string scopeName,
        K8SResourceType resourceType,
        List<K8SRestartTarget> requestedTargets,
        List<K8SRestartTarget> eligibleTargets,
        List<K8SRestartIgnoredTarget> ignoredTargets,
        IEnumerable<K8SWorkloadReference> workloads,
        IReadOnlyCollection<string> restrictionKeywords)
    {
        var workloadList = workloads
            .GroupBy(workload => workload.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(workload => workload.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new K8SRestartPreview
        {
            Namespace = namespaceName,
            ScopeName = scopeName,
            ResourceType = resourceType,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            RestrictionKeywords = [.. restrictionKeywords],
            RequestedTargets = requestedTargets,
            EligibleTargets = eligibleTargets,
            IgnoredTargets = ignoredTargets,
            Workloads = workloadList,
            CommandPreview = KubectlCommandBuilder.BuildRestartCommands(namespaceName, workloadList)
        };
    }

    private static void EnsurePreviewHasRestartableTargets(K8SRestartPreview preview)
    {
        if (preview.Workloads.Count > 0)
        {
            return;
        }

        if (preview.IgnoredTargets.Count > 0)
        {
            throw K8SOperationException.NoRestartableTargetsRemain();
        }

        throw K8SOperationException.NoRestartableTargetsFound();
    }

    private async Task<List<string>> ExecuteRestartCommandsAsync(
        K8SRuntimeConfig runtimeConfig,
        string namespaceName,
        IEnumerable<K8SWorkloadReference> workloads,
        CancellationToken cancellationToken)
    {
        var commands = KubectlCommandBuilder.BuildRestartCommands(namespaceName, workloads);
        foreach (var command in commands)
        {
            await provider.ExecuteKubectlAsync(runtimeConfig, command, cancellationToken);
        }

        return commands;
    }

    private static K8SRestartIgnoredTarget CreateIgnoredTarget(
        K8SResourceType resourceType,
        string name,
        string namespaceName,
        K8SOperationException exception)
    {
        return new K8SRestartIgnoredTarget
        {
            ResourceType = resourceType,
            Kind = resourceType.ToDisplayName(),
            Name = name,
            Namespace = namespaceName,
            ReasonCode = exception.MessageCode,
            ReasonArguments = [.. exception.MessageArguments],
            Reason = exception.Message
        };
    }
}

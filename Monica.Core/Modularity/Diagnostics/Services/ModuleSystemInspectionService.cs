using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;
using Monica.Core.Modularity.Services;
using Monica.Core.Modularity.Services.Support;
using Monica.Core.TypeDiscovery.Models;

namespace Monica.Core.Modularity.Diagnostics.Services;

/// <summary>
/// Default implementation of <see cref="IModuleSystemInspectionService"/>.
/// Provides status, performance, and dependency information for dashboards and monitoring.
/// </summary>
public sealed class ModuleSystemInspectionService(MonicaApplication application) : IModuleSystemInspectionService
{
    /// <summary>
    /// Gets the overall module system status.
    /// </summary>
    /// <returns>The system status.</returns>
    public ModuleSystemStatus GetSystemStatus()
    {
        var enabledModules = application.Modules.RuntimeSnapshots.Count;
        var disabledModules = application.ModuleStates.GetDisabledModuleTypes().Count;
        var totalModules = enabledModules + disabledModules;
        var errorModules = application.Modules.RegistrationErrors.Count;

        var hasCircularDependencies = application.Dependencies.HasCircularDependencies();
        var hasRegistrationErrors = application.Modules.RegistrationErrors.Count > 0;

        var composition = application.Profiling.GetCompositionPerformance();
        var compositionCompleted = composition.Milestones.Any(static milestone =>
            milestone.Milestone == ModuleCompositionMilestone.CompositionCompleted);
        var state = DetermineSystemState(compositionCompleted, errorModules, hasCircularDependencies);

        return new ModuleSystemStatus
        {
            IsInitialized = compositionCompleted,
            TotalModules = totalModules,
            EnabledModules = enabledModules,
            DisabledModules = disabledModules,
            ErrorModules = errorModules,
            ServiceRegistrationDurationMs = composition.ServiceRegistrationDurationMs,
            State = state,
            HasCircularDependencies = hasCircularDependencies,
            HasRegistrationErrors = hasRegistrationErrors
        };
    }

    /// <summary>
    /// Gets module system performance information.
    /// </summary>
    /// <returns>The performance snapshot.</returns>
    public ModuleSystemPerformance GetSystemPerformance()
    {
        var runtimeModuleKeys = application.Modules.RuntimeSnapshots
            .Select(static snapshot => snapshot.ModuleKey)
            .ToHashSet();
        return new ModuleSystemPerformance
        {
            Composition = application.Profiling.GetCompositionPerformance(),
            Modules = application.Profiling.GetModulePerformances(runtimeModuleKeys)
        };
    }

    /// <summary>
    /// Gets registration and dependency information for all modules.
    /// </summary>
    /// <returns>The registration information.</returns>
    public ModuleRegistrationOverview GetRegistrationInfo()
    {
        var enabledModules = new List<ModuleBasicInfo>();
        var disabledModules = new List<ModuleBasicInfo>();
        var modulesByOrder = new Dictionary<int, ModuleBasicInfo>();

        // Build the enabled-module list from runtime snapshots.
        foreach (var snapshot in application.Modules.RuntimeSnapshots.OrderBy(s => s.RegisterInfo.Order))
        {
            var basicInfo = CreateModuleBasicInfo(snapshot);
            enabledModules.Add(basicInfo);
            modulesByOrder[basicInfo.Order] = basicInfo;
        }

        // Build the disabled-module list from the module manager.
        var disabledModuleTypes = application.ModuleStates.GetDisabledModuleTypes();
        foreach (var moduleType in disabledModuleTypes)
        {
            var moduleKey = application.Dependencies.ResolveModuleKey(moduleType);

            var basicInfo = new ModuleBasicInfo
            {
                ModuleTypeName = moduleType.Name,
                ModuleFullTypeName = moduleType.FullName ?? moduleType.Name,
                ModuleKey = moduleKey,
                Order = int.MaxValue, // Disabled modules do not participate in registration ordering.
                Status = ModulePhase.Disabled,
                Dependencies = application.Dependencies.DependenciesByModule.TryGetValue(moduleKey, out var deps)
                    ? [.. deps]
                    : [],
                SerialPhaseDurationMs = 0,
                IsDisabled = true,
                IsWebModule = typeof(IWebModule).IsAssignableFrom(moduleType),
                IsDowngradedFromWebModule = false,
                HasErrors = false
            };
            disabledModules.Add(basicInfo);
        }

        var totalSerialPhaseDuration = enabledModules.Sum(m => m.SerialPhaseDurationMs);
        var statistics = new ModuleRegistrationStatistics
        {
            TotalModules = enabledModules.Count + disabledModules.Count,
            EnabledModules = enabledModules.Count,
            DisabledModules = disabledModules.Count,
            TotalSerialPhaseDurationMs = totalSerialPhaseDuration
        };

        return new ModuleRegistrationOverview
        {
            EnabledModules = enabledModules,
            DisabledModules = disabledModules,
            ModulesByOrder = modulesByOrder,
            Statistics = statistics
        };
    }

    /// <summary>
    /// Gets detailed information for a specific module.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <returns>The module details, or `null` if the module does not exist.</returns>
    public ModuleDetailInfo? GetModuleDetail(Type moduleType)
    {
        var snapshot = application.Modules.RuntimeSnapshots.FirstOrDefault(s => s.ModuleType == moduleType);
        if (snapshot == null)
        {
            return null;
        }

        return CreateModuleDetailInfo(snapshot);
    }

    /// <summary>
    /// Gets detailed information for a specific module.
    /// </summary>
    /// <param name="moduleKey">The module key.</param>
    /// <returns>The module details, or `null` if the module does not exist.</returns>
    public ModuleDetailInfo? GetModuleDetail(ModuleKey moduleKey)
    {
        var snapshot = application.Modules.RuntimeSnapshots.FirstOrDefault(s => s.ModuleKey == moduleKey);
        if (snapshot == null)
        {
            return null;
        }

        return CreateModuleDetailInfo(snapshot);
    }

    /// <summary>
    /// Gets the module dependency graph.
    /// </summary>
    /// <returns>The dependency graph.</returns>
    public ModuleDependencyGraph GetDependencyGraph()
    {
        var graph = application.Dependencies.CalculateCompleteModuleDependencyGraph();
        var moduleSnapshotsByKey = application.Modules.RuntimeSnapshots.ToDictionary(snapshot => snapshot.ModuleKey);
        var nodes = new List<ModuleDependencyNode>();
        var edges = new List<ModuleDependencyEdge>();
        var edgeKeys = new HashSet<(ModuleKey Source, ModuleKey Target, DependencyType Type)>();

        // Create graph nodes.
        foreach (var moduleKey in graph.Nodes)
        {
            var moduleType = application.Dependencies.ModuleTypesByKey.GetValueOrDefault(moduleKey);
            moduleSnapshotsByKey.TryGetValue(moduleKey, out var snapshot);

            var isEnabled = moduleType != null && !application.ModuleStates.IsModuleDisabled(moduleType);
            var status = GetModuleStatus(moduleKey, moduleType);
            var isWebModule = snapshot?.IsWebModule
                ?? (moduleType != null && typeof(IWebModule).IsAssignableFrom(moduleType));
            var isDowngradedFromWebModule = snapshot?.IsDowngradedFromWebModule ?? false;

            var dependencies = application.Dependencies.CalculateModuleDependencies(moduleKey);
            var directDeps = application.Dependencies.DependenciesByModule.TryGetValue(moduleKey, out var directDependencies)
                ? directDependencies.Count
                : 0;

            var dependentCount = application.Dependencies.DependenciesByModule.Values.Count(deps => deps.Contains(moduleKey));
            var cyclePath = application.Dependencies.FindCycleInvolvingModule(moduleKey);

            nodes.Add(new ModuleDependencyNode
            {
                Module = moduleKey,
                ModuleName = moduleKey.ToString(),
                ModuleTypeName = moduleType?.Name ?? "Unknown",
                IsEnabled = isEnabled,
                IsUIModule = moduleKey.IsUIModule,
                IsWebModule = isWebModule,
                IsDowngradedFromWebModule = isDowngradedFromWebModule,
                IsThirdPartyModule = !moduleKey.IsBuiltIn,
                DirectDependencyCount = directDeps,
                TotalDependencyCount = dependencies.Count,
                DependentModuleCount = dependentCount,
                Layer = CalculateModuleLayer(moduleKey),
                IsPartOfCycle = cyclePath.Count > 0,
                Status = status
            });
        }

        // Create graph edges, including transitive dependencies.
        foreach (var source in graph.Nodes)
        {
            var dependencyInfo = application.Dependencies.GetModuleDependencyInfo(source);

            foreach (var target in dependencyInfo.DirectDependencies)
            {
                var dependencyType = DetermineEdgeType(source, target);
                var isPartOfCycle = IsEdgePartOfCycle(source, target);

                if (edgeKeys.Add((source, target, dependencyType)))
                {
                    edges.Add(new ModuleDependencyEdge
                    {
                        SourceModule = source,
                        TargetModule = target,
                        DependencyType = dependencyType,
                        IsPartOfCycle = isPartOfCycle
                    });
                }
            }

            var transitiveDependencies = dependencyInfo.AllDependencies
                .Except(dependencyInfo.DirectDependencies)
                .Where(target => target != source);

            foreach (var target in transitiveDependencies)
            {
                if (edgeKeys.Add((source, target, DependencyType.Transitive)))
                {
                    edges.Add(new ModuleDependencyEdge
                    {
                        SourceModule = source,
                        TargetModule = target,
                        DependencyType = DependencyType.Transitive,
                        IsPartOfCycle = false
                    });
                }
            }
        }

        var hasCircularDependencies = graph.HasCycles();
        var circularPaths = FindAllCircularPaths();
        var topologicalOrder = hasCircularDependencies ? [] : graph.TopologicalSort();
        var moduleLayers = CalculateModuleLayers(nodes);

        return new ModuleDependencyGraph
        {
            Nodes = nodes,
            Edges = edges,
            HasCircularDependencies = hasCircularDependencies,
            CircularDependencyPaths = circularPaths,
            TopologicalOrder = topologicalOrder,
            ModuleLayers = moduleLayers
        };
    }

    /// <summary>
    /// Gets the module system health check result.
    /// </summary>
    /// <returns>The health check result.</returns>
    public ModuleSystemHealthCheck GetHealthCheck()
    {
        var healthCheckItems = new List<HealthCheckItem>();
        var issues = new List<HealthIssue>();
        var recommendations = new List<string>();

        // Check overall initialization state.
        healthCheckItems.Add(CheckSystemInitialization());

        // Check for circular dependencies.
        healthCheckItems.Add(CheckCircularDependencies(issues));

        // Check for module registration errors.
        healthCheckItems.Add(CheckModuleErrors(issues));

        // Check for performance issues.
        healthCheckItems.Add(CheckPerformanceIssues(issues));

        // Check for disabled modules.
        healthCheckItems.Add(CheckDisabledModules(issues));

        // Generate recommendations from the collected issues.
        GenerateRecommendations(issues, recommendations);

        var overallHealth = DetermineOverallHealth(healthCheckItems, issues);
        var healthSummary = GenerateHealthSummary(overallHealth, healthCheckItems, issues);
        var performanceMetrics = CalculateHealthPerformanceMetrics();

        return new ModuleSystemHealthCheck
        {
            OverallHealth = overallHealth,
            HealthSummary = healthSummary,
            CheckTime = DateTime.Now,
            HealthCheckItems = healthCheckItems,
            Issues = issues,
            Recommendations = recommendations,
            PerformanceMetrics = performanceMetrics
        };
    }

    /// <summary>
    /// Gets the current type-finder assembly analysis snapshot.
    /// </summary>
    public TypeFinderAssemblyAnalysis GetAssemblyAnalysis()
    {
        return application.TypeFinder.GetAssemblyAnalysis();
    }

    #region Private Helpers

    private static ModuleSystemState DetermineSystemState(
        bool compositionCompleted,
        int errorModules,
        bool hasCircularDependencies)
    {
        if (errorModules > 0 || hasCircularDependencies)
        {
            return ModuleSystemState.Failed;
        }

        if (!compositionCompleted)
        {
            return ModuleSystemState.NotInitialized;
        }

        return ModuleSystemState.Initialized;
    }

    private ModuleBasicInfo CreateModuleBasicInfo(ModuleRuntimeSnapshot snapshot)
    {
        var moduleKey = snapshot.ModuleKey;
        var dependencies = application.Dependencies.DependenciesByModule.TryGetValue(moduleKey, out var deps)
            ? deps.ToList()
            : [];

        var hasErrors = application.Modules.RegistrationErrors.Any(e => e.ModuleType == snapshot.ModuleType);

        return new ModuleBasicInfo
        {
            ModuleTypeName = snapshot.ModuleType.Name,
            ModuleFullTypeName = snapshot.ModuleType.FullName ?? snapshot.ModuleType.Name,
            ModuleKey = moduleKey,
            Order = snapshot.RegisterInfo.Order,
            Status = snapshot.RegisterInfo.ModulePhase,
            Dependencies = dependencies,
            SerialPhaseDurationMs = snapshot.SerialPhaseDurationMs,
            IsDisabled = false,
            IsWebModule = snapshot.IsWebModule,
            IsDowngradedFromWebModule = snapshot.IsDowngradedFromWebModule,
            HasErrors = hasErrors
        };
    }

    private ModuleDetailInfo CreateModuleDetailInfo(ModuleRuntimeSnapshot snapshot)
    {
        var basicInfo = CreateModuleBasicInfo(snapshot);

        var performanceInfo = application.Profiling.GetModulePerformance(snapshot);

        var moduleKey = snapshot.ModuleKey;
        var dependencyInfo = application.Dependencies.GetModuleDependencyInfo(moduleKey);

        var configInfo = new ModuleConfigInfo
        {
            IsDisabled = false,
            IsWebModule = snapshot.IsWebModule,
            IsDowngradedFromWebModule = snapshot.IsDowngradedFromWebModule,
            ConfigurationItems = [], // This may later be populated from concrete module configuration data.
            ConfiguredOptions = CreateConfiguredOptions(snapshot.RegisterInfo),
            RegisterRequestCount = snapshot.RegisterInfo.RegisterRequests.Count,
            HasCircularDependency = dependencyInfo.IsPartOfCycle
        };

        var executionHistory = new List<ModulePhaseExecution>(); // This may later be populated from concrete execution history.

        var errors = application.Modules.RegistrationErrors
            .Where(e => e.ModuleType == snapshot.ModuleType)
            .Select(e => new ModuleErrorInfo
            {
                ErrorType = e.ErrorType.ToString(),
                ErrorMessage = e.ErrorMessage,
                Phase = e.Phase,
                StackTrace = e.StackTrace
            })
            .ToList();

        return new ModuleDetailInfo
        {
            BasicInfo = basicInfo,
            PerformanceInfo = performanceInfo,
            DependencyInfo = dependencyInfo,
            ConfigInfo = configInfo,
            ExecutionHistory = executionHistory,
            Errors = errors
        };
    }

    private List<ModuleConfiguredOption> CreateConfiguredOptions(ModuleRegistrationState registerInfo)
    {
        return registerInfo.FinalConfigures
            .Where(static entry => typeof(IModuleOptionsBase).IsAssignableFrom(entry.Key))
            .Select(entry => new ModuleConfiguredOption
            {
                OptionType = entry.Key,
                OptionInstance = entry.Value,
                IsExtraOption = entry.Key != registerInfo.ModuleOptionType
            })
            .OrderBy(option => option.IsExtraOption ? 1 : 0)
            .ThenBy(option => option.OptionType.Name)
            .ToList();
    }

    private ModulePhase GetModuleStatus(ModuleKey moduleKey, Type? moduleType)
    {
        if (moduleType == null || application.ModuleStates.IsModuleDisabled(moduleType))
        {
            return ModulePhase.Disabled;
        }

        var snapshot = application.Modules.RuntimeSnapshots.FirstOrDefault(s => s.ModuleKey == moduleKey);
        return snapshot?.RegisterInfo.ModulePhase ?? ModulePhase.None;
    }

    private int CalculateModuleLayer(ModuleKey moduleKey)
    {
        // Use dependency count as the graph layer.
        var dependencies = application.Dependencies.CalculateModuleDependencies(moduleKey);
        return dependencies.Count;
    }

    private DependencyType DetermineEdgeType(ModuleKey source, ModuleKey target)
    {
        // Check whether the edge is a direct dependency.
        if (application.Dependencies.DependenciesByModule.TryGetValue(source, out var directDeps) && directDeps.Contains(target))
        {
            // Distinguish direct edges that also participate in a cycle.
            var cyclePath = application.Dependencies.FindCycleInvolvingModule(source);
            if (cyclePath.Contains(target))
            {
                return DependencyType.Circular;
            }
            return DependencyType.Direct;
        }

        return DependencyType.Transitive;
    }

    private bool IsEdgePartOfCycle(ModuleKey source, ModuleKey target)
    {
        var sourceCycle = application.Dependencies.FindCycleInvolvingModule(source);
        var targetCycle = application.Dependencies.FindCycleInvolvingModule(target);
        return sourceCycle.Count > 0 && targetCycle.Count > 0 &&
               sourceCycle.Contains(target) && targetCycle.Contains(source);
    }

    private List<List<ModuleKey>> FindAllCircularPaths()
    {
        var circularPaths = new List<List<ModuleKey>>();
        var processedModules = new HashSet<ModuleKey>();

        foreach (var module in application.Dependencies.DependenciesByModule.Keys)
        {
            if (processedModules.Contains(module)) continue;

            var cyclePath = application.Dependencies.FindCycleInvolvingModule(module);
            if (cyclePath.Count > 0)
            {
                circularPaths.Add([.. cyclePath]);
                processedModules.UnionWith(cyclePath);
            }
        }

        return circularPaths;
    }

    private Dictionary<int, List<ModuleKey>> CalculateModuleLayers(List<ModuleDependencyNode> nodes)
    {
        var layers = new Dictionary<int, List<ModuleKey>>();

        foreach (var node in nodes)
        {
            if (!layers.ContainsKey(node.Layer))
            {
                layers[node.Layer] = [];
            }
            layers[node.Layer].Add(node.Module);
        }

        return layers;
    }

    private HealthCheckItem CheckSystemInitialization()
    {
        var isInitialized = application.Modules.RuntimeSnapshots.Count > 0;
        var status = isInitialized ? HealthStatus.Healthy : HealthStatus.Critical;
        var details = isInitialized 
            ? $"System initialized with {application.Modules.RuntimeSnapshots.Count} modules"
            : "System not initialized";

        return new HealthCheckItem
        {
            Name = "System Initialization",
            Description = "Check if the module system has been properly initialized",
            Status = status,
            Details = details,
            ExecutionTimeMs = 0 // This check is effectively instantaneous.
        };
    }

    private HealthCheckItem CheckCircularDependencies(List<HealthIssue> issues)
    {
        var hasCircularDependencies = application.Dependencies.HasCircularDependencies();
        var status = hasCircularDependencies ? HealthStatus.Critical : HealthStatus.Healthy;
        var details = hasCircularDependencies 
            ? "Circular dependencies detected in module system"
            : "No circular dependencies found";

        if (hasCircularDependencies)
        {
            issues.Add(new HealthIssue
            {
                Severity = IssueSeverity.Critical,
                Title = "Circular Dependencies Detected",
                Description = "The module system has circular dependencies which can cause initialization issues",
                IssueType = IssueType.Dependency,
                RecommendedAction = "Review module dependencies and remove circular references"
            });
        }

        return new HealthCheckItem
        {
            Name = "Circular Dependencies",
            Description = "Check for circular dependencies in the module system",
            Status = status,
            Details = details,
            ExecutionTimeMs = 1 // Fast check.
        };
    }

    private HealthCheckItem CheckModuleErrors(List<HealthIssue> issues)
    {
        var errorCount = application.Modules.RegistrationErrors.Count;
        var status = errorCount == 0 ? HealthStatus.Healthy : HealthStatus.Critical;
        var details = errorCount == 0 
            ? "No module registration errors found"
            : $"{errorCount} module registration errors detected";

        if (errorCount > 0)
        {
            foreach (var error in application.Modules.RegistrationErrors)
            {
                issues.Add(new HealthIssue
                {
                    Severity = IssueSeverity.High,
                    Title = $"Module Registration Error: {error.ModuleType.Name}",
                    Description = error.ErrorMessage,
                    IssueType = IssueType.Initialization,
                    RecommendedAction = "Review module configuration and fix initialization errors"
                });
            }
        }

        return new HealthCheckItem
        {
            Name = "Module Errors",
            Description = "Check for module registration and initialization errors",
            Status = status,
            Details = details,
            ExecutionTimeMs = 1
        };
    }

    private HealthCheckItem CheckPerformanceIssues(List<HealthIssue> issues)
    {
        var serviceRegistrationDuration = application.Profiling
            .GetCompositionPerformance()
            .ServiceRegistrationDurationMs;
        var slowModules = application.Profiling.GetModuleProfilesSortedBySerialPhaseDuration().Take(3).ToList();
        
        var status = HealthStatus.Healthy;
        var details = $"Service registration time: {serviceRegistrationDuration:F1}ms";

        // Performance thresholds.
        const double slowServiceRegistrationThreshold = 5000; // 5 seconds
        const long verySlowModuleThreshold = 1000; // 1 second

        if (serviceRegistrationDuration > slowServiceRegistrationThreshold)
        {
            status = HealthStatus.Warning;
            issues.Add(new HealthIssue
            {
                Severity = IssueSeverity.Medium,
                Title = "Slow Monica Service Registration",
                Description = $"Monica service registration took {serviceRegistrationDuration:F1}ms, which exceeds the recommended threshold",
                IssueType = IssueType.Performance,
                RecommendedAction = "Review serial module callbacks, composition work, and blocking checkpoints"
            });
        }

        foreach (var module in slowModules.Where(module =>
                     module.GetSerialPhaseDurationMs() > verySlowModuleThreshold))
        {
            issues.Add(new HealthIssue
            {
                Severity = IssueSeverity.Low,
                Title = $"Slow Module: {module.ModuleType.Name}",
                Description = $"Module serial callbacks took {module.GetSerialPhaseDurationMs():F1}ms to initialize",
                IssueType = IssueType.Performance,
                RecommendedAction = $"Optimize {module.ModuleType.Name} module initialization"
            });
        }

        return new HealthCheckItem
        {
            Name = "Performance Issues",
            Description = "Check for performance-related issues in module initialization",
            Status = status,
            Details = details,
            ExecutionTimeMs = 2
        };
    }

    private HealthCheckItem CheckDisabledModules(List<HealthIssue> issues)
    {
        var disabledModules = application.ModuleStates.GetDisabledModuleTypes();
        var status = disabledModules.Count == 0 ? HealthStatus.Healthy : HealthStatus.Warning;
        var details = disabledModules.Count == 0 
            ? "No disabled modules found"
            : $"{disabledModules.Count} modules are disabled";

        if (disabledModules.Count > 0)
        {
            issues.Add(new HealthIssue
            {
                Severity = IssueSeverity.Information,
                Title = "Disabled Modules",
                Description = $"{disabledModules.Count} modules are disabled",
                IssueType = IssueType.Configuration,
                RecommendedAction = "Review disabled modules to ensure they are intentionally disabled"
            });
        }

        return new HealthCheckItem
        {
            Name = "Disabled Modules",
            Description = "Check for disabled modules in the system",
            Status = status,
            Details = details,
            ExecutionTimeMs = 1
        };
    }

    private void GenerateRecommendations(List<HealthIssue> issues, List<string> recommendations)
    {
        if (issues.Any(i => i.IssueType == IssueType.Dependency))
        {
            recommendations.Add("Review and refactor module dependencies to eliminate circular references");
        }

        if (issues.Any(i => i.IssueType == IssueType.Performance))
        {
            recommendations.Add("Optimize slow-initializing modules to improve system startup time");
        }

        if (issues.Any(i => i.IssueType == IssueType.Initialization))
        {
            recommendations.Add("Fix module initialization errors to ensure system stability");
        }

        if (issues.Any(i => i.Severity >= IssueSeverity.High))
        {
            recommendations.Add("Address high-severity issues immediately to prevent system instability");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("System is healthy - continue monitoring for optimal performance");
        }
    }

    private HealthStatus DetermineOverallHealth(List<HealthCheckItem> healthCheckItems, List<HealthIssue> issues)
    {
        if (healthCheckItems.Any(item => item.Status == HealthStatus.Critical) || 
            issues.Any(issue => issue.Severity == IssueSeverity.Critical))
        {
            return HealthStatus.Critical;
        }

        if (healthCheckItems.Any(item => item.Status == HealthStatus.Unhealthy) ||
            issues.Any(issue => issue.Severity == IssueSeverity.High))
        {
            return HealthStatus.Unhealthy;
        }

        if (healthCheckItems.Any(item => item.Status == HealthStatus.Warning) ||
            issues.Any(issue => issue.Severity >= IssueSeverity.Medium))
        {
            return HealthStatus.Warning;
        }

        return HealthStatus.Healthy;
    }

    private string GenerateHealthSummary(HealthStatus overallHealth, List<HealthCheckItem> healthCheckItems, List<HealthIssue> issues)
    {
        var summary = overallHealth switch
        {
            HealthStatus.Healthy => "Module system is healthy and operating normally",
            HealthStatus.Warning => "Module system has minor issues that should be addressed",
            HealthStatus.Unhealthy => "Module system has significant issues requiring attention",
            HealthStatus.Critical => "Module system has critical issues that need immediate attention",
            _ => "Module system health status unknown"
        };

        if (issues.Count > 0)
        {
            summary += $" ({issues.Count} issue{(issues.Count != 1 ? "s" : "")} detected)";
        }

        return summary;
    }

    private HealthPerformanceMetrics CalculateHealthPerformanceMetrics()
    {
        var moduleProfiles = application.Profiling.GetModuleProfilesSortedBySerialPhaseDuration();
        var serviceRegistrationDuration = application.Profiling
            .GetCompositionPerformance()
            .ServiceRegistrationDurationMs;
        
        var averageModuleSerialPhaseDuration = moduleProfiles.Count > 0
            ? moduleProfiles.Average(static profile => profile.GetSerialPhaseDurationMs())
            : 0;

        var slowestModule = moduleProfiles.FirstOrDefault();
        var slowestModuleSerialPhaseDuration = (long)Math.Floor(
            slowestModule?.GetSerialPhaseDurationMs() ?? 0);
        var slowestModuleName = slowestModule?.ModuleType.Name;

        // Calculate an efficiency score from 0-100 based on init time and module count.
        var efficiencyScore = CalculateServiceRegistrationEfficiencyScore(
            serviceRegistrationDuration,
            moduleProfiles.Count);

        return new HealthPerformanceMetrics
        {
            AverageModuleSerialPhaseDurationMs = averageModuleSerialPhaseDuration,
            SlowestModuleSerialPhaseDurationMs = slowestModuleSerialPhaseDuration,
            SlowestModuleName = slowestModuleName,
            ServiceRegistrationDurationMs = serviceRegistrationDuration,
            ServiceRegistrationEfficiencyScore = efficiencyScore,
            MemoryUsageBytes = GC.GetTotalMemory(false) // Current memory usage.
        };
    }

    private static int CalculateServiceRegistrationEfficiencyScore(
        double serviceRegistrationDuration,
        int moduleCount)
    {
        if (moduleCount == 0) return 100;

        // Baseline: 100 ms per module, with 3 seconds as the minimum efficient total.
        var baselineTime = Math.Max(moduleCount * 100, 3000);
        
        if (serviceRegistrationDuration <= baselineTime)
        {
            return 100;
        }

        // Deduct points linearly once the baseline is exceeded.
        var score = Math.Max(0, 100 - (int)((serviceRegistrationDuration - baselineTime) / 100));
        return Math.Min(100, score);
    }

    #endregion
} 

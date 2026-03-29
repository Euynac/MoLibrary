using Monica.Core.Modularity.Dashboard.Interfaces;
using Monica.Core.Modularity.Dashboard.Models;
using Monica.Core.Modularity.Features;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.TypeFinder;

namespace Monica.Core.Modularity.Dashboard;

/// <summary>
/// Default implementation of <see cref="IModuleSystemStatusService"/>.
/// Provides status, performance, and dependency information for dashboards and monitoring.
/// </summary>
public class ModuleSystemStatusService : IModuleSystemStatusService
{
    /// <summary>
    /// Gets the overall module system status.
    /// </summary>
    /// <returns>The system status.</returns>
    public ModuleSystemStatus GetSystemStatus()
    {
        var enabledModules = MoModuleRegisterCentre.ModuleSnapshots.Count;
        var disabledModules = ModuleManager.GetDisabledModuleTypes().Count;
        var totalModules = enabledModules + disabledModules;
        var errorModules = MoModuleRegisterCentre.ModuleRegisterErrors.Count;

        var hasCircularDependencies = ModuleAnalyser.HasCircularDependencies();
        var hasRegistrationErrors = MoModuleRegisterCentre.ModuleRegisterErrors.Count > 0;

        var state = DetermineSystemState(enabledModules, errorModules, hasCircularDependencies);

        return new ModuleSystemStatus
        {
            IsInitialized = MoModuleRegisterCentre.ModuleSnapshots.Count > 0,
            TotalModules = totalModules,
            EnabledModules = enabledModules,
            DisabledModules = disabledModules,
            ErrorModules = errorModules,
            TotalInitializationTimeMs = ModuleProfiler.GetTotalElapsedMilliseconds(),
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
        var phaseDurations = ModuleProfiler.GetPhaseDurations();
        var phasePerformances = new List<PhasePerformanceInfo>();
        
        var order = 0;
        foreach (var (phaseName, duration) in phaseDurations)
        {
            phasePerformances.Add(new PhasePerformanceInfo
            {
                PhaseName = phaseName,
                DurationMs = duration,
                Order = order++
            });
        }

        var modulePerformances = new List<ModulePerformanceInfo>();
        var slowestModules = new List<ModulePerformanceInfo>();

        foreach (var snapshot in MoModuleRegisterCentre.ModuleSnapshots)
        {
            var profile = ModuleProfiler.GetModuleProfile(snapshot.ModuleType);
            if (profile != null)
            {
                var modulePerf = new ModulePerformanceInfo
                {
                    ModuleTypeName = snapshot.ModuleType.Name,
                    ModuleKey = snapshot.ModuleKey,
                    TotalDurationMs = profile.GetTotalDuration(),
                    PhaseDurations = profile.GetPhaseDurations()
                };
                modulePerformances.Add(modulePerf);
            }
        }

        slowestModules = modulePerformances
            .OrderByDescending(m => m.TotalDurationMs)
            .Take(5)
            .ToList();

        var configMethodStats = CalculateConfigMethodStatistics(modulePerformances);

        var totalSystemPhaseDuration = phaseDurations.Values.Sum();
        var totalModulePhaseDuration = modulePerformances.Sum(m => m.TotalDurationMs);

        return new ModuleSystemPerformance
        {
            TotalSystemInitializationTimeMs = ModuleProfiler.GetTotalElapsedMilliseconds(),
            PhasePerformances = phasePerformances,
            ModulePerformances = modulePerformances,
            SlowestModules = slowestModules,
            ConfigMethodStatistics = configMethodStats,
            TotalSystemPhaseDurationMs = totalSystemPhaseDuration,
            TotalModulePhaseDurationMs = totalModulePhaseDuration,
            SystemPhaseCount = phaseDurations.Count,
            TotalModulePhaseExecutions = modulePerformances.Sum(m => m.PhaseDurations.Count)
        };
    }

    /// <summary>
    /// Gets registration and dependency information for all modules.
    /// </summary>
    /// <returns>The registration information.</returns>
    public ModuleRegistrationInfo GetRegistrationInfo()
    {
        var enabledModules = new List<ModuleBasicInfo>();
        var disabledModules = new List<ModuleBasicInfo>();
        var modulesByOrder = new Dictionary<int, ModuleBasicInfo>();

        // Build the enabled-module list from runtime snapshots.
        foreach (var snapshot in MoModuleRegisterCentre.ModuleSnapshots.OrderBy(s => s.RegisterInfo.Order))
        {
            var basicInfo = CreateModuleBasicInfo(snapshot);
            enabledModules.Add(basicInfo);
            modulesByOrder[basicInfo.Order] = basicInfo;
        }

        // Build the disabled-module list from the module manager.
        var disabledModuleTypes = ModuleManager.GetDisabledModuleTypes();
        foreach (var moduleType in disabledModuleTypes)
        {
            var moduleKey = ModuleAnalyser.ResolveModuleKey(moduleType);

            var basicInfo = new ModuleBasicInfo
            {
                ModuleTypeName = moduleType.Name,
                ModuleFullTypeName = moduleType.FullName ?? moduleType.Name,
                ModuleKey = moduleKey,
                Order = int.MaxValue, // Disabled modules do not participate in registration ordering.
                Status = EMoModuleConfigMethods.Disabled,
                Dependencies = ModuleAnalyser.ModuleDependencyMap.TryGetValue(moduleKey, out var deps)
                    ? [.. deps]
                    : [],
                InitializationTimeMs = 0,
                IsDisabled = true,
                HasErrors = false
            };
            disabledModules.Add(basicInfo);
        }

        var totalInitTime = enabledModules.Sum(m => m.InitializationTimeMs);
        var slowestModules = enabledModules
            .Where(m => m.InitializationTimeMs > 0)
            .OrderByDescending(m => m.InitializationTimeMs)
            .Take(5)
            .ToList();

        var statistics = new ModuleRegistrationStatistics
        {
            TotalModules = enabledModules.Count + disabledModules.Count,
            EnabledModules = enabledModules.Count,
            DisabledModules = disabledModules.Count,
            TotalInitializationTimeMs = totalInitTime,
            SlowestModules = slowestModules
        };

        return new ModuleRegistrationInfo
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
        var snapshot = MoModuleRegisterCentre.ModuleSnapshots.FirstOrDefault(s => s.ModuleType == moduleType);
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
        var snapshot = MoModuleRegisterCentre.ModuleSnapshots.FirstOrDefault(s => s.ModuleKey == moduleKey);
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
        var graph = ModuleAnalyser.CalculateCompleteModuleDependencyGraph();
        var nodes = new List<ModuleDependencyNode>();
        var edges = new List<ModuleDependencyEdge>();
        var edgeKeys = new HashSet<(ModuleKey Source, ModuleKey Target, DependencyType Type)>();

        // Create graph nodes.
        foreach (var moduleKey in graph.Nodes)
        {
            var moduleType = ModuleAnalyser.ModuleKeyToTypeDict.GetValueOrDefault(moduleKey);

            var isEnabled = moduleType != null && !ModuleManager.IsModuleDisabled(moduleType);
            var status = GetModuleStatus(moduleKey, moduleType);

            var dependencies = ModuleAnalyser.CalculateModuleDependencies(moduleKey);
            var directDeps = ModuleAnalyser.ModuleDependencyMap.TryGetValue(moduleKey, out var directDependencies)
                ? directDependencies.Count
                : 0;

            var dependentCount = ModuleAnalyser.ModuleDependencyMap.Values.Count(deps => deps.Contains(moduleKey));
            var cyclePath = ModuleAnalyser.FindCycleInvolvingModule(moduleKey);

            nodes.Add(new ModuleDependencyNode
            {
                Module = moduleKey,
                ModuleName = moduleKey.ToString(),
                ModuleTypeName = moduleType?.Name ?? "Unknown",
                IsEnabled = isEnabled,
                IsUIModule = moduleKey.IsUIModule,
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
            var dependencyInfo = ModuleAnalyser.GetModuleDependencyInfo(source);

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
        return Mo.Options.GlobalTypeFinder.GetAssemblyAnalysis();
    }

    #region Private Helpers

    private static ModuleSystemState DetermineSystemState(int enabledModules, int errorModules, bool hasCircularDependencies)
    {
        if (errorModules > 0 || hasCircularDependencies)
        {
            return ModuleSystemState.Failed;
        }

        if (enabledModules == 0)
        {
            return ModuleSystemState.NotInitialized;
        }

        return ModuleSystemState.Initialized;
    }

    private static List<ConfigMethodStatistics> CalculateConfigMethodStatistics(List<ModulePerformanceInfo> modulePerformances)
    {
        var configMethodStats = new List<ConfigMethodStatistics>();
        var allPhases = Enum.GetValues<EMoModuleConfigMethods>();

        foreach (var phase in allPhases)
        {
            if (phase == EMoModuleConfigMethods.None) continue;

            var moduleData = modulePerformances
                .Where(m => m.PhaseDurations.ContainsKey(phase) && m.PhaseDurations[phase] > 0)
                .ToList();

            if (moduleData.Count == 0) continue;

            var totalDuration = moduleData.Sum(m => m.PhaseDurations[phase]);
            var averageDuration = totalDuration / moduleData.Count;
            var slowestModule = moduleData.OrderByDescending(m => m.PhaseDurations[phase]).First();

            configMethodStats.Add(new ConfigMethodStatistics
            {
                ConfigMethod = phase,
                TotalDurationMs = totalDuration,
                AverageDurationMs = averageDuration,
                ModuleCount = moduleData.Count,
                SlowestModuleName = slowestModule.ModuleTypeName,
                SlowestModuleDurationMs = slowestModule.PhaseDurations[phase]
            });
        }

        return configMethodStats;
    }

    private static ModuleBasicInfo CreateModuleBasicInfo(ModuleSnapshot snapshot)
    {
        var moduleKey = snapshot.ModuleKey;
        var dependencies = ModuleAnalyser.ModuleDependencyMap.TryGetValue(moduleKey, out var deps)
            ? deps.ToList()
            : [];

        var hasErrors = MoModuleRegisterCentre.ModuleRegisterErrors.Any(e => e.ModuleType == snapshot.ModuleType);

        return new ModuleBasicInfo
        {
            ModuleTypeName = snapshot.ModuleType.Name,
            ModuleFullTypeName = snapshot.ModuleType.FullName ?? snapshot.ModuleType.Name,
            ModuleKey = moduleKey,
            Order = snapshot.RegisterInfo.Order,
            Status = snapshot.RegisterInfo.ModulePhase,
            Dependencies = dependencies,
            InitializationTimeMs = snapshot.TotalInitializationDurationMs,
            IsDisabled = false,
            HasErrors = hasErrors
        };
    }

    private static ModuleDetailInfo CreateModuleDetailInfo(ModuleSnapshot snapshot)
    {
        var basicInfo = CreateModuleBasicInfo(snapshot);

        var profile = ModuleProfiler.GetModuleProfile(snapshot.ModuleType);
        var performanceInfo = new ModulePerformanceInfo
        {
            ModuleTypeName = snapshot.ModuleType.Name,
            ModuleKey = snapshot.ModuleKey,
            TotalDurationMs = profile?.GetTotalDuration() ?? 0,
            PhaseDurations = profile?.GetPhaseDurations() ?? []
        };

        var moduleKey = snapshot.ModuleKey;
        var dependencyInfo = ModuleAnalyser.GetModuleDependencyInfo(moduleKey);

        var configInfo = new ModuleConfigInfo
        {
            IsDisabled = false,
            ConfigurationItems = [], // This may later be populated from concrete module configuration data.
            ConfiguredOptions = CreateConfiguredOptions(snapshot.RegisterInfo),
            RegisterRequestCount = snapshot.RegisterInfo.RegisterRequests.Count,
            HasCircularDependency = dependencyInfo.IsPartOfCycle
        };

        var executionHistory = new List<ModulePhaseExecution>(); // This may later be populated from concrete execution history.

        var errors = MoModuleRegisterCentre.ModuleRegisterErrors
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

    private static List<ModuleConfiguredOption> CreateConfiguredOptions(ModuleRegisterInfo registerInfo)
    {
        return registerInfo.FinalConfigures
            .Where(static entry => typeof(IMoModuleOptionBase).IsAssignableFrom(entry.Key))
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

    private static EMoModuleConfigMethods GetModuleStatus(ModuleKey moduleKey, Type? moduleType)
    {
        if (moduleType == null || ModuleManager.IsModuleDisabled(moduleType))
        {
            return EMoModuleConfigMethods.Disabled;
        }

        var snapshot = MoModuleRegisterCentre.ModuleSnapshots.FirstOrDefault(s => s.ModuleKey == moduleKey);
        return snapshot?.RegisterInfo.ModulePhase ?? EMoModuleConfigMethods.None;
    }

    private static int CalculateModuleLayer(ModuleKey moduleKey)
    {
        // Use dependency count as the graph layer.
        var dependencies = ModuleAnalyser.CalculateModuleDependencies(moduleKey);
        return dependencies.Count;
    }

    private static DependencyType DetermineEdgeType(ModuleKey source, ModuleKey target)
    {
        // Check whether the edge is a direct dependency.
        if (ModuleAnalyser.ModuleDependencyMap.TryGetValue(source, out var directDeps) && directDeps.Contains(target))
        {
            // Distinguish direct edges that also participate in a cycle.
            var cyclePath = ModuleAnalyser.FindCycleInvolvingModule(source);
            if (cyclePath.Contains(target))
            {
                return DependencyType.Circular;
            }
            return DependencyType.Direct;
        }

        return DependencyType.Transitive;
    }

    private static bool IsEdgePartOfCycle(ModuleKey source, ModuleKey target)
    {
        var sourceCycle = ModuleAnalyser.FindCycleInvolvingModule(source);
        var targetCycle = ModuleAnalyser.FindCycleInvolvingModule(target);
        return sourceCycle.Count > 0 && targetCycle.Count > 0 &&
               sourceCycle.Contains(target) && targetCycle.Contains(source);
    }

    private static List<List<ModuleKey>> FindAllCircularPaths()
    {
        var circularPaths = new List<List<ModuleKey>>();
        var processedModules = new HashSet<ModuleKey>();

        foreach (var module in ModuleAnalyser.ModuleDependencyMap.Keys)
        {
            if (processedModules.Contains(module)) continue;

            var cyclePath = ModuleAnalyser.FindCycleInvolvingModule(module);
            if (cyclePath.Count > 0)
            {
                circularPaths.Add(cyclePath);
                processedModules.UnionWith(cyclePath);
            }
        }

        return circularPaths;
    }

    private static Dictionary<int, List<ModuleKey>> CalculateModuleLayers(List<ModuleDependencyNode> nodes)
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

    private static HealthCheckItem CheckSystemInitialization()
    {
        var isInitialized = MoModuleRegisterCentre.ModuleSnapshots.Count > 0;
        var status = isInitialized ? HealthStatus.Healthy : HealthStatus.Critical;
        var details = isInitialized 
            ? $"System initialized with {MoModuleRegisterCentre.ModuleSnapshots.Count} modules"
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

    private static HealthCheckItem CheckCircularDependencies(List<HealthIssue> issues)
    {
        var hasCircularDependencies = ModuleAnalyser.HasCircularDependencies();
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

    private static HealthCheckItem CheckModuleErrors(List<HealthIssue> issues)
    {
        var errorCount = MoModuleRegisterCentre.ModuleRegisterErrors.Count;
        var status = errorCount == 0 ? HealthStatus.Healthy : HealthStatus.Critical;
        var details = errorCount == 0 
            ? "No module registration errors found"
            : $"{errorCount} module registration errors detected";

        if (errorCount > 0)
        {
            foreach (var error in MoModuleRegisterCentre.ModuleRegisterErrors)
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

    private static HealthCheckItem CheckPerformanceIssues(List<HealthIssue> issues)
    {
        var totalInitTime = ModuleProfiler.GetTotalElapsedMilliseconds();
        var slowModules = ModuleProfiler.GetModuleProfilesSortedByTotalDuration().Take(3).ToList();
        
        var status = HealthStatus.Healthy;
        var details = $"Total initialization time: {totalInitTime}ms";

        // Performance thresholds.
        const long slowInitThreshold = 5000; // 5 seconds
        const long verySlowModuleThreshold = 1000; // 1 second

        if (totalInitTime > slowInitThreshold)
        {
            status = HealthStatus.Warning;
            issues.Add(new HealthIssue
            {
                Severity = IssueSeverity.Medium,
                Title = "Slow System Initialization",
                Description = $"System initialization took {totalInitTime}ms, which exceeds the recommended threshold",
                IssueType = IssueType.Performance,
                RecommendedAction = "Review module initialization logic and optimize slow modules"
            });
        }

        foreach (var module in slowModules.Where(m => m.GetTotalDuration() > verySlowModuleThreshold))
        {
            issues.Add(new HealthIssue
            {
                Severity = IssueSeverity.Low,
                Title = $"Slow Module: {module.ModuleType.Name}",
                Description = $"Module took {module.GetTotalDuration()}ms to initialize",
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

    private static HealthCheckItem CheckDisabledModules(List<HealthIssue> issues)
    {
        var disabledModules = ModuleManager.GetDisabledModuleTypes();
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

    private static void GenerateRecommendations(List<HealthIssue> issues, List<string> recommendations)
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

    private static HealthStatus DetermineOverallHealth(List<HealthCheckItem> healthCheckItems, List<HealthIssue> issues)
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

    private static string GenerateHealthSummary(HealthStatus overallHealth, List<HealthCheckItem> healthCheckItems, List<HealthIssue> issues)
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

    private static HealthPerformanceMetrics CalculateHealthPerformanceMetrics()
    {
        var moduleProfiles = ModuleProfiler.GetModuleProfilesSortedByTotalDuration();
        var totalInitTime = ModuleProfiler.GetTotalElapsedMilliseconds();
        
        var averageModuleInitTime = moduleProfiles.Count > 0 
            ? moduleProfiles.Average(p => p.GetTotalDuration()) 
            : 0;

        var slowestModule = moduleProfiles.FirstOrDefault();
        var slowestModuleInitTime = slowestModule?.GetTotalDuration() ?? 0;
        var slowestModuleName = slowestModule?.ModuleType.Name;

        // Calculate an efficiency score from 0-100 based on init time and module count.
        var efficiencyScore = CalculateEfficiencyScore(totalInitTime, moduleProfiles.Count);

        return new HealthPerformanceMetrics
        {
            AverageModuleInitTimeMs = averageModuleInitTime,
            SlowestModuleInitTimeMs = slowestModuleInitTime,
            SlowestModuleName = slowestModuleName,
            TotalSystemInitTimeMs = totalInitTime,
            InitializationEfficiencyScore = efficiencyScore,
            MemoryUsageBytes = GC.GetTotalMemory(false) // Current memory usage.
        };
    }

    private static int CalculateEfficiencyScore(long totalInitTime, int moduleCount)
    {
        if (moduleCount == 0) return 100;

        // Baseline: 100 ms per module, with 3 seconds as the minimum efficient total.
        var baselineTime = Math.Max(moduleCount * 100, 3000);
        
        if (totalInitTime <= baselineTime)
        {
            return 100;
        }

        // Deduct points linearly once the baseline is exceeded.
        var score = Math.Max(0, 100 - (int)((totalInitTime - baselineTime) / 100));
        return Math.Min(100, score);
    }

    #endregion
} 

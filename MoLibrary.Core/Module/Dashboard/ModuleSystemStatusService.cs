using MoLibrary.Core.Module.Dashboard.Interfaces;
using MoLibrary.Core.Module.Dashboard.Models;
using MoLibrary.Core.Module.Features;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Dashboard;

/// <summary>
/// 模块系统状态服务的实现类。
/// 提供模块系统状态、性能、依赖关系等信息，用于支持界面展示和系统监控。
/// </summary>
public class ModuleSystemStatusService : IModuleSystemStatusService
{
    /// <summary>
    /// 获取模块系统的整体状态信息。
    /// </summary>
    /// <returns>模块系统状态信息</returns>
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
    /// 获取模块系统的性能信息。
    /// </summary>
    /// <returns>模块系统性能信息</returns>
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
                    ModuleEnum = snapshot.ModuleEnum,
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
    /// 获取所有模块的注册和依赖关系信息。
    /// </summary>
    /// <returns>模块注册信息列表</returns>
    public ModuleRegistrationInfo GetRegistrationInfo()
    {
        var enabledModules = new List<ModuleBasicInfo>();
        var disabledModules = new List<ModuleBasicInfo>();
        var modulesByOrder = new Dictionary<int, ModuleBasicInfo>();

        // 处理启用的模块
        foreach (var snapshot in MoModuleRegisterCentre.ModuleSnapshots.OrderBy(s => s.RegisterInfo.Order))
        {
            var basicInfo = CreateModuleBasicInfo(snapshot);
            enabledModules.Add(basicInfo);
            modulesByOrder[basicInfo.Order] = basicInfo;
        }

        // 处理禁用的模块
        var disabledModuleTypes = ModuleManager.GetDisabledModuleTypes();
        foreach (var moduleType in disabledModuleTypes)
        {
            var moduleEnum = ModuleAnalyser.ModuleTypeToEnumMap.TryGetValue(moduleType, out var enumValue) 
                ? enumValue 
                : EMoModules.Developer;

            var basicInfo = new ModuleBasicInfo
            {
                ModuleTypeName = moduleType.Name,
                ModuleFullTypeName = moduleType.FullName ?? moduleType.Name,
                ModuleEnum = moduleEnum,
                Order = int.MaxValue, // 禁用模块没有顺序
                Status = EMoModuleConfigMethods.Disabled,
                Dependencies = ModuleAnalyser.ModuleDependencyMap.TryGetValue(moduleEnum, out var deps) 
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
    /// 获取指定模块的详细信息。
    /// </summary>
    /// <param name="moduleType">模块类型</param>
    /// <returns>模块详细信息，如果模块不存在则返回null</returns>
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
    /// 获取指定模块的详细信息。
    /// </summary>
    /// <param name="moduleEnum">模块枚举</param>
    /// <returns>模块详细信息，如果模块不存在则返回null</returns>
    public ModuleDetailInfo? GetModuleDetail(EMoModules moduleEnum)
    {
        var snapshot = MoModuleRegisterCentre.ModuleSnapshots.FirstOrDefault(s => s.ModuleEnum == moduleEnum);
        if (snapshot == null)
        {
            return null;
        }

        return CreateModuleDetailInfo(snapshot);
    }

    /// <summary>
    /// 获取模块依赖关系图信息。
    /// </summary>
    /// <returns>模块依赖关系图</returns>
    public ModuleDependencyGraph GetDependencyGraph()
    {
        var graph = ModuleAnalyser.CalculateCompleteModuleDependencyGraph();
        var nodes = new List<ModuleDependencyNode>();
        var edges = new List<ModuleDependencyEdge>();

        // 创建节点
        foreach (var module in graph.Nodes)
        {
            var moduleType = ModuleAnalyser.ModuleEnumToTypeDict.TryGetValue(module, out var type) 
                ? type 
                : null;

            var isEnabled = moduleType != null && !ModuleManager.IsModuleDisabled(moduleType);
            var status = GetModuleStatus(module, moduleType);

            var dependencies = ModuleAnalyser.CalculateModuleDependencies(module);
            var directDeps = ModuleAnalyser.ModuleDependencyMap.TryGetValue(module, out var directDependencies) 
                ? directDependencies.Count 
                : 0;

            var dependentCount = ModuleAnalyser.ModuleDependencyMap.Values.Count(deps => deps.Contains(module));
            var cyclePath = ModuleAnalyser.FindCycleInvolvingModule(module);

            nodes.Add(new ModuleDependencyNode
            {
                Module = module,
                ModuleName = module.ToString(),
                ModuleTypeName = moduleType?.Name ?? "Unknown",
                IsEnabled = isEnabled,
                DirectDependencyCount = directDeps,
                TotalDependencyCount = dependencies.Count,
                DependentModuleCount = dependentCount,
                Layer = CalculateModuleLayer(module),
                IsPartOfCycle = cyclePath.Count > 0,
                Status = status
            });
        }

        // 创建边
        foreach (var (source, target) in graph.Edges)
        {
            var dependencyType = DetermineEdgeType(source, target);
            var isPartOfCycle = IsEdgePartOfCycle(source, target);

            edges.Add(new ModuleDependencyEdge
            {
                SourceModule = source,
                TargetModule = target,
                DependencyType = dependencyType,
                IsPartOfCycle = isPartOfCycle
            });
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
    /// 获取模块系统的健康状态检查结果。
    /// </summary>
    /// <returns>健康状态检查结果</returns>
    public ModuleSystemHealthCheck GetHealthCheck()
    {
        var healthCheckItems = new List<HealthCheckItem>();
        var issues = new List<HealthIssue>();
        var recommendations = new List<string>();

        // 检查系统初始化状态
        healthCheckItems.Add(CheckSystemInitialization());

        // 检查循环依赖
        healthCheckItems.Add(CheckCircularDependencies(issues));

        // 检查模块错误
        healthCheckItems.Add(CheckModuleErrors(issues));

        // 检查性能问题
        healthCheckItems.Add(CheckPerformanceIssues(issues));

        // 检查禁用模块
        healthCheckItems.Add(CheckDisabledModules(issues));

        // 生成建议
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

    #region 私有帮助方法

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
        var dependencies = ModuleAnalyser.ModuleDependencyMap.TryGetValue(snapshot.ModuleEnum, out var deps) 
            ? deps.ToList()
            : [];

        var hasErrors = MoModuleRegisterCentre.ModuleRegisterErrors.Any(e => e.ModuleType == snapshot.ModuleType);

        return new ModuleBasicInfo
        {
            ModuleTypeName = snapshot.ModuleType.Name,
            ModuleFullTypeName = snapshot.ModuleType.FullName ?? snapshot.ModuleType.Name,
            ModuleEnum = snapshot.ModuleEnum,
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
            ModuleEnum = snapshot.ModuleEnum,
            TotalDurationMs = profile?.GetTotalDuration() ?? 0,
            PhaseDurations = profile?.GetPhaseDurations() ?? []
        };

        var dependencyInfo = ModuleAnalyser.GetModuleDependencyInfo(snapshot.ModuleEnum);

        var configInfo = new ModuleConfigInfo
        {
            IsDisabled = false,
            ConfigurationItems = [], // 这里可能需要从实际的模块配置中获取
            RegisterRequestCount = snapshot.RegisterInfo.RegisterRequests.Count,
            HasCircularDependency = dependencyInfo.IsPartOfCycle
        };

        var executionHistory = new List<ModulePhaseExecution>(); // 这里可能需要从实际的执行历史中获取

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

    private static EMoModuleConfigMethods GetModuleStatus(EMoModules moduleEnum, Type? moduleType)
    {
        if (moduleType == null || ModuleManager.IsModuleDisabled(moduleType))
        {
            return EMoModuleConfigMethods.Disabled;
        }

        var snapshot = MoModuleRegisterCentre.ModuleSnapshots.FirstOrDefault(s => s.ModuleEnum == moduleEnum);
        return snapshot?.RegisterInfo.ModulePhase ?? EMoModuleConfigMethods.None;
    }

    private static int CalculateModuleLayer(EMoModules module)
    {
        // 计算模块在依赖图中的层级
        var dependencies = ModuleAnalyser.CalculateModuleDependencies(module);
        return dependencies.Count;
    }

    private static DependencyType DetermineEdgeType(EMoModules source, EMoModules target)
    {
        // 检查是否是直接依赖
        if (ModuleAnalyser.ModuleDependencyMap.TryGetValue(source, out var directDeps) && directDeps.Contains(target))
        {
            // 检查是否是循环依赖的一部分
            var cyclePath = ModuleAnalyser.FindCycleInvolvingModule(source);
            if (cyclePath.Contains(target))
            {
                return DependencyType.Circular;
            }
            return DependencyType.Direct;
        }

        return DependencyType.Transitive;
    }

    private static bool IsEdgePartOfCycle(EMoModules source, EMoModules target)
    {
        var sourceCycle = ModuleAnalyser.FindCycleInvolvingModule(source);
        var targetCycle = ModuleAnalyser.FindCycleInvolvingModule(target);
        return sourceCycle.Count > 0 && targetCycle.Count > 0 && 
               sourceCycle.Contains(target) && targetCycle.Contains(source);
    }

    private static List<List<EMoModules>> FindAllCircularPaths()
    {
        var circularPaths = new List<List<EMoModules>>();
        var processedModules = new HashSet<EMoModules>();

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

    private static Dictionary<int, List<EMoModules>> CalculateModuleLayers(List<ModuleDependencyNode> nodes)
    {
        var layers = new Dictionary<int, List<EMoModules>>();

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
            ExecutionTimeMs = 0 // 这是一个快速检查
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
            ExecutionTimeMs = 1 // 快速检查
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

        // 定义性能阈值
        const long slowInitThreshold = 5000; // 5秒
        const long verySlowModuleThreshold = 1000; // 1秒

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

        // 计算效率评分（0-100），基于初始化时间和模块数量
        var efficiencyScore = CalculateEfficiencyScore(totalInitTime, moduleProfiles.Count);

        return new HealthPerformanceMetrics
        {
            AverageModuleInitTimeMs = averageModuleInitTime,
            SlowestModuleInitTimeMs = slowestModuleInitTime,
            SlowestModuleName = slowestModuleName,
            TotalSystemInitTimeMs = totalInitTime,
            InitializationEfficiencyScore = efficiencyScore,
            MemoryUsageBytes = GC.GetTotalMemory(false) // 当前内存使用量
        };
    }

    private static int CalculateEfficiencyScore(long totalInitTime, int moduleCount)
    {
        if (moduleCount == 0) return 100;

        // 基准：每个模块100ms，总时间不超过3秒认为是高效的
        var baselineTime = Math.Max(moduleCount * 100, 3000);
        
        if (totalInitTime <= baselineTime)
        {
            return 100;
        }

        // 线性减分，超出基准时间越多，分数越低
        var score = Math.Max(0, 100 - (int)((totalInitTime - baselineTime) / 100));
        return Math.Min(100, score);
    }

    #endregion
} 
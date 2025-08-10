using MoLibrary.Framework.Core.Model;
using MudBlazor;
using System.Linq;
using System.Reflection;

namespace MoLibrary.FrameworkUI.UIFrameworkMonitor.Components;

/// <summary>
/// 项目单元可视化配置类
/// 负责为D3.js图形提供节点样式、颜色、图标等配置信息
/// </summary>
public static class ProjectUnitVisualizationConfig
{
    /// <summary>
    /// 判断是否为复杂节点类型
    /// </summary>
    public static bool IsComplexUnitType(EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => true,
            EProjectUnitType.DomainService => true,
            EProjectUnitType.BackgroundWorker => true,
            EProjectUnitType.BackgroundJob => true,
            EProjectUnitType.HttpApi => true,
            EProjectUnitType.GrpcApi => true,
            _ => false
        };
    }

    /// <summary>
    /// 获取节点类型颜色
    /// </summary>
    public static string GetUnitTypeColor(EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => "#2196F3", // Blue
            EProjectUnitType.DomainService => "#4CAF50", // Green
            EProjectUnitType.Repository => "#FF9800", // Orange
            EProjectUnitType.DomainEvent => "#9C27B0", // Purple
            EProjectUnitType.DomainEventHandler => "#673AB7", // Deep Purple
            EProjectUnitType.LocalEventHandler => "#3F51B5", // Indigo
            EProjectUnitType.BackgroundWorker => "#00BCD4", // Cyan
            EProjectUnitType.BackgroundJob => "#009688", // Teal
            EProjectUnitType.HttpApi => "#F44336", // Red
            EProjectUnitType.GrpcApi => "#E91E63", // Pink
            EProjectUnitType.Entity => "#795548", // Brown
            EProjectUnitType.RequestDto => "#607D8B", // Blue Gray
            EProjectUnitType.Seeder => "#8BC34A", // Light Green
            EProjectUnitType.StateStore => "#FF5722", // Deep Orange
            EProjectUnitType.EventBus => "#3F51B5", // Indigo
            EProjectUnitType.Actor => "#00ACC1", // Light Blue
            _ => "#9E9E9E" // Gray
        };
    }

    /// <summary>
    /// 获取节点类型图标
    /// </summary>
    public static string GetUnitTypeIcon(EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => Icons.Material.Filled.BusinessCenter,
            EProjectUnitType.DomainService => Icons.Material.Filled.Domain,
            EProjectUnitType.Repository => Icons.Material.Filled.Storage,
            EProjectUnitType.DomainEvent => Icons.Material.Filled.Event,
            EProjectUnitType.DomainEventHandler => Icons.Material.Filled.EventAvailable,
            EProjectUnitType.LocalEventHandler => Icons.Material.Filled.EventNote,
            EProjectUnitType.BackgroundWorker => Icons.Material.Filled.Work,
            EProjectUnitType.BackgroundJob => Icons.Material.Filled.Schedule,
            EProjectUnitType.HttpApi => Icons.Material.Filled.Http,
            EProjectUnitType.GrpcApi => Icons.Material.Filled.Api,
            EProjectUnitType.Entity => Icons.Material.Filled.Dataset,
            EProjectUnitType.RequestDto => Icons.Material.Filled.DataObject,
            EProjectUnitType.Seeder => Icons.Material.Filled.Email,
            EProjectUnitType.StateStore => Icons.Material.Filled.Memory,
            EProjectUnitType.EventBus => Icons.Material.Filled.Hub,
            EProjectUnitType.Actor => Icons.Material.Filled.Person,
            _ => Icons.Material.Filled.Circle
        };
    }

    /// <summary>
    /// 获取节点类型的Chips
    /// </summary>
    public static object[] GetUnitTypeChips(DtoProjectUnit unit)
    {
        var chips = new List<object>
        {
            // 类型Chip - 不带图标，使用对应类型的颜色
            new
            {
                text = unit.UnitType.ToString(),
                color = GetUnitTypeColor(unit.UnitType),
                icon = "" // 不带图标
            }
        };

        // Author Chip
        if (!string.IsNullOrEmpty(unit.Author))
        {
            chips.Add(new
            {
                text = unit.Author,
                color = "dark",
                icon = Icons.Material.Filled.Person
            });
        }

        // 依赖Chip
        if (unit.DependencyUnits.Count > 0)
        {
            chips.Add(new
            {
                text = $"依赖 {unit.DependencyUnits.Count}",
                color = "info",
                icon = Icons.Material.Filled.Link
            });
        }
        
        // 被依赖Chip
        if (unit.DependedByCount > 0)
        {
            chips.Add(new
            {
                text = $"被依赖 {unit.DependedByCount}",
                color = "success",
                icon = Icons.Material.Filled.CallReceived
            });
        }

        // 特性Chip
        if (unit.Attributes.Any())
        {
            chips.Add(new
            {
                text = $"特性 {unit.Attributes.Count}",
                color = "secondary",
                icon = Icons.Material.Filled.Label
            });
        }
        
        // 方法Chip
        if (unit.Methods.Any())
        {
            chips.Add(new
            {
                text = $"方法 {unit.Methods.Count}",
                color = "primary",
                icon = Icons.Material.Filled.Functions
            });
        }
        
        // 告警Chips - 根据不同级别分别显示
        if (unit.Alerts.Any())
        {
            var errorCount = unit.Alerts.Count(a => a.Level == EAlertLevel.Error);
            var warningCount = unit.Alerts.Count(a => a.Level == EAlertLevel.Warning);
            var infoCount = unit.Alerts.Count(a => a.Level == EAlertLevel.Info);
            
            if (errorCount > 0)
            {
                chips.Add(new
                {
                    text = $"错误 {errorCount}",
                    color = "error",
                    icon = Icons.Material.Filled.Error
                });
            }
            
            if (warningCount > 0)
            {
                chips.Add(new
                {
                    text = $"警告 {warningCount}",
                    color = "warning",
                    icon = Icons.Material.Filled.Warning
                });
            }
            
            if (infoCount > 0)
            {
                chips.Add(new
                {
                    text = $"信息 {infoCount}",
                    color = "info",
                    icon = Icons.Material.Filled.Info
                });
            }
        }

        return chips.ToArray();
    }

    /// <summary>
    /// 获取节点元数据
    /// </summary>
    public static object[] GetUnitMetadata(DtoProjectUnit unit)
    {
        var metadata = new List<object>();

        // Group信息
        if (unit.Group?.Any() == true)
        {
            metadata.Add(new { key = "分组", value = string.Join(", ", unit.Group) });
        }

        // Description信息
        if (!string.IsNullOrEmpty(unit.Description))
        {
            metadata.Add(new { key = "描述", value = unit.Description });
        }

        // 可以根据需要添加更多元数据
        if (unit.Attributes.Any())
        {
            metadata.Add(new { key = "特性数量", value = unit.Attributes.Count.ToString() });
        }

        if (unit.DependencyUnits.Any())
        {
            metadata.Add(new { key = "依赖数量", value = unit.DependencyUnits.Count.ToString() });
        }
        
        // 显示方法名和注释（最多显示5个主要方法）
        if (unit.Methods.Any())
        {
            foreach (var method in unit.Methods.Take(5))
            {
                var methodInfo = !string.IsNullOrEmpty(method.Description) 
                    ? $"{method.MethodName}: {method.Description}" 
                    : method.MethodName;
                metadata.Add(new { key = "方法", value = methodInfo });
            }
            
            if (unit.Methods.Count > 5)
            {
                metadata.Add(new { key = "...", value = $"还有 {unit.Methods.Count - 5} 个方法" });
            }
        }

        return metadata.ToArray();
    }
    
    /// <summary>
    /// 获取节点的最高告警级别
    /// </summary>
    public static string? GetHighestAlertLevel(DtoProjectUnit unit)
    {
        if (!unit.Alerts.Any())
            return null;
            
        if (unit.Alerts.Any(a => a.Level == EAlertLevel.Error))
            return "error";
        if (unit.Alerts.Any(a => a.Level == EAlertLevel.Warning))
            return "warning";
        if (unit.Alerts.Any(a => a.Level == EAlertLevel.Info))
            return "info";
            
        return null;
    }
    
    /// <summary>
    /// 判断方法是否为异步方法
    /// </summary>
    private static bool IsAsyncMethod(MethodInfo method)
    {
        return method.ReturnType.IsGenericType && 
               (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>) || 
                method.ReturnType == typeof(Task));
    }
}
using MoLibrary.Framework.Core.Model;
using MudBlazor;

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
            // 类型Chip
            new
            {
                text = unit.UnitType.ToString(),
                color = "primary",
                icon = GetUnitTypeIcon(unit.UnitType)
            }
        };

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

        return chips.ToArray();
    }

    /// <summary>
    /// 获取节点元数据
    /// </summary>
    public static object[] GetUnitMetadata(DtoProjectUnit unit)
    {
        var metadata = new List<object>();

        // 可以根据需要添加更多元数据
        if (unit.Attributes.Any())
        {
            metadata.Add(new { key = "特性数量", value = unit.Attributes.Count.ToString() });
        }

        if (unit.DependencyUnits.Any())
        {
            metadata.Add(new { key = "依赖数量", value = unit.DependencyUnits.Count.ToString() });
        }

        return metadata.ToArray();
    }
}
using Monica.Framework.ProjectUnits.Models;
using MudBlazor;

namespace Monica.Framework.UI.UIProjectUnits.Components;

public static class ProjectUnitVisualizationConfig
{
    public static bool IsComplexUnitType(EProjectUnitType unitType)
    {
        return unitType is EProjectUnitType.ApplicationService
            or EProjectUnitType.DomainService
            or EProjectUnitType.RecurringJob
            or EProjectUnitType.TriggeredJob
            or EProjectUnitType.HttpApi
            or EProjectUnitType.GrpcApi;
    }

    public static string GetUnitTypeColor(EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => "#2196F3",
            EProjectUnitType.DomainService => "#4CAF50",
            EProjectUnitType.Repository => "#FF9800",
            EProjectUnitType.DomainEvent => "#9C27B0",
            EProjectUnitType.DomainEventHandler => "#673AB7",
            EProjectUnitType.LocalEventHandler => "#3F51B5",
            EProjectUnitType.RecurringJob => "#00BCD4",
            EProjectUnitType.TriggeredJob => "#009688",
            EProjectUnitType.HttpApi => "#F44336",
            EProjectUnitType.GrpcApi => "#E91E63",
            EProjectUnitType.Entity => "#795548",
            EProjectUnitType.RequestDto => "#607D8B",
            EProjectUnitType.Configuration => "#FFD54F",
            EProjectUnitType.Seeder => "#8BC34A",
            EProjectUnitType.StateStore => "#FF5722",
            EProjectUnitType.EventBus => "#3F51B5",
            EProjectUnitType.Actor => "#00ACC1",
            _ => "#9E9E9E"
        };
    }

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
            EProjectUnitType.RecurringJob => Icons.Material.Filled.Work,
            EProjectUnitType.TriggeredJob => Icons.Material.Filled.Schedule,
            EProjectUnitType.HttpApi => Icons.Material.Filled.Http,
            EProjectUnitType.GrpcApi => Icons.Material.Filled.Api,
            EProjectUnitType.Entity => Icons.Material.Filled.Dataset,
            EProjectUnitType.RequestDto => Icons.Material.Filled.DataObject,
            EProjectUnitType.Configuration => Icons.Material.Filled.Settings,
            EProjectUnitType.Seeder => Icons.Material.Filled.Email,
            EProjectUnitType.StateStore => Icons.Material.Filled.Memory,
            EProjectUnitType.EventBus => Icons.Material.Filled.Hub,
            EProjectUnitType.Actor => Icons.Material.Filled.Person,
            _ => Icons.Material.Filled.Circle
        };
    }

    public static string? GetHighestAlertLevel(DtoProjectUnit unit)
    {
        if (!unit.Alerts.Any())
        {
            return null;
        }

        if (unit.Alerts.Any(alert => alert.Level == EAlertLevel.Error))
        {
            return "error";
        }

        if (unit.Alerts.Any(alert => alert.Level == EAlertLevel.Warning))
        {
            return "warning";
        }

        return unit.Alerts.Any(alert => alert.Level == EAlertLevel.Info)
            ? "info"
            : null;
    }
}

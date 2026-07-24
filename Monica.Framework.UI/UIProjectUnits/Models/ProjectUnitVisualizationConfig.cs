using Monica.ProjectUnits.Models;
using MudBlazor;

namespace Monica.Framework.UI.UIProjectUnits.Models;

public static class ProjectUnitVisualizationConfig
{
    public static Color GetUnitTypeMudColor(EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => Color.Primary,
            EProjectUnitType.CrudApplicationService => Color.Primary,
            EProjectUnitType.DomainService => Color.Success,
            EProjectUnitType.Repository => Color.Warning,
            EProjectUnitType.DomainEvent => Color.Secondary,
            EProjectUnitType.DomainEventHandler => Color.Tertiary,
            EProjectUnitType.LocalEventHandler => Color.Info,
            EProjectUnitType.RecurringJob => Color.Info,
            EProjectUnitType.TriggeredJob => Color.Success,
            EProjectUnitType.HttpApi => Color.Error,
            EProjectUnitType.GrpcApi => Color.Secondary,
            EProjectUnitType.Entity => Color.Dark,
            EProjectUnitType.RequestDto => Color.Default,
            EProjectUnitType.Configuration => Color.Warning,
            EProjectUnitType.Seeder => Color.Success,
            EProjectUnitType.StateStore => Color.Error,
            EProjectUnitType.EventBus => Color.Info,
            EProjectUnitType.Actor => Color.Tertiary,
            _ => Color.Default
        };
    }

    public static bool IsComplexUnitType(EProjectUnitType unitType)
    {
        return unitType is EProjectUnitType.ApplicationService
            or EProjectUnitType.CrudApplicationService
            or EProjectUnitType.DomainService
            or EProjectUnitType.RecurringJob
            or EProjectUnitType.TriggeredJob
            or EProjectUnitType.HttpApi
            or EProjectUnitType.GrpcApi;
    }

    public static string GetUnitTypeColorRole(EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => "primary",
            EProjectUnitType.CrudApplicationService => "primary",
            EProjectUnitType.DomainService => "success",
            EProjectUnitType.Repository => "warning",
            EProjectUnitType.DomainEvent => "secondary",
            EProjectUnitType.DomainEventHandler => "tertiary",
            EProjectUnitType.LocalEventHandler => "info",
            EProjectUnitType.RecurringJob => "info",
            EProjectUnitType.TriggeredJob => "success",
            EProjectUnitType.HttpApi => "error",
            EProjectUnitType.GrpcApi => "secondary",
            EProjectUnitType.Entity => "dark",
            EProjectUnitType.RequestDto => "default",
            EProjectUnitType.Configuration => "warning",
            EProjectUnitType.Seeder => "success",
            EProjectUnitType.StateStore => "error",
            EProjectUnitType.EventBus => "info",
            EProjectUnitType.Actor => "tertiary",
            _ => "default"
        };
    }

    public static string GetUnitTypeIcon(EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => Icons.Material.Filled.BusinessCenter,
            EProjectUnitType.CrudApplicationService => Icons.Material.Filled.EditNote,
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

    public static string? GetHighestAlertLevel(ProjectUnitSummary unit)
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

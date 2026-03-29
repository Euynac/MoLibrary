using Microsoft.Extensions.Localization;
using Monica.Framework.Core.Model;
using Monica.Framework.UI.UIFrameworkMonitor.Components;
using MudBlazor;

namespace Monica.Framework.UI.Localization;

public enum FrameworkMonitorCountText
{
    Assembly,
    Enum,
    EnumValue,
    ProjectUnit,
    Dependency,
    DependedBy,
    Attribute,
    Method,
    Error,
    Warning,
    Info,
    Match
}

public enum FrameworkMonitorMethodModifier
{
    Static,
    Virtual,
    Abstract,
    Async
}

public static class FrameworkMonitorLocalizationExtensions
{
    public static string GetProjectUnitTypeText(this IStringLocalizer<FrameworkMonitorResource> localizer, EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => localizer["Shared:ProjectUnitTypes:ApplicationService"].Value,
            EProjectUnitType.DomainService => localizer["Shared:ProjectUnitTypes:DomainService"].Value,
            EProjectUnitType.Repository => localizer["Shared:ProjectUnitTypes:Repository"].Value,
            EProjectUnitType.DomainEvent => localizer["Shared:ProjectUnitTypes:DomainEvent"].Value,
            EProjectUnitType.DomainEventHandler => localizer["Shared:ProjectUnitTypes:DomainEventHandler"].Value,
            EProjectUnitType.LocalEventHandler => localizer["Shared:ProjectUnitTypes:LocalEventHandler"].Value,
            EProjectUnitType.Seeder => localizer["Shared:ProjectUnitTypes:Seeder"].Value,
            EProjectUnitType.RecurringJob => localizer["Shared:ProjectUnitTypes:RecurringJob"].Value,
            EProjectUnitType.TriggeredJob => localizer["Shared:ProjectUnitTypes:TriggeredJob"].Value,
            EProjectUnitType.HttpApi => localizer["Shared:ProjectUnitTypes:HttpApi"].Value,
            EProjectUnitType.GrpcApi => localizer["Shared:ProjectUnitTypes:GrpcApi"].Value,
            EProjectUnitType.StateStore => localizer["Shared:ProjectUnitTypes:StateStore"].Value,
            EProjectUnitType.EventBus => localizer["Shared:ProjectUnitTypes:EventBus"].Value,
            EProjectUnitType.Actor => localizer["Shared:ProjectUnitTypes:Actor"].Value,
            EProjectUnitType.Entity => localizer["Shared:ProjectUnitTypes:Entity"].Value,
            EProjectUnitType.RequestDto => localizer["Shared:ProjectUnitTypes:RequestDto"].Value,
            EProjectUnitType.Configuration => localizer["Shared:ProjectUnitTypes:Configuration"].Value,
            _ => unitType.ToString()
        };
    }

    public static string GetCountText(this IStringLocalizer<FrameworkMonitorResource> localizer, FrameworkMonitorCountText countText, int count)
    {
        var singular = count == 1;

        return countText switch
        {
            FrameworkMonitorCountText.Assembly => singular
                ? localizer["Shared:Counts:Assembly:Singular", count].Value
                : localizer["Shared:Counts:Assembly:Plural", count].Value,
            FrameworkMonitorCountText.Enum => singular
                ? localizer["Shared:Counts:Enum:Singular", count].Value
                : localizer["Shared:Counts:Enum:Plural", count].Value,
            FrameworkMonitorCountText.EnumValue => singular
                ? localizer["Shared:Counts:EnumValue:Singular", count].Value
                : localizer["Shared:Counts:EnumValue:Plural", count].Value,
            FrameworkMonitorCountText.ProjectUnit => singular
                ? localizer["Shared:Counts:ProjectUnit:Singular", count].Value
                : localizer["Shared:Counts:ProjectUnit:Plural", count].Value,
            FrameworkMonitorCountText.Dependency => singular
                ? localizer["Shared:Counts:Dependency:Singular", count].Value
                : localizer["Shared:Counts:Dependency:Plural", count].Value,
            FrameworkMonitorCountText.DependedBy => singular
                ? localizer["Shared:Counts:DependedBy:Singular", count].Value
                : localizer["Shared:Counts:DependedBy:Plural", count].Value,
            FrameworkMonitorCountText.Attribute => singular
                ? localizer["Shared:Counts:Attribute:Singular", count].Value
                : localizer["Shared:Counts:Attribute:Plural", count].Value,
            FrameworkMonitorCountText.Method => singular
                ? localizer["Shared:Counts:Method:Singular", count].Value
                : localizer["Shared:Counts:Method:Plural", count].Value,
            FrameworkMonitorCountText.Error => singular
                ? localizer["Shared:Counts:Error:Singular", count].Value
                : localizer["Shared:Counts:Error:Plural", count].Value,
            FrameworkMonitorCountText.Warning => singular
                ? localizer["Shared:Counts:Warning:Singular", count].Value
                : localizer["Shared:Counts:Warning:Plural", count].Value,
            FrameworkMonitorCountText.Info => singular
                ? localizer["Shared:Counts:Info:Singular", count].Value
                : localizer["Shared:Counts:Info:Plural", count].Value,
            FrameworkMonitorCountText.Match => singular
                ? localizer["Shared:Counts:Match:Singular", count].Value
                : localizer["Shared:Counts:Match:Plural", count].Value,
            _ => count.ToString()
        };
    }

    public static string GetAlertLevelText(this IStringLocalizer<FrameworkMonitorResource> localizer, EAlertLevel alertLevel)
    {
        return alertLevel switch
        {
            EAlertLevel.Info => localizer["Shared:AlertLevels:Info"].Value,
            EAlertLevel.Warning => localizer["Shared:AlertLevels:Warning"].Value,
            EAlertLevel.Error => localizer["Shared:AlertLevels:Error"].Value,
            _ => alertLevel.ToString()
        };
    }

    public static string GetConfigurationModeText(this IStringLocalizer<FrameworkMonitorResource> localizer, bool? isOffline)
    {
        return isOffline switch
        {
            true => localizer["Shared:ConfigurationModes:Offline"].Value,
            false => localizer["Shared:ConfigurationModes:Online"].Value,
            null => localizer["Shared:ConfigurationModes:Unknown"].Value
        };
    }

    public static string GetConfigurationUsageText(this IStringLocalizer<FrameworkMonitorResource> localizer, EConfigurationUsageType usage)
    {
        return usage switch
        {
            EConfigurationUsageType.Offline => localizer["Shared:ConfigurationUsageTypes:Offline"].Value,
            EConfigurationUsageType.OnlineSnapshot => localizer["Shared:ConfigurationUsageTypes:OnlineSnapshot"].Value,
            EConfigurationUsageType.OnlineMonitor => localizer["Shared:ConfigurationUsageTypes:OnlineMonitor"].Value,
            EConfigurationUsageType.Unknown => localizer["Shared:ConfigurationUsageTypes:Unknown"].Value,
            _ => localizer["Shared:ConfigurationUsageTypes:Unknown"].Value
        };
    }

    public static string GetMethodModifierText(this IStringLocalizer<FrameworkMonitorResource> localizer, FrameworkMonitorMethodModifier modifier)
    {
        return modifier switch
        {
            FrameworkMonitorMethodModifier.Static => localizer["Shared:MethodModifiers:Static"].Value,
            FrameworkMonitorMethodModifier.Virtual => localizer["Shared:MethodModifiers:Virtual"].Value,
            FrameworkMonitorMethodModifier.Abstract => localizer["Shared:MethodModifiers:Abstract"].Value,
            FrameworkMonitorMethodModifier.Async => localizer["Shared:MethodModifiers:Async"].Value,
            _ => modifier.ToString()
        };
    }

    public static string BuildProjectUnitGraphTooltip(this IStringLocalizer<FrameworkMonitorResource> localizer, DtoProjectUnit unit)
    {
        var lines = new[]
        {
            unit.Title,
            localizer["ProjectUnitVisualization:Graph:Tooltip:Type", localizer.GetProjectUnitTypeText(unit.UnitType)].Value,
            localizer["ProjectUnitVisualization:Graph:Tooltip:Dependencies", unit.DependencyUnits.Count].Value,
            localizer["ProjectUnitVisualization:Graph:Tooltip:DependedBy", unit.DependedByCount].Value
        };

        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildProjectUnitGraphSummary(this IStringLocalizer<FrameworkMonitorResource> localizer, DtoProjectUnit unit)
    {
        var parts = new List<string>();

        if (unit.DependencyUnits.Count > 0)
        {
            parts.Add(localizer["ProjectUnitVisualization:Graph:Summary:Dependencies", unit.DependencyUnits.Count].Value);
        }

        if (unit.DependedByCount > 0)
        {
            parts.Add(localizer["ProjectUnitVisualization:Graph:Summary:DependedBy", unit.DependedByCount].Value);
        }

        return string.Join(" | ", parts);
    }

    public static object[] BuildProjectUnitGraphChips(this IStringLocalizer<FrameworkMonitorResource> localizer, DtoProjectUnit unit)
    {
        var chips = new List<object>
        {
            new
            {
                text = localizer.GetProjectUnitTypeText(unit.UnitType),
                color = ProjectUnitVisualizationConfig.GetUnitTypeColor(unit.UnitType),
                icon = string.Empty
            }
        };

        if (!string.IsNullOrWhiteSpace(unit.Author))
        {
            chips.Add(new
            {
                text = unit.Author,
                color = "dark",
                icon = Icons.Material.Filled.Person
            });
        }

        if (unit.DependencyUnits.Count > 0)
        {
            chips.Add(new
            {
                text = localizer.GetCountText(FrameworkMonitorCountText.Dependency, unit.DependencyUnits.Count),
                color = "info",
                icon = Icons.Material.Filled.Link
            });
        }

        if (unit.DependedByCount > 0)
        {
            chips.Add(new
            {
                text = localizer.GetCountText(FrameworkMonitorCountText.DependedBy, unit.DependedByCount),
                color = "success",
                icon = Icons.Material.Filled.CallReceived
            });
        }

        if (unit.Attributes.Any())
        {
            chips.Add(new
            {
                text = localizer.GetCountText(FrameworkMonitorCountText.Attribute, unit.Attributes.Count),
                color = "secondary",
                icon = Icons.Material.Filled.Label
            });
        }

        if (unit.Methods.Any())
        {
            chips.Add(new
            {
                text = localizer.GetCountText(FrameworkMonitorCountText.Method, unit.Methods.Count),
                color = "primary",
                icon = Icons.Material.Filled.Functions
            });
        }

        if (unit.Alerts.Any())
        {
            var errorCount = unit.Alerts.Count(alert => alert.Level == EAlertLevel.Error);
            var warningCount = unit.Alerts.Count(alert => alert.Level == EAlertLevel.Warning);
            var infoCount = unit.Alerts.Count(alert => alert.Level == EAlertLevel.Info);

            if (errorCount > 0)
            {
                chips.Add(new
                {
                    text = localizer.GetCountText(FrameworkMonitorCountText.Error, errorCount),
                    color = "error",
                    icon = Icons.Material.Filled.Error
                });
            }

            if (warningCount > 0)
            {
                chips.Add(new
                {
                    text = localizer.GetCountText(FrameworkMonitorCountText.Warning, warningCount),
                    color = "warning",
                    icon = Icons.Material.Filled.Warning
                });
            }

            if (infoCount > 0)
            {
                chips.Add(new
                {
                    text = localizer.GetCountText(FrameworkMonitorCountText.Info, infoCount),
                    color = "info",
                    icon = Icons.Material.Filled.Info
                });
            }
        }

        return chips.ToArray();
    }

    public static object[] BuildProjectUnitGraphMetadata(this IStringLocalizer<FrameworkMonitorResource> localizer, DtoProjectUnit unit)
    {
        var metadata = new List<object>();

        if (unit.Group?.Any() == true)
        {
            metadata.Add(new
            {
                kind = "group",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:Group"].Value,
                value = string.Join(", ", unit.Group)
            });
        }

        if (!string.IsNullOrWhiteSpace(unit.Description))
        {
            metadata.Add(new
            {
                kind = "description",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:Description"].Value,
                value = unit.Description
            });
        }

        if (unit.Attributes.Any())
        {
            metadata.Add(new
            {
                kind = "attribute-count",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:AttributeCount"].Value,
                value = unit.Attributes.Count.ToString()
            });
        }

        if (unit.DependencyUnits.Any())
        {
            metadata.Add(new
            {
                kind = "dependency-count",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:DependencyCount"].Value,
                value = unit.DependencyUnits.Count.ToString()
            });
        }

        if (unit.Methods.Any())
        {
            foreach (var method in unit.Methods.Take(5))
            {
                var methodInfo = string.IsNullOrWhiteSpace(method.Description)
                    ? method.MethodName
                    : $"{method.MethodName}: {method.Description}";

                metadata.Add(new
                {
                    kind = "method",
                    key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:Method"].Value,
                    value = methodInfo
                });
            }

            if (unit.Methods.Count > 5)
            {
                metadata.Add(new
                {
                    kind = "more-methods",
                    key = "...",
                    value = localizer["ProjectUnitVisualization:Graph:Metadata:MoreMethods", unit.Methods.Count - 5].Value
                });
            }
        }

        return metadata.ToArray();
    }
}

using Microsoft.Extensions.Localization;
using Monica.ProjectUnits.Models;
using Monica.Framework.UI.UIProjectUnits.Models;
using MudBlazor;

namespace Monica.Framework.UI.Localization;

public enum ProjectUnitsCountText
{
    Assembly,
    Enum,
    EnumValue,
    ProjectUnit,
    Dependency,
    DependedBy,
    Requirement,
    Method,
    Error,
    Warning,
    Info,
    Match
}

public enum ProjectUnitsMethodModifier
{
    Static,
    Virtual,
    Abstract,
    Async
}

public static class ProjectUnitsLocalizationExtensions
{
    public static string GetProjectUnitTypeText(this IStringLocalizer<ProjectUnitsResource> localizer, EProjectUnitType unitType)
    {
        return unitType switch
        {
            EProjectUnitType.ApplicationService => localizer["Shared:ProjectUnitTypes:ApplicationService"].Value,
            EProjectUnitType.CrudApplicationService => localizer["Shared:ProjectUnitTypes:CrudApplicationService"].Value,
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

    public static string GetCountText(this IStringLocalizer<ProjectUnitsResource> localizer, ProjectUnitsCountText countText, int count)
    {
        var singular = count == 1;

        return countText switch
        {
            ProjectUnitsCountText.Assembly => singular
                ? localizer["Shared:Counts:Assembly:Singular", count].Value
                : localizer["Shared:Counts:Assembly:Plural", count].Value,
            ProjectUnitsCountText.Enum => singular
                ? localizer["Shared:Counts:Enum:Singular", count].Value
                : localizer["Shared:Counts:Enum:Plural", count].Value,
            ProjectUnitsCountText.EnumValue => singular
                ? localizer["Shared:Counts:EnumValue:Singular", count].Value
                : localizer["Shared:Counts:EnumValue:Plural", count].Value,
            ProjectUnitsCountText.ProjectUnit => singular
                ? localizer["Shared:Counts:ProjectUnit:Singular", count].Value
                : localizer["Shared:Counts:ProjectUnit:Plural", count].Value,
            ProjectUnitsCountText.Dependency => singular
                ? localizer["Shared:Counts:Dependency:Singular", count].Value
                : localizer["Shared:Counts:Dependency:Plural", count].Value,
            ProjectUnitsCountText.DependedBy => singular
                ? localizer["Shared:Counts:DependedBy:Singular", count].Value
                : localizer["Shared:Counts:DependedBy:Plural", count].Value,
            ProjectUnitsCountText.Requirement => singular
                ? localizer["Shared:Counts:Requirement:Singular", count].Value
                : localizer["Shared:Counts:Requirement:Plural", count].Value,
            ProjectUnitsCountText.Method => singular
                ? localizer["Shared:Counts:Method:Singular", count].Value
                : localizer["Shared:Counts:Method:Plural", count].Value,
            ProjectUnitsCountText.Error => singular
                ? localizer["Shared:Counts:Error:Singular", count].Value
                : localizer["Shared:Counts:Error:Plural", count].Value,
            ProjectUnitsCountText.Warning => singular
                ? localizer["Shared:Counts:Warning:Singular", count].Value
                : localizer["Shared:Counts:Warning:Plural", count].Value,
            ProjectUnitsCountText.Info => singular
                ? localizer["Shared:Counts:Info:Singular", count].Value
                : localizer["Shared:Counts:Info:Plural", count].Value,
            ProjectUnitsCountText.Match => singular
                ? localizer["Shared:Counts:Match:Singular", count].Value
                : localizer["Shared:Counts:Match:Plural", count].Value,
            _ => count.ToString()
        };
    }

    public static string GetAlertLevelText(this IStringLocalizer<ProjectUnitsResource> localizer, EAlertLevel alertLevel)
    {
        return alertLevel switch
        {
            EAlertLevel.Info => localizer["Shared:AlertLevels:Info"].Value,
            EAlertLevel.Warning => localizer["Shared:AlertLevels:Warning"].Value,
            EAlertLevel.Error => localizer["Shared:AlertLevels:Error"].Value,
            _ => alertLevel.ToString()
        };
    }

    public static string GetConfigurationModeText(this IStringLocalizer<ProjectUnitsResource> localizer, bool? isOffline)
    {
        return isOffline switch
        {
            true => localizer["Shared:ConfigurationModes:Offline"].Value,
            false => localizer["Shared:ConfigurationModes:Online"].Value,
            null => localizer["Shared:ConfigurationModes:Unknown"].Value
        };
    }

    public static string GetConfigurationUsageText(this IStringLocalizer<ProjectUnitsResource> localizer, EConfigurationUsageType usage)
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

    public static string GetMethodModifierText(this IStringLocalizer<ProjectUnitsResource> localizer, ProjectUnitsMethodModifier modifier)
    {
        return modifier switch
        {
            ProjectUnitsMethodModifier.Static => localizer["Shared:MethodModifiers:Static"].Value,
            ProjectUnitsMethodModifier.Virtual => localizer["Shared:MethodModifiers:Virtual"].Value,
            ProjectUnitsMethodModifier.Abstract => localizer["Shared:MethodModifiers:Abstract"].Value,
            ProjectUnitsMethodModifier.Async => localizer["Shared:MethodModifiers:Async"].Value,
            _ => modifier.ToString()
        };
    }

    public static string BuildProjectUnitGraphTooltip(this IStringLocalizer<ProjectUnitsResource> localizer, ProjectUnitSummary unit)
    {
        var lines = new[]
        {
            unit.Title,
            localizer["ProjectUnitVisualization:Graph:Tooltip:Type", localizer.GetProjectUnitTypeText(unit.UnitType)].Value,
            localizer["ProjectUnitVisualization:Graph:Tooltip:Dependencies", unit.Dependencies.Count].Value,
            localizer["ProjectUnitVisualization:Graph:Tooltip:DependedBy", unit.DependedByCount].Value
        };

        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildProjectUnitGraphSummary(this IStringLocalizer<ProjectUnitsResource> localizer, ProjectUnitSummary unit)
    {
        var parts = new List<string>();

        if (unit.Dependencies.Count > 0)
        {
            parts.Add(localizer["ProjectUnitVisualization:Graph:Summary:Dependencies", unit.Dependencies.Count].Value);
        }

        if (unit.DependedByCount > 0)
        {
            parts.Add(localizer["ProjectUnitVisualization:Graph:Summary:DependedBy", unit.DependedByCount].Value);
        }

        return string.Join(" | ", parts);
    }

    public static object[] BuildProjectUnitGraphChips(this IStringLocalizer<ProjectUnitsResource> localizer, ProjectUnitSummary unit)
    {
        var chips = new List<object>
        {
            new
            {
                text = localizer.GetProjectUnitTypeText(unit.UnitType),
                color = ProjectUnitVisualizationConfig.GetUnitTypeColorRole(unit.UnitType),
                icon = string.Empty
            }
        };

        if (!string.IsNullOrWhiteSpace(unit.Owner))
        {
            chips.Add(new
            {
                text = unit.Owner,
                color = "dark",
                icon = Icons.Material.Filled.Person
            });
        }

        if (unit.Dependencies.Count > 0)
        {
            chips.Add(new
            {
                text = localizer.GetCountText(ProjectUnitsCountText.Dependency, unit.Dependencies.Count),
                color = "info",
                icon = Icons.Material.Filled.Link
            });
        }

        if (unit.DependedByCount > 0)
        {
            chips.Add(new
            {
                text = localizer.GetCountText(ProjectUnitsCountText.DependedBy, unit.DependedByCount),
                color = "success",
                icon = Icons.Material.Filled.CallReceived
            });
        }

        if (unit.RequirementCount > 0)
        {
            chips.Add(new
            {
                text = localizer.GetCountText(ProjectUnitsCountText.Requirement, unit.RequirementCount),
                color = "secondary",
                icon = Icons.Material.Filled.AssignmentTurnedIn
            });
        }

        if (unit.MethodCount > 0)
        {
            chips.Add(new
            {
                text = localizer.GetCountText(ProjectUnitsCountText.Method, unit.MethodCount),
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
                    text = localizer.GetCountText(ProjectUnitsCountText.Error, errorCount),
                    color = "error",
                    icon = Icons.Material.Filled.Error
                });
            }

            if (warningCount > 0)
            {
                chips.Add(new
                {
                    text = localizer.GetCountText(ProjectUnitsCountText.Warning, warningCount),
                    color = "warning",
                    icon = Icons.Material.Filled.Warning
                });
            }

            if (infoCount > 0)
            {
                chips.Add(new
                {
                    text = localizer.GetCountText(ProjectUnitsCountText.Info, infoCount),
                    color = "info",
                    icon = Icons.Material.Filled.Info
                });
            }
        }

        return chips.ToArray();
    }

    public static object[] BuildProjectUnitGraphMetadata(this IStringLocalizer<ProjectUnitsResource> localizer, ProjectUnitSummary unit)
    {
        var metadata = new List<object>();

        if (unit.Tags.Count != 0)
        {
            metadata.Add(new
            {
                kind = "tags",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:Tags"].Value,
                value = string.Join(", ", unit.Tags)
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

        if (!string.IsNullOrWhiteSpace(unit.Owner))
        {
            metadata.Add(new
            {
                kind = "owner",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:Owner"].Value,
                value = unit.Owner
            });
        }

        if (unit.RequirementCount > 0)
        {
            metadata.Add(new
            {
                kind = "requirement-count",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:RequirementCount"].Value,
                value = unit.RequirementCount.ToString()
            });
        }

        if (unit.Dependencies.Count > 0)
        {
            metadata.Add(new
            {
                kind = "dependency-count",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:DependencyCount"].Value,
                value = unit.Dependencies.Count.ToString()
            });
        }

        if (unit.MethodCount > 0)
        {
            metadata.Add(new
            {
                kind = "method-count",
                key = localizer["ProjectUnitVisualization:Graph:MetadataLabels:MethodCount"].Value,
                value = unit.MethodCount.ToString()
            });
        }

        return metadata.ToArray();
    }
}

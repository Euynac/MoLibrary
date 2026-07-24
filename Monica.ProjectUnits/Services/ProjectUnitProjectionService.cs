using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.ProjectUnits.Abstractions;
using Monica.ProjectUnits.Models;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Services;

internal sealed class ProjectUnitProjectionService(
    IProjectUnitCatalog catalog,
    IMonicaApplicationOptions applicationOptions,
    IProjectUnitRequirementLinkResolver requirementLinkResolver,
    ILogger<ProjectUnitProjectionService> logger)
{
    internal List<ProjectUnitSummary> GetAllProjectUnits()
    {
        var units = catalog.GetAllUnits();
        var dependedByCounts = BuildDependedByCounts(units);
        return units
            .Select(unit => CreateSummary(unit, dependedByCounts))
            .OrderBy(unit => unit.UnitType)
            .ThenBy(unit => unit.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal ProjectUnitDashboardSnapshot GetDashboard()
    {
        var units = catalog.GetAllUnits();
        var dependedByCounts = BuildDependedByCounts(units);
        var summaries = units.ToDictionary(
            unit => unit.Key,
            unit => CreateSummary(unit, dependedByCounts),
            StringComparer.Ordinal);
        var totalUnits = units.Count;

        return new ProjectUnitDashboardSnapshot
        {
            Service = CreateServiceIdentity(),
            TotalUnits = totalUnits,
            DistinctUnitTypes = units.Select(unit => unit.UnitType).Distinct().Count(),
            Coverage =
            [
                CreateCoverageMetric(
                    ProjectUnitCoverageKind.Metadata,
                    units.Count(unit => unit.HasExplicitMetadata),
                    totalUnits),
                CreateCoverageMetric(
                    ProjectUnitCoverageKind.Description,
                    units.Count(unit => !string.IsNullOrWhiteSpace(unit.Description)),
                    totalUnits),
                CreateCoverageMetric(
                    ProjectUnitCoverageKind.Ownership,
                    units.Count(unit => !string.IsNullOrWhiteSpace(unit.Owner)),
                    totalUnits),
                CreateCoverageMetric(
                    ProjectUnitCoverageKind.Requirements,
                    units.Count(unit => unit.RequirementIds.Count != 0),
                    totalUnits)
            ],
            UnitTypes = units
                .GroupBy(unit => unit.UnitType)
                .Select(group => new ProjectUnitTypeStatistics
                {
                    UnitType = group.Key,
                    Count = group.Count(),
                    Percentage = CalculatePercentage(group.Count(), totalUnits) ?? 0m
                })
                .OrderByDescending(statistics => statistics.Count)
                .ThenBy(statistics => statistics.UnitType)
                .ToList(),
            Dependencies = new ProjectUnitDependencyStatistics
            {
                EdgeCount = units.Sum(unit => unit.DependencyUnits.Count),
                IsolatedUnitCount = units.Count(unit =>
                    unit.DependencyUnits.Count == 0 && !dependedByCounts.ContainsKey(unit.Key))
            },
            Alerts = CreateAlertStatistics(units),
            CoverageGaps = units
                .Select(unit => CreateCoverageGap(unit, summaries[unit.Key]))
                .Where(gap => gap.MissingCoverage.Count != 0 || gap.Unit.Alerts.Count != 0)
                .OrderByDescending(gap => gap.Unit.Alerts.Any(alert => alert.Level == EAlertLevel.Error))
                .ThenByDescending(gap => gap.Unit.Alerts.Any(alert => alert.Level == EAlertLevel.Warning))
                .ThenByDescending(gap => gap.MissingCoverage.Count)
                .ThenBy(gap => gap.Unit.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    internal async Task<ProjectUnitDetail> GetProjectUnitDetailAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (catalog.FindByFullName(key) is not { } unit)
        {
            throw new KeyNotFoundException($"Project unit '{key}' was not found in the current host's catalog.");
        }

        var units = catalog.GetAllUnits();
        var dependedByCounts = BuildDependedByCounts(units);

        return new ProjectUnitDetail
        {
            Summary = CreateSummary(unit, dependedByCounts),
            Dependents = units
                .Where(candidate => candidate.DependencyUnits.Contains(unit))
                .Select(CreateReference)
                .OrderBy(reference => reference.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Requirements = await ResolveRequirementsAsync(unit.RequirementIds, cancellationToken),
            Methods = unit.Methods
                .Select(method => new ProjectUnitMethodSummary
                {
                    Name = method.MethodName,
                    Signature = method.MethodSignature,
                    Description = Normalize(method.Description),
                    IsStatic = method.MethodInfo.IsStatic,
                    IsVirtual = method.MethodInfo.IsVirtual,
                    IsAbstract = method.MethodInfo.IsAbstract,
                    IsAsync = IsTaskLike(method.MethodInfo.ReturnType)
                })
                .OrderBy(method => method.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(method => method.Signature, StringComparer.Ordinal)
                .ToList(),
            ConstructorParameters = unit.ConstructorParameterTypes
                .Select(parameterType => parameterType.GetCleanName())
                .ToList(),
            Configuration = CreateConfigurationDetail(unit)
        };
    }

    internal List<ProjectUnitDomainEventInfo> GetDomainEvents()
    {
        var units = catalog.GetAllUnits();
        var dependedByCounts = BuildDependedByCounts(units);

        return catalog.GetUnits<UnitDomainEvent>()
            .Select(unit => new ProjectUnitDomainEventInfo
            {
                Info = CreateSummary(unit, dependedByCounts),
                Structure = unit.GetStructure()
            })
            .ToList();
    }

    private ProjectUnitServiceIdentity CreateServiceIdentity()
    {
        var projectName = Normalize(applicationOptions.ProjectName) ?? "Unknown";
        return new ProjectUnitServiceIdentity
        {
            ProjectName = projectName,
            AppId = Normalize(applicationOptions.AppId) ?? projectName,
            AppName = Normalize(applicationOptions.AppName) ?? projectName,
            AppVersion = Normalize(applicationOptions.AppVersion),
            DomainName = Normalize(applicationOptions.DomainName)
        };
    }

    private async Task<List<ProjectUnitRequirementReference>> ResolveRequirementsAsync(
        IReadOnlyList<string> requirementIds,
        CancellationToken cancellationToken)
    {
        var references = new List<ProjectUnitRequirementReference>(requirementIds.Count);
        foreach (var requirementId in requirementIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectUnitRequirementLink? link = null;

            try
            {
                link = await requirementLinkResolver.ResolveAsync(requirementId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to resolve requirement {RequirementId} for project-unit detail navigation.",
                    requirementId);
            }

            var safeHref = NormalizeSafeHref(link?.Href);
            if (link is not null && safeHref is null)
            {
                logger.LogWarning(
                    "Requirement resolver returned an unsafe or invalid link for {RequirementId}: {RequirementHref}",
                    requirementId,
                    link.Href);
            }

            references.Add(new ProjectUnitRequirementReference
            {
                Id = requirementId,
                Title = Normalize(link?.Title) ?? requirementId,
                Href = safeHref
            });
        }

        return references;
    }

    private static ProjectUnitSummary CreateSummary(
        ProjectUnit unit,
        IReadOnlyDictionary<string, int> dependedByCounts)
    {
        return new ProjectUnitSummary
        {
            Key = unit.Key,
            Title = unit.Title,
            Description = Normalize(unit.Description),
            Owner = Normalize(unit.Owner),
            Tags = unit.Tags.ToList(),
            UnitType = unit.UnitType,
            HasExplicitMetadata = unit.HasExplicitMetadata,
            RequirementCount = unit.RequirementIds.Count,
            MethodCount = unit.Methods.Count,
            Dependencies = unit.DependencyUnits
                .Select(CreateReference)
                .OrderBy(reference => reference.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            DependedByCount = dependedByCounts.GetValueOrDefault(unit.Key),
            Alerts = unit.Alerts.Select(CloneAlert).ToList()
        };
    }

    private static ProjectUnitReference CreateReference(ProjectUnit unit)
    {
        return new ProjectUnitReference
        {
            Key = unit.Key,
            Title = unit.Title,
            UnitType = unit.UnitType
        };
    }

    private static ProjectUnitAlert CloneAlert(ProjectUnitAlert alert)
    {
        return new ProjectUnitAlert
        {
            Level = alert.Level,
            Message = alert.Message,
            Source = alert.Source,
            CreatedAt = alert.CreatedAt
        };
    }

    private static Dictionary<string, int> BuildDependedByCounts(IReadOnlyList<ProjectUnit> units)
    {
        return units
            .SelectMany(unit => unit.DependencyUnits)
            .GroupBy(dependency => dependency.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static ProjectUnitCoverageMetric CreateCoverageMetric(
        ProjectUnitCoverageKind kind,
        int covered,
        int total)
    {
        return new ProjectUnitCoverageMetric
        {
            Kind = kind,
            Covered = covered,
            Total = total,
            Percentage = CalculatePercentage(covered, total)
        };
    }

    private static decimal? CalculatePercentage(int count, int total)
    {
        return total == 0 ? null : Math.Round(count * 100m / total, 1, MidpointRounding.AwayFromZero);
    }

    private static ProjectUnitCoverageGap CreateCoverageGap(ProjectUnit unit, ProjectUnitSummary summary)
    {
        var missingCoverage = new List<ProjectUnitCoverageKind>();
        if (!unit.HasExplicitMetadata)
        {
            missingCoverage.Add(ProjectUnitCoverageKind.Metadata);
        }

        if (string.IsNullOrWhiteSpace(unit.Description))
        {
            missingCoverage.Add(ProjectUnitCoverageKind.Description);
        }

        if (string.IsNullOrWhiteSpace(unit.Owner))
        {
            missingCoverage.Add(ProjectUnitCoverageKind.Ownership);
        }

        if (unit.RequirementIds.Count == 0)
        {
            missingCoverage.Add(ProjectUnitCoverageKind.Requirements);
        }

        return new ProjectUnitCoverageGap
        {
            Unit = summary,
            MissingCoverage = missingCoverage
        };
    }

    private static ProjectUnitAlertStatistics CreateAlertStatistics(IReadOnlyList<ProjectUnit> units)
    {
        var alerts = units.SelectMany(unit => unit.Alerts).ToList();
        return new ProjectUnitAlertStatistics
        {
            InformationCount = alerts.Count(alert => alert.Level == EAlertLevel.Info),
            WarningCount = alerts.Count(alert => alert.Level == EAlertLevel.Warning),
            ErrorCount = alerts.Count(alert => alert.Level == EAlertLevel.Error)
        };
    }

    private static ProjectUnitConfigurationDetail? CreateConfigurationDetail(ProjectUnit unit)
    {
        if (unit is not UnitConfiguration configuration)
        {
            return null;
        }

        return new ProjectUnitConfigurationDetail
        {
            DefinitionKey = configuration.DefinitionKey,
            IsOffline = configuration.IsOffline,
            ReloadBehavior = configuration.InferredReloadBehavior,
            Usages = configuration.ConfigurationDependencies
                .Select(item => new ProjectUnitConfigurationUsage
                {
                    Unit = CreateReference(item.Key),
                    UsageType = item.Value
                })
                .OrderBy(usage => usage.Unit.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static bool IsTaskLike(Type returnType)
    {
        if (typeof(Task).IsAssignableFrom(returnType) || returnType == typeof(ValueTask))
        {
            return true;
        }

        return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>);
    }

    private static string? NormalizeSafeHref(string? href)
    {
        if (Normalize(href) is not { } normalized
            || !Uri.TryCreate(normalized, UriKind.RelativeOrAbsolute, out var uri))
        {
            return null;
        }

        if (!uri.IsAbsoluteUri)
        {
            return normalized.StartsWith("//", StringComparison.Ordinal) ? null : normalized;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? normalized
            : null;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

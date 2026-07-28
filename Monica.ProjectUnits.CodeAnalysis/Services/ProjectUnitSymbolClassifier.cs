using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Monica.ProjectUnits.CodeAnalysis.Models;
using Monica.ProjectUnits.Models;

namespace Monica.ProjectUnits.CodeAnalysis.Services;

internal sealed class ProjectUnitSymbolClassifier
{
    private const string APPLICATION_SERVICE = "Monica.WebApi.Abstractions.ApplicationService";
    private const string APPLICATION_SERVICE_GENERIC = "Monica.WebApi.Abstractions.ApplicationService`2";
    private const string CONFIGURATION_ATTRIBUTE = "Monica.Configuration.Annotations.ConfigurationAttribute";
    private const string CRUD_APPLICATION_SERVICE = "Monica.WebApi.AutoControllers.Abstractions.ICrudApplicationService";
    private const string DISTRIBUTED_EVENT_HANDLER = "Monica.EventBus.Abstractions.Handlers.IDistributedEventHandler`1";
    private const string DOMAIN_EVENT = "Monica.EventBus.Events.IDomainEvent";
    private const string DOMAIN_EVENT_BASE = "Monica.EventBus.Events.DomainEvent";
    private const string DOMAIN_SERVICE = "Monica.WebApi.Abstractions.DomainService";
    private const string ENTITY = "Monica.Repository.Entity.Abstractions.IEntity";
    private const string LOCAL_EVENT_HANDLER = "Monica.EventBus.Abstractions.Handlers.ILocalEventHandler`1";
    private const string METADATA_ATTRIBUTE = "Monica.ProjectUnits.Annotations.ProjectUnitMetadataAttribute";
    private const string RECURRING_JOB = "Monica.JobScheduler.Abstractions.RecurringJob";
    private const string REPOSITORY = "Monica.Repository.Persistence.Abstractions.IRepository`1";
    private const string REQUEST = "Monica.WebApi.Abstractions.IResultRequestBase";
    private const string REQUIREMENT_ATTRIBUTE = "Monica.ProjectUnits.Annotations.ProjectUnitRequirementAttribute";
    private const string TRIGGERED_JOB = "Monica.JobScheduler.Abstractions.TriggeredJob`1";
    private const string OPTIONS = "Microsoft.Extensions.Options.IOptions`1";
    private const string OPTIONS_MONITOR = "Microsoft.Extensions.Options.IOptionsMonitor`1";
    private const string OPTIONS_SNAPSHOT = "Microsoft.Extensions.Options.IOptionsSnapshot`1";

    internal ProjectUnitSymbolCandidate? Classify(INamedTypeSymbol symbol)
    {
        if (symbol is not { TypeKind: TypeKind.Class, IsAbstract: false, Arity: 0 })
        {
            return null;
        }

        var classification = ClassifyType(symbol);
        if (classification is null)
        {
            return null;
        }

        var diagnostics = new List<ProjectUnitSourceDiagnostic>();
        var description = ReadDocumentationSummary(symbol);
        var title = symbol.Name;

        if (symbol.FindAttribute(CONFIGURATION_ATTRIBUTE, inherit: true) is { } configuration)
        {
            title = configuration.ReadNamedString("DisplayName") ?? title;
            description = configuration.ReadNamedString("Description") ?? description;
        }

        var metadataAttributes = symbol.GetAttributes()
            .Where(attribute => string.Equals(
                attribute.AttributeClass?.GetMetadataName(),
                METADATA_ATTRIBUTE,
                StringComparison.Ordinal))
            .ToArray();
        var hasExplicitMetadata = metadataAttributes.Length != 0;
        string? owner = null;
        IReadOnlyList<string> tags = [];
        if (metadataAttributes.Length > 1)
        {
            diagnostics.Add(Warning(
                "ProjectUnit.Metadata.Duplicate",
                "A ProjectUnit may declare only one ProjectUnitMetadataAttribute."));
        }

        if (metadataAttributes.FirstOrDefault() is { } metadata)
        {
            if (metadata.ReadString(0) is { } metadataTitle)
            {
                title = metadataTitle;
            }
            else
            {
                diagnostics.Add(Warning(
                    "ProjectUnit.Metadata.Title.Empty",
                    "Project-unit metadata title must not be empty."));
            }

            owner = metadata.ReadNamedString("Owner");
            description = metadata.ReadNamedString("Description") ?? description;
            tags = NormalizeValues(
                metadata.ReadNamedStrings("Tags"),
                "ProjectUnit.Metadata.Tag.Empty",
                "Project-unit metadata tags must not contain empty values.",
                diagnostics);
        }

        var requirements = NormalizeValues(
            symbol.GetAttributes()
                .Where(attribute => string.Equals(
                    attribute.AttributeClass?.GetMetadataName(),
                    REQUIREMENT_ATTRIBUTE,
                    StringComparison.Ordinal))
                .Select(attribute => attribute.ReadString(0)),
            "ProjectUnit.Requirement.Id.Empty",
            "Project-unit requirement identifiers must not be empty.",
            diagnostics);

        var dependencies = BuildDependencies(symbol, classification);
        return new ProjectUnitSymbolCandidate(
            symbol,
            classification.UnitType,
            title,
            description,
            owner,
            tags,
            requirements,
            hasExplicitMetadata,
            dependencies,
            diagnostics);
    }

    private static ProjectUnitTypeClassification? ClassifyType(INamedTypeSymbol symbol)
    {
        var isApplicationService = symbol.DerivesFrom(APPLICATION_SERVICE);
        if (isApplicationService && symbol.FindInterface(CRUD_APPLICATION_SERVICE) is not null)
        {
            return new(EProjectUnitType.CrudApplicationService, AnalyzeConstructorDependencies: true);
        }

        if (isApplicationService)
        {
            return new(EProjectUnitType.ApplicationService, AnalyzeConstructorDependencies: true);
        }

        if (symbol.FindAttribute(CONFIGURATION_ATTRIBUTE, inherit: true) is not null)
        {
            return new(EProjectUnitType.Configuration, AnalyzeConstructorDependencies: false);
        }

        if (symbol.FindInterface(DISTRIBUTED_EVENT_HANDLER) is not null)
        {
            return new(EProjectUnitType.DomainEventHandler, AnalyzeConstructorDependencies: true);
        }

        if (symbol.FindInterface(LOCAL_EVENT_HANDLER) is not null)
        {
            return new(EProjectUnitType.LocalEventHandler, AnalyzeConstructorDependencies: true);
        }

        if (!string.Equals(symbol.GetMetadataName(), DOMAIN_EVENT_BASE, StringComparison.Ordinal)
            && symbol.FindInterface(DOMAIN_EVENT) is not null)
        {
            return new(EProjectUnitType.DomainEvent, AnalyzeConstructorDependencies: false);
        }

        if (symbol.FindInterface(REPOSITORY) is not null
            && symbol.AllInterfaces.Any(candidate => candidate.Name == $"I{symbol.Name}"))
        {
            return new(EProjectUnitType.Repository, AnalyzeConstructorDependencies: true);
        }

        if (symbol.FindInterface(ENTITY) is not null)
        {
            return new(EProjectUnitType.Entity, AnalyzeConstructorDependencies: false);
        }

        if (symbol.DerivesFrom(DOMAIN_SERVICE))
        {
            return new(EProjectUnitType.DomainService, AnalyzeConstructorDependencies: true);
        }

        if (symbol.DerivesFrom(RECURRING_JOB))
        {
            return new(EProjectUnitType.RecurringJob, AnalyzeConstructorDependencies: true);
        }

        if (symbol.FindBaseType(TRIGGERED_JOB) is not null)
        {
            return new(EProjectUnitType.TriggeredJob, AnalyzeConstructorDependencies: true);
        }

        if (symbol.FindInterface(REQUEST) is not null)
        {
            return new(EProjectUnitType.RequestDto, AnalyzeConstructorDependencies: false);
        }

        return null;
    }

    private static IReadOnlyList<ProjectUnitSymbolDependency> BuildDependencies(
        INamedTypeSymbol symbol,
        ProjectUnitTypeClassification classification)
    {
        var dependencies = new List<ProjectUnitSymbolDependency>();
        switch (classification.UnitType)
        {
            case EProjectUnitType.ApplicationService:
            case EProjectUnitType.CrudApplicationService:
                if (symbol.FindBaseType(APPLICATION_SERVICE_GENERIC)?.TypeArguments.FirstOrDefault() is INamedTypeSymbol request)
                {
                    dependencies.Add(new(request, "ProjectUnit.ApplicationService.Request.NotFound"));
                }
                break;
            case EProjectUnitType.DomainEventHandler:
                if (symbol.FindInterface(DISTRIBUTED_EVENT_HANDLER)?.TypeArguments.FirstOrDefault() is INamedTypeSymbol distributedEvent)
                {
                    dependencies.Add(new(distributedEvent, "ProjectUnit.EventHandler.Event.NotFound"));
                }
                break;
            case EProjectUnitType.LocalEventHandler:
                if (symbol.FindInterface(LOCAL_EVENT_HANDLER)?.TypeArguments.FirstOrDefault() is INamedTypeSymbol localEvent)
                {
                    dependencies.Add(new(localEvent, "ProjectUnit.EventHandler.Event.NotFound"));
                }
                break;
            case EProjectUnitType.Repository:
                if (symbol.FindInterface(REPOSITORY)?.TypeArguments.FirstOrDefault() is INamedTypeSymbol entity)
                {
                    dependencies.Add(new(entity, "ProjectUnit.Repository.Entity.NotFound"));
                }
                break;
        }

        if (classification.AnalyzeConstructorDependencies)
        {
            foreach (var constructor in symbol.InstanceConstructors.Where(static constructor =>
                         constructor.DeclaredAccessibility == Accessibility.Public))
            {
                foreach (var parameter in constructor.Parameters)
                {
                    if (ResolveOptionsType(parameter.Type) is { } dependency)
                    {
                        dependencies.Add(new(dependency, null));
                    }
                }
            }
        }

        return dependencies
            .DistinctBy(static dependency => SymbolIdentity.Create(dependency.Symbol), StringComparer.Ordinal)
            .ToArray();
    }

    private static INamedTypeSymbol? ResolveOptionsType(ITypeSymbol symbol)
    {
        if (symbol is not INamedTypeSymbol named)
        {
            return null;
        }

        var definition = named.OriginalDefinition.GetMetadataName();
        if (definition is OPTIONS or OPTIONS_MONITOR or OPTIONS_SNAPSHOT)
        {
            return named.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
        }

        return named;
    }

    private static IReadOnlyList<string> NormalizeValues(
        IEnumerable<string?> values,
        string diagnosticCode,
        string diagnosticMessage,
        ICollection<ProjectUnitSourceDiagnostic> diagnostics)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (ProjectUnitSymbolFacts.Normalize(value) is not { } normalized)
            {
                diagnostics.Add(Warning(diagnosticCode, diagnosticMessage));
            }
            else if (seen.Add(normalized))
            {
                results.Add(normalized);
            }
        }

        return results;
    }

    private static string? ReadDocumentationSummary(INamedTypeSymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml(cancellationToken: default);
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            var summary = XElement.Parse(xml).Element("summary")?.Value;
            return ProjectUnitSymbolFacts.Normalize(
                summary is null
                    ? null
                    : string.Join(' ', summary.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static ProjectUnitSourceDiagnostic Warning(string code, string message)
        => new(code, ProjectUnitSourceDiagnosticSeverity.Warning, message);

    private sealed record ProjectUnitTypeClassification(
        EProjectUnitType UnitType,
        bool AnalyzeConstructorDependencies);
}

internal sealed record ProjectUnitSymbolCandidate(
    INamedTypeSymbol Symbol,
    EProjectUnitType UnitType,
    string Title,
    string? Description,
    string? Owner,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> RequirementIds,
    bool HasExplicitMetadata,
    IReadOnlyList<ProjectUnitSymbolDependency> Dependencies,
    IReadOnlyList<ProjectUnitSourceDiagnostic> Diagnostics);

internal sealed record ProjectUnitSymbolDependency(
    INamedTypeSymbol Symbol,
    string? MissingDiagnosticCode);

internal static class SymbolIdentity
{
    internal static string Create(INamedTypeSymbol symbol)
        => $"{symbol.ContainingAssembly.Identity.Name}::{symbol.GetRuntimeName()}";
}

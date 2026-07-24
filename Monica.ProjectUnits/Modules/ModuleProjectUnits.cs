using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;
using Monica.Core.XmlDocumentation.Abstractions;
using Monica.ProjectUnits.Abstractions;
using Monica.ProjectUnits.Facades;
using Monica.ProjectUnits.Models;
using Monica.ProjectUnits.Providers.AspNetCore;
using Monica.ProjectUnits.Services;
using Monica.ProjectUnits.Services.Support;
using Monica.Tool.Extensions;
using Monica.WebApi.AutoControllers.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleProjectUnitsBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers host-scoped project architecture discovery, diagnostics, and optional request filtering.
        /// </summary>
        /// <param name="action">An optional callback that configures discovery and endpoint behavior.</param>
        /// <returns>The module guide used to register or configure dependencies.</returns>
        public ModuleProjectUnitsGuide AddProjectUnits(Action<ModuleProjectUnitsOption>? action = null)
        {
            return builder.AddModule<ModuleProjectUnits, ModuleProjectUnitsOption, ModuleProjectUnitsGuide>(action);
        }
    }
}

/// <summary>
/// Discovers application architecture units for the current host and exposes optional inspection endpoints.
/// </summary>
[ModuleKey(BuiltInModuleKey.ProjectUnits)]
public class ModuleProjectUnits(ModuleProjectUnitsOption option)
    : WebModuleBase<ModuleProjectUnits, ModuleProjectUnitsOption, ModuleProjectUnitsGuide>(option), IBusinessTypeIterator
{
    private readonly ProjectUnitCatalog _catalog = new(option);

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        if (Option.ParseUnitDetails)
        {
            DependsOnModule<ModuleXmlDocumentationGuide>().Register();
        }

        DependsOnModule<ModuleJsonSerializationGuide>().Register();
        DependsOnModule<ModuleEventBusGuide>().Register();
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        DeriveCrudNamingRuleFromAutoControllers();
        _catalog.SetDocumentationService(option.ParseUnitDetails ? ResolveDocumentationService(services) : null);

        services.AddSingleton(_catalog);
        services.AddSingleton<IProjectUnitCatalog>(_catalog);
        services.TryAddScoped<IProjectUnitRequirementLinkResolver, NullProjectUnitRequirementLinkResolver>();
        services.AddScoped<ProjectUnitProjectionService>();
        services.AddScoped<ProjectUnitCatalogService>();
        services.AddScoped<ProjectUnitsFacade>();
    }

    /// <summary>
    /// Derives the <see cref="EProjectUnitType.CrudApplicationService"/> naming rule from the AutoControllers module's
    /// configured <c>CrudControllerOption.CrudControllerPostfix</c>, so the suffix is configured in a single place.
    /// </summary>
    /// <remarks>
    /// CRUD application services strip <c>CrudControllerPostfix</c> to derive their route name, so that suffix is the
    /// natural single source of truth for their naming convention. This module reads the finalized option (available
    /// because option materialization runs before <see cref="ConfigureServices"/>) and only fills in the rule when the
    /// host has not configured one explicitly and the postfix is non-empty. When the AutoControllers module is absent,
    /// nothing is derived.
    /// </remarks>
    private void DeriveCrudNamingRuleFromAutoControllers()
    {
        if (option.ConventionOptions.Dict.ContainsKey(EProjectUnitType.CrudApplicationService))
        {
            // Respect an explicit host-provided rule.
            return;
        }

        if (!Application.Modules.IsRegistered(typeof(ModuleAutoControllers)))
        {
            return;
        }

        var crudOption = GetOptions<CrudControllerOption>();
        if (string.IsNullOrEmpty(crudOption.CrudControllerPostfix))
        {
            return;
        }

        option.ConventionOptions.Dict[EProjectUnitType.CrudApplicationService] = new ProjectUnitNamingRule
        {
            Postfix = crudOption.CrudControllerPostfix
        };
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        return _catalog.Discover(types);
    }

    /// <inheritdoc />
    public override void PostConfigureServices(IServiceCollection services)
    {
        _catalog.ConnectUnits();
        ProjectUnitConfigurationReloadBehaviorEnricher.Enrich(services, _catalog, Logger);
        if (option.EnableRequestFilter)
        {
            services.AddRequestFilter(_catalog);
        }
    }

    private sealed class RequestFilterDto
    {
        public List<string>? Urls { get; set; }
        public bool? Disable { get; set; }
    }

    /// <inheritdoc />
    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        if (option.EnableRequestFilter)
        {
            app.UseRequestFilter();
        }
    }

    /// <inheritdoc />
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            var tagName = option.GetApiGroupName();

            endpoints.MapPost("/framework/units/domain-event/{eventKey}/publish",
                async ([FromRoute] string eventKey,
                      [FromServices] ProjectUnitsFacade projectUnitsFacade,
                      [FromBody] JsonNode eventContent) =>
                {
                    return await projectUnitsFacade.PublishDomainEventAsync(eventKey, eventContent);
                })
                .WithName("ProjectUnits.PublishDomainEvent")
                .WithTags(tagName)
                .WithSummary("Publish a discovered domain event")
                .WithDescription("Deserializes and publishes a domain event by its discovered project-unit key.");

            if (option.EnableRequestFilter)
            {
                endpoints.MapPost("/framework/request-filter",
                    async ([FromBody] RequestFilterDto dto,
                          [FromServices] ProjectUnitsFacade projectUnitsFacade) =>
                    {
                        return await projectUnitsFacade.ManageRequestFilterAsync(dto.Urls, dto.Disable);
                    })
                    .WithName("ProjectUnits.ConfigureRequestFilter")
                    .WithTags(tagName)
                    .WithSummary("Configure the project-unit request filter")
                    .WithDescription("Enables or disables host-local request paths when request filtering is configured.");
            }

            endpoints.MapGet("/framework/units",
                async ([FromServices] ProjectUnitsFacade projectUnitsFacade) =>
                {
                    return await projectUnitsFacade.GetAllProjectUnitsAsync();
                })
                .WithName("ProjectUnits.List")
                .WithTags(tagName)
                .WithSummary("List discovered project units")
                .WithDescription("Returns every architecture unit discovered for the current host.");

            endpoints.MapGet("/framework/units/dashboard",
                async ([FromServices] ProjectUnitsFacade projectUnitsFacade) =>
                {
                    return await projectUnitsFacade.GetDashboardAsync();
                })
                .WithName("ProjectUnits.Dashboard")
                .WithTags(tagName)
                .WithSummary("Get the project-unit status dashboard")
                .WithDescription("Returns host identity, architecture health, independent context coverage, and actionable gaps.");

            endpoints.MapGet("/framework/units/domain-event",
                async ([FromServices] ProjectUnitsFacade projectUnitsFacade) =>
                {
                    return await projectUnitsFacade.GetDomainEventsAsync();
                })
                .WithName("ProjectUnits.ListDomainEvents")
                .WithTags(tagName)
                .WithSummary("List discovered domain events")
                .WithDescription("Returns domain-event project units and their serializable structure.");

            endpoints.MapGet("/framework/units/{key}",
                async ([FromRoute] string key,
                      [FromServices] ProjectUnitsFacade projectUnitsFacade,
                      CancellationToken cancellationToken) =>
                {
                    return await projectUnitsFacade.GetProjectUnitDetailAsync(key, cancellationToken);
                })
                .WithName("ProjectUnits.Detail")
                .WithTags(tagName)
                .WithSummary("Get project-unit detail")
                .WithDescription("Returns typed project-unit metadata, dependencies, methods, and requirement links.");

            endpoints.MapGet("/framework/enum",
                async ([FromServices] ProjectUnitsFacade projectUnitsFacade,
                      [FromQuery] string? name = null) =>
                {
                    return await projectUnitsFacade.GetEnumInfoAsync(name);
                })
                .WithName("ProjectUnits.ListEnums")
                .WithTags(tagName)
                .WithSummary("List discovered enum metadata")
                .WithDescription("Returns discovered enum names and values, optionally filtered by enum name.");
        });
    }

    private static IXmlDocumentationService? ResolveDocumentationService(IServiceCollection services)
    {
        return services
            .LastOrDefault(descriptor =>
                !descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(IXmlDocumentationService))
            ?.ImplementationInstance as IXmlDocumentationService;
    }
}

/// <summary>
/// Provides fluent registration for the ProjectUnits module.
/// </summary>
public class ModuleProjectUnitsGuide : WebModuleGuide<ModuleProjectUnits, ModuleProjectUnitsOption,
    ModuleProjectUnitsGuide>
{
    /// <summary>
    /// Registers the application-owned resolver used to turn requirement identifiers into optional navigation links.
    /// </summary>
    /// <typeparam name="TResolver">A scoped requirement-link resolver implementation.</typeparam>
    /// <returns>The current guide instance.</returns>
    /// <remarks>
    /// Resolution occurs only when project-unit detail is requested. Unknown requirements should return
    /// <see langword="null"/> so they remain visible as unresolved references.
    /// </remarks>
    public ModuleProjectUnitsGuide UseRequirementLinkResolver<TResolver>()
        where TResolver : class, IProjectUnitRequirementLinkResolver
    {
        ConfigureServices(context =>
        {
            context.Services.Replace(
                ServiceDescriptor.Scoped<IProjectUnitRequirementLinkResolver, TResolver>());
        });
        return this;
    }
}

/// <summary>
/// Configures host-scoped project-unit discovery and diagnostic endpoints.
/// </summary>
public class ModuleProjectUnitsOption : MinimalApiModuleOptions<ModuleProjectUnits>
{
    /// <summary>
    /// Gets or sets the naming rules used to validate discovered architectural units.
    /// </summary>
    public ProjectUnitNamingOptions ConventionOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the request-filter middleware and its management endpoint are enabled.
    /// The filter is disabled by default and its path state is isolated to the current host.
    /// </summary>
    public bool EnableRequestFilter { get; set; }

    /// <summary>
    /// Gets or sets whether ProjectUnits loads XML summaries for discovered types and methods.
    /// The default is <see langword="true"/> and automatically registers the XML documentation module.
    /// </summary>
    public bool ParseUnitDetails { get; set; } = true;
}

/// <summary>
/// Configures project-unit naming validation globally and per architectural category.
/// </summary>
public class ProjectUnitNamingOptions
{
    /// <summary>
    /// Gets or sets category-specific naming rules. Missing categories use their project-unit model defaults.
    /// </summary>
    public Dictionary<EProjectUnitType, ProjectUnitNamingRule> Dict { get; set; } = [];

    /// <summary>
    /// Gets or sets the fallback behavior when a naming rule is violated. The default records a warning.
    /// </summary>
    public ENameConventionMode NameConventionMode { get; set; } = ENameConventionMode.Warning;

    /// <summary>
    /// Gets or sets whether naming conventions are validated. Validation is disabled by default.
    /// </summary>
    public bool EnableNameConvention { get; set; }
}

/// <summary>
/// Describes the required fragments of a project-unit CLR type name and namespace.
/// </summary>
public class ProjectUnitNamingRule
{
    /// <summary>
    /// Gets or sets the required type-name suffix.
    /// </summary>
    public string? Postfix { get; set; }

    /// <summary>
    /// Gets or sets the required type-name prefix.
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// Gets or sets a type-name fragment recorded for diagnostics.
    /// </summary>
    public string? Contains { get; set; }

    /// <summary>
    /// Gets or sets a namespace fragment recorded for diagnostics.
    /// </summary>
    public string? NamespaceContains { get; set; }

    /// <summary>
    /// Gets or sets the violation behavior for this rule, or <see langword="null"/> to use the global behavior.
    /// </summary>
    public ENameConventionMode? NameConventionMode { get; set; } = ENameConventionMode.Warning;

    public override string ToString()
    {
        return $"{Postfix?.Be("Suffix: {0}\n", true)}{Prefix?.Be("Prefix: {0}\n", true)}{Contains?.Be("Contains: {0}\n", true)}{NamespaceContains?.Be("Namespace contains: {0}", true)}".TrimEnd();
    }
}

/// <summary>
/// Specifies how ProjectUnits responds to naming-convention violations.
/// </summary>
public enum ENameConventionMode
{
    /// <summary>
    /// Records an alert and allows host startup to continue.
    /// </summary>
    Warning,

    /// <summary>
    /// Records an error and stops host startup.
    /// </summary>
    Strict,

    /// <summary>
    /// Ignores naming-convention violations for the rule.
    /// </summary>
    Disable
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.TypeDiscovery.Models;
using Monica.WebApi.AutoControllers.Abstractions;
using Monica.WebApi.AutoControllers.Abstractions.Internal;
using Monica.WebApi.AutoControllers.Extensions;
using Monica.WebApi.AutoControllers.Models;
using Monica.WebApi.AutoControllers.Services.Support;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleAutoControllersBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers generated MVC controllers and configures both module and CRUD conventions.
        /// </summary>
        public ModuleRegistration<ModuleAutoControllers, ModuleAutoControllersOption> AddAutoControllers(
            Action<ModuleAutoControllersOption>? configure = null,
            Action<CrudControllerOption>? configureCrud = null)
        {
            return builder.AddModule<ModuleAutoControllers, ModuleAutoControllersOption>(configure)
                .Configure(options => configureCrud?.Invoke(options.Crud));
        }
    }
}

/// <summary>
/// Discovers host controllers and CRUD services, then composes MVC without building a temporary service provider.
/// </summary>
public class ModuleAutoControllers : MonicaModule<ModuleAutoControllersOption>, IWebHostRequiredModule
{
    private readonly AutoControllerApplicationPartCatalog _applicationPartCatalog = new();

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleAutoModel, ModuleAutoModelOption>();
        module.Require<ModuleControllers, ModuleControllersOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleAutoControllersOption> context)
    {
        var services = context.Services;
        var validation = new CrudControllerOptionValidator().Validate(name: null, Option.Crud);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                nameof(ModuleAutoControllersOption.Crud),
                typeof(CrudControllerOption),
                validation.Failures);
        }

        services.AddSingleton(_applicationPartCatalog);
        services.AddSingleton<IOptions<CrudControllerOption>>(
            Microsoft.Extensions.Options.Options.Create(Option.Crud));
        services.TryAddSingleton<IConventionalHttpMethodResolver, ConventionalHttpMethodResolver>();
        services.AddTransient<IServiceConvention, CrudControllerServiceConvention>();
        services.AddTransient<IApiDescriptionProvider, CrudApiDescriptionProvider>();
        services.AddTransient<IApiDescriptionProvider, RequestEndpointApiDescriptionProvider>();
        services.AddTransient<IConventionalRouteBuilder, ConventionalRouteBuilder>();
        services.AddSingleton<ResultEnvelopeMvcFilter>();
        services.AddEndpointsApiExplorer();

        // MVC option configuration is created by DI after the final provider exists, avoiding a temporary container.
        services.AddSingleton<IConfigureOptions<MvcOptions>>(provider =>
            new ConfigureNamedOptions<MvcOptions>(
                Microsoft.Extensions.Options.Options.DefaultName,
                options => options.ConfigAutoController(provider)));
    }

    public override void DiscoverTypes(TypeDiscoveryPlan<ModuleAutoControllersOption> discovery)
    {
        discovery.Match(
            TypeQuery.AnyOf(
                TypeQuery.ClosedClass.AssignableTo<ControllerBase>(),
                TypeQuery.ClosedClass.AssignableTo<ICrudApplicationService>()),
            (_, matches) =>
            {
                foreach (var match in matches)
                {
                    _applicationPartCatalog.Add(match.Type);
                }
            });
    }

    public override void PostConfigureServices(ModuleContext<ModuleAutoControllersOption> context)
    {
        var mvcBuilder = context.Services.AddControllers();
        var applicationPartTypes = _applicationPartCatalog.GetApplicationPartTypes();

        mvcBuilder.PartManager.ApplicationParts.Clear();
        mvcBuilder.PartManager.ApplicationParts.Add(new TypeCollectionApplicationPart(applicationPartTypes));
        mvcBuilder.PartManager.FeatureProviders.Add(new CrudControllerFeatureProvider(
            NullLogger<CrudControllerFeatureProvider>.Instance,
            Microsoft.Extensions.Options.Options.Create(Option.Crud)));
        mvcBuilder.Services.Replace(
            ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
    }

    public override void ConfigureEndpoints(WebModuleContext<ModuleAutoControllersOption> context)
    {
        context.ApplicationBuilder.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}

/// <summary>
/// Owns module composition settings and the primary generated-CRUD convention object.
/// </summary>
public class ModuleAutoControllersOption : ModuleOptions<ModuleAutoControllers>
{
    /// <summary>
    /// Gets the generated CRUD route, naming, paging, and HTTP-method conventions for this host.
    /// </summary>
    public CrudControllerOption Crud { get; } = new();
}

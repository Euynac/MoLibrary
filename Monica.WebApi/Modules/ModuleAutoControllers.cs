using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
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
        /// Registers and configures the AutoControllers module.
        /// </summary>
        public ModuleAutoControllersGuide AddAutoControllers(Action<ModuleAutoControllersOption>? action = null, Action<CrudControllerOption>? crudOptionAction = null)
        {
            return builder.AddModule<ModuleAutoControllers, ModuleAutoControllersOption, ModuleAutoControllersGuide>(action)
                .ConfigureExtraOption(crudOptionAction);
        }
    }
}

[ModuleKey(BuiltInModuleKey.AutoControllers)]
public class ModuleAutoControllers(ModuleAutoControllersOption option)
    : WebModuleBase<ModuleAutoControllers, ModuleAutoControllersOption, ModuleAutoControllersGuide>(option), IBusinessTypeIterator
{
    private readonly AutoControllerApplicationPartCatalog _applicationPartCatalog = new();

    public override void ConfigureServices(IServiceCollection services)
    {
        // Keep discovery state on a module-owned singleton so direct and transitive registration paths share it.
        services.AddSingleton(_applicationPartCatalog);
        services.TryAddSingleton<IConventionalHttpMethodResolver, ConventionalHttpMethodResolver>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<CrudControllerOption>, CrudControllerOptionValidator>());
        services.AddOptions<CrudControllerOption>().ValidateOnStart();
    }

    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            // AutoControllers exposes host/business MVC controllers, so it must stay unmarked and therefore defaults to the business Swagger document.
            endpoints.MapControllers();
        });
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAutoModelGuide>().Register();
        DependsOnModule<ModuleControllersGuide>().Register().ConfigMvcBuilder((builder, provider) =>
        {
            var catalog = provider.GetRequiredService<AutoControllerApplicationPartCatalog>();
            var applicationPartTypes = catalog.GetApplicationPartTypes();

            builder.PartManager.ApplicationParts.Clear();
            builder.PartManager.ApplicationParts.Add(new TypeCollectionApplicationPart(applicationPartTypes));

            // Used to identify generated CRUD controllers from the registered types.
            builder.PartManager.FeatureProviders.Add(
                ActivatorUtilities
                    .CreateInstance<CrudControllerFeatureProvider>(provider));
            builder.Services.Replace(ServiceDescriptor.Transient<IControllerActivator, ServiceBasedControllerActivator>());
            
            // Important: ASP.NET Core MVC uses its own controller activation by default. However, for CrudApplicationService, it needs to be obtained from dependency injection (including ICachedServiceProvider).
            
        }).ConfigMvcOption((o, provider) =>
        {
            o.ConfigAutoController(provider);
        }).ConfigDependentServices(services =>
        {
            services.AddTransient<IServiceConvention, CrudControllerServiceConvention>();
            services.AddTransient<IApiDescriptionProvider, CrudApiDescriptionProvider>();
            services.AddTransient<IApiDescriptionProvider, RequestEndpointApiDescriptionProvider>();
            services.AddTransient<IConventionalRouteBuilder, ConventionalRouteBuilder>();
            services.AddSingleton<ResultEnvelopeMvcFilter>();
            //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi?view=aspnetcore-7.0

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

            // if you use v6's "minimal APIs" https://stackoverflow.com/questions/71932980/what-is-addendpointsapiexplorer-in-asp-net-core-6
            services.AddEndpointsApiExplorer();
        });
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false, IsGenericType: false } &&
                (typeof(ControllerBase).IsAssignableFrom(type) || typeof(ICrudApplicationService).IsAssignableFrom(type)))
            {
                _applicationPartCatalog.Add(type);
            }

            yield return type;
        }
    }
}

/// <summary>
/// Provides fluent configuration for generated AutoControllers.
/// </summary>
public class ModuleAutoControllersGuide : WebModuleGuide<ModuleAutoControllers, ModuleAutoControllersOption,
    ModuleAutoControllersGuide>
{

}

/// <summary>
/// Configures the AutoControllers module lifecycle.
/// </summary>
/// <remarks>
/// Generated CRUD routes, paging, and HTTP method conventions are configured through
/// <see cref="CrudControllerOption" /> in the second <c>AddAutoControllers</c> callback.
/// </remarks>
public class ModuleAutoControllersOption : ModuleOptions<ModuleAutoControllers>;

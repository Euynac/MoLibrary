using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.DomainDrivenDesign.AutoController;
using Monica.DomainDrivenDesign.AutoController.Components;
using Monica.DomainDrivenDesign.AutoController.Extensions;
using Monica.DomainDrivenDesign.AutoController.Features;
using Monica.DomainDrivenDesign.AutoController.Interfaces;
using Monica.DomainDrivenDesign.AutoCrud;
using Monica.DomainDrivenDesign.AutoController.Settings;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleAutoControllersBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Registers and configures the AutoControllers module.
        /// </summary>
        public static ModuleAutoControllersGuide AddAutoControllers(Action<ModuleAutoControllersOption>? action = null, Action<MoCrudControllerOption>? crudOptionAction = null)
        {
            var applicationPartTypes = new HashSet<Type>();

            return new ModuleAutoControllersGuide()
                .Register(option =>
                {
                    option.SetApplicationPartTypes(applicationPartTypes);
                    action?.Invoke(option);
                })
                .ConfigureExtraOption(crudOptionAction);
        }
    }
}

[ModuleKey(EMoModuleKey.AutoControllers)]
public class ModuleAutoControllers(ModuleAutoControllersOption option)
    : MoModule<ModuleAutoControllers, ModuleAutoControllersOption, ModuleAutoControllersGuide>(option), IWantIterateBusinessTypes
{
    public override void ConfigureEndpoints(IApplicationBuilder app)
    {
        UseEndpoints(app, endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAutoModelGuide>().Register();
        DependsOnModule<ModuleControllersGuide>().Register().ConfigMvcBuilder((builder, provider) =>
        {
            builder.ConfigureApplicationPartManager(manager =>
            {
                var option = provider.GetRequiredService<IOptions<ModuleAutoControllersOption>>().Value;

                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new MoTypeCollectionApplicationPart(option.ApplicationPartTypes));

                // Used to identify generated CRUD controllers from the registered types.
                manager.FeatureProviders.Add(
                    ActivatorUtilities
                        .CreateInstance<MoConventionalCrudControllerFeatureProvider>(provider));
            }).AddControllersAsServices();
        }).ConfigMvcOption((o, provider) =>
        {
            o.ConfigAutoController(provider);
        }).ConfigDependentServices(services =>
        {
            services.AddTransient<IMoServiceConvention, MoCrudControllerServiceConvention>();
            services.AddTransient<IApiDescriptionProvider, MoCrudApiDescriptionProvider>();
            services.AddTransient<IMoConventionalRouteBuilder, MoConventionalRouteBuilder>();
            services.AddSingleton<MoResultFilterMvc>();
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
                (typeof(ControllerBase).IsAssignableFrom(type) || typeof(IMoCrudAppService).IsAssignableFrom(type)))
            {
                Option.AddApplicationPartType(type);
            }

            yield return type;
        }
    }
}

public class ModuleAutoControllersGuide : MoModuleGuide<ModuleAutoControllers, ModuleAutoControllersOption,
    ModuleAutoControllersGuide>
{

}

public class ModuleAutoControllersOption : MoModuleOption<ModuleAutoControllers>
{
    internal HashSet<Type> ApplicationPartTypes { get; private set; } = [];

    internal void SetApplicationPartTypes(HashSet<Type> applicationPartTypes)
    {
        ApplicationPartTypes = applicationPartTypes;
    }

    internal void AddApplicationPartType(Type type)
    {
        ApplicationPartTypes.Add(type);
    }
}

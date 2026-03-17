using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
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
        /// 配置 AutoControllers 模块
        /// </summary>
        public static ModuleAutoControllersGuide AddAutoControllers(Action<ModuleAutoControllersOption>? action = null, Action<MoCrudControllerOption>? crudOptionAction = null)
        {
            return new ModuleAutoControllersGuide().Register(action).ConfigureExtraOption(crudOptionAction);
        }
    }
}

public class ModuleAutoControllers(ModuleAutoControllersOption option)
    : MoModule<ModuleAutoControllers, ModuleAutoControllersOption, ModuleAutoControllersGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.AutoControllers;
    }
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
            //if MVC controller discovery touches EFCore migration assemblies, it will cause missing design-time dependencies
            builder.ConfigureApplicationPartManager(manager =>
            {
                var related = Mo.Options.GlobalTypeFinder.GetAssemblies()
                    .Where(static assembly => !assembly.IsDynamic)
                    .Select(static assembly => assembly.GetName().Name)
                    .Where(static name => !string.IsNullOrWhiteSpace(name))
                    .Select(static name => name!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                related = Mo.Options.GlobalTypeFinder.GetTypes()
                    .Where(static type => type is { IsClass: true, IsAbstract: false } && typeof(IMoCrudAppService).IsAssignableFrom(type))
                    .Select(static type => type.Assembly.GetName().Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name) && related.Contains(name))
                    .Select(static name => name!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (related.Count > 0)
                {
                    var partsToKeep = manager.ApplicationParts
                        .Where(p => p is not AssemblyPart part ||
                                    related.Contains(part.Name))
                        .ToList();

                    manager.ApplicationParts.Clear();
                    foreach (var part in partsToKeep)
                        manager.ApplicationParts.Add(part);
                }
                
                //用于在ApplicationParts检测需要自定义添加的Controller
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
}

public class ModuleAutoControllersGuide : MoModuleGuide<ModuleAutoControllers, ModuleAutoControllersOption,
    ModuleAutoControllersGuide>
{

}

public class ModuleAutoControllersOption : MoModuleOption<ModuleAutoControllers>
{
}

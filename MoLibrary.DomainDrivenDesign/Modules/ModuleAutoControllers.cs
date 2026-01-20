using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.AutoModel.Modules;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Modules;
using MoLibrary.DomainDrivenDesign.AutoController;
using MoLibrary.DomainDrivenDesign.AutoController.Components;
using MoLibrary.DomainDrivenDesign.AutoController.Extensions;
using MoLibrary.DomainDrivenDesign.AutoController.Features;
using MoLibrary.DomainDrivenDesign.AutoController.Interfaces;
using MoLibrary.DomainDrivenDesign.AutoController.Settings;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DomainDrivenDesign.Modules;


public static class ModuleAutoControllersBuilderExtensions
{
    public static ModuleAutoControllersGuide ConfigModuleAutoControllers(this WebApplicationBuilder builder,
        Action<ModuleAutoControllersOption>? action = null, Action<MoCrudControllerOption>? crudOptionAction = null)
    {
        return new ModuleAutoControllersGuide().Register(action).ConfigureExtraOption(crudOptionAction);
    }
}

public class ModuleAutoControllers(ModuleAutoControllersOption option)
    : MoModuleWithDependencies<ModuleAutoControllers, ModuleAutoControllersOption, ModuleAutoControllersGuide>(option)
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
            builder.ConfigureApplicationPartManager(manager =>
            {
                var related = Mo.Options.GlobalTypeFinder.GetAssemblies()
                    .Select(p => p.GetName().Name!)
                    .ToHashSet();
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
            });
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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.DomainDrivenDesign.AutoController.Components;
using MoLibrary.DomainDrivenDesign.AutoController.Extensions;
using MoLibrary.DomainDrivenDesign.AutoController.Features;
using MoLibrary.DomainDrivenDesign.AutoController.Interfaces;
using MoLibrary.DomainDrivenDesign.AutoController.Settings;
using MoLibrary.DomainDrivenDesign.AutoController;
using MoLibrary.Tool.MoResponse;
using Microsoft.AspNetCore.Mvc;
using MoLibrary.AutoModel.Modules;

namespace MoLibrary.DomainDrivenDesign.Modules;


public static class ModuleAutoControllersBuilderExtensions
{
    public static ModuleAutoControllersGuide AddMoModuleAutoControllers(this WebApplicationBuilder builder,
        Action<ModuleAutoControllersOption>? action = null, Action<MoCrudControllerOption>? crudOptionAction = null, Action<MvcOptions>? setupAction = null)
    {
        return new ModuleAutoControllersGuide().Register(action).ConfigureExtraOption(crudOptionAction)
            .ConfigureServices(setupAction);
    }
}

public class ModuleAutoControllers(ModuleAutoControllersOption option)
    : MoModuleWithDependencies<ModuleAutoControllers, ModuleAutoControllersOption>(option)
{
    public override EMoModules CurModuleEnum()
    {
        return EMoModules.AutoControllers;
    }

    public override Res ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IMoServiceConvention, MoCrudControllerServiceConvention>();
        services.AddTransient<IApiDescriptionProvider, MoCrudApiDescriptionProvider>();
        services.AddTransient<IMoConventionalRouteBuilder, MoConventionalRouteBuilder>();
        services.AddSingleton<MoResultFilterMvc>();
        //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi?view=aspnetcore-7.0

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

        // if you use v6's "minimal APIs" https://stackoverflow.com/questions/71932980/what-is-addendpointsapiexplorer-in-asp-net-core-6
        services.AddEndpointsApiExplorer();
        return Res.Ok();
    }

    public override Res ConfigureEndpoints(IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
        return base.ConfigureEndpoints(app);
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleAutoModelGuide>().Register();
    }
}

public class ModuleAutoControllersGuide : MoModuleGuide<ModuleAutoControllers, ModuleAutoControllersOption,
    ModuleAutoControllersGuide>
{
    internal ModuleAutoControllersGuide ConfigureServices(Action<MvcOptions>? setupAction = null)
    {
        ConfigureServices(nameof(ConfigureServices), context =>
        {
            context.Services.AddControllers(options =>
            {
                options.ConfigMoMvcOptions(context.Services);

                setupAction?.Invoke(options);
            }).ConfigureApplicationPartManager(manager =>
            {
                //manager.ApplicationParts.RemoveAll();
                //用于在ApplicationParts检测需要自定义添加的Controller
                manager.FeatureProviders.Add(
                    ActivatorUtilities
                        .CreateInstance<MoConventionalCrudControllerFeatureProvider>(context.Services.BuildServiceProvider()));

                //TODO 可能可以减少搜索Assembly以优化效率
                //增加搜索assembly
                //manager.ApplicationParts.AddIfNotContains();
            }).AddControllersAsServices();
        });
        return this;
    }

}

public class ModuleAutoControllersOption : IMoModuleOption<ModuleAutoControllers>
{
}
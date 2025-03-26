using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DomainDrivenDesign.AutoController.Components;
using MoLibrary.DomainDrivenDesign.AutoController.Extensions;
using MoLibrary.DomainDrivenDesign.AutoController.Features;
using MoLibrary.DomainDrivenDesign.AutoController.Interfaces;
using MoLibrary.DomainDrivenDesign.AutoController.Settings;

namespace MoLibrary.DomainDrivenDesign.AutoController;

public static class ServicesCollectionExtensions
{
    /// <summary>
    ///  Adds auto generated controllers for all aggregates in the application to the service collection.
    /// </summary>
    public static IMvcBuilder AddMoControllers(this IServiceCollection services,
        Action<MvcOptions>? setupAction = null, Action<MoCrudControllerOption>? crudOptionAction = null)
    {
        services.AddTransient<IMoServiceConvention, MoCrudControllerServiceConvention>();
        services.AddTransient<IApiDescriptionProvider, MoCrudApiDescriptionProvider>();
        services.AddTransient<IMoConventionalRouteBuilder, MoConventionalRouteBuilder>();
        services.AddSingleton<MoResultFilterMvc>();

        services.Configure<MoCrudControllerOption>(options =>
        {
            crudOptionAction?.Invoke(options);
        });


        //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi?view=aspnetcore-7.0

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

        // if you use v6's "minimal APIs" https://stackoverflow.com/questions/71932980/what-is-addendpointsapiexplorer-in-asp-net-core-6
        services.AddEndpointsApiExplorer();//使得Minimal api 支持Swagger? 似乎也不影响


        return services.AddControllers(options =>
           {
               options.ConfigMoMvcOptions(services);
               
               setupAction?.Invoke(options);
           }).ConfigureApplicationPartManager(manager =>
        {
            //用于在ApplicationParts检测需要自定义添加的Controller
            manager.FeatureProviders.Add(
                ActivatorUtilities
                    .CreateInstance<MoConventionalCrudControllerFeatureProvider>(services.BuildServiceProvider()));

            //TODO 可能可以减少搜索Assembly以优化效率
            //增加搜索assembly
            //manager.ApplicationParts.AddIfNotContains();
        }).AddControllersAsServices();
    }
    public static void UseEndpointsAutoControllers(this IApplicationBuilder app)
    {
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
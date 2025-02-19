using BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Features;
using Google.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;


namespace BuildingBlocksPlatform.DomainDrivenDesign.AutoController.Extensions;

public static class ServicesCollectionExtensions
{
    /// <summary>
    ///  Adds auto generated controllers for all aggregates in the application to the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="setupAction"></param>
    /// <returns></returns>
    public static IMvcBuilder AddAutoControllers(this IServiceCollection services,
        Action<MvcOptions>? setupAction = null)
    {

        return services.AddControllers(options =>
           {
               options.ConfigMoMvcOptions(services);
               
               setupAction?.Invoke(options);
           }).ConfigureApplicationPartManager(manager =>
        {
            //用于在ApplicationParts检测需要自定义添加的Controller
            manager.FeatureProviders.Add(
                ActivatorUtilities
                    .CreateInstance<MoConventionalControllerFeatureProvider>(services.BuildServiceProvider()));

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
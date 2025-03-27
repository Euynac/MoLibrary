using Microsoft.Extensions.DependencyInjection;
using MoLibrary.DependencyInjection.DynamicProxy;
using MoLibrary.DependencyInjection.DynamicProxy.DefaultInterceptors;
using MoLibrary.DomainDrivenDesign.Interfaces;
using MoLibrary.Logging;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.DomainDrivenDesign;

public static class ServicesCollectionExtensions
{
    /// <summary>
    /// 注册领域驱动设计服务
    /// </summary>
    /// <param name="services"></param>
    public static void AddMoDomainDrivenDesign(this IServiceCollection services)
    {
        //TODO 优化无需AOP
        services.AddMoInterceptor<PropertyInjectServiceProviderEmptyInterceptor>().CreateProxyWhenSatisfy(
            c =>
            {
                if (c.ImplementationType.IsAssignableTo<IMoDomainService>() ||
                    c.ImplementationType.IsAssignableTo<IMoApplicationService>())
                {
                    GlobalLog.LogInformation($"service inject:{c.ImplementationType.FullName}");
                    return true;
                }

                return false;
            });
    }
}
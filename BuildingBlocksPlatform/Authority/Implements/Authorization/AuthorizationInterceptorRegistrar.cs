using System.Reflection;
using BuildingBlocksPlatform.DependencyInjection.DynamicProxy;
using BuildingBlocksPlatform.DomainDrivenDesign.Interfaces;
using BuildingBlocksPlatform.SeedWork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Authority.Implements.Authorization;

/// <summary>
/// 注册非Controller的，用于DomainService、ApplicationService等Authorize标签中间件
/// </summary>
public static class AuthorizationInterceptorRegistrar
{
    public static void AddAuthorizationInterceptor(this IServiceCollection services)
    {
        services.AddMoInterceptor<AuthorizationInterceptor>().CreateProxyWhenSatisfy((descriptor) =>
        {
            if (ShouldIntercept(descriptor.ImplementationType))
            {
                //TODO 支持对Controller、OurCRUD进行权限验证
                GlobalLog.LogInformation("注入权限验证：{name}", descriptor.ImplementationType.Name);
                return true;
            }

            return false;
        });

    }

    private static bool ShouldIntercept(Type type)
    {
        return type.IsDefined(typeof(AuthorizeAttribute), true) || AnyMethodHasAuthorizeAttribute(type);
        //return type.IsAssignableTo<IMoApplicationService>(); //type.IsDefined(typeof(AuthorizeAttribute), true) || AnyMethodHasAuthorizeAttribute(type) || 
    }

    private static bool AnyMethodHasAuthorizeAttribute(Type implementationType)
    {
        return implementationType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(HasAuthorizeAttribute);
    }

    private static bool HasAuthorizeAttribute(MemberInfo methodInfo)
    {
        return methodInfo.IsDefined(typeof(AuthorizeAttribute), true);
    }
}

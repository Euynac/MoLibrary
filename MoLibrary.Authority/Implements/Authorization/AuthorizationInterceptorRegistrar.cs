using System.Reflection;
using Microsoft.AspNetCore.Authorization;

namespace MoLibrary.Authority.Implements.Authorization;

/// <summary>
/// 注册非Controller的，用于DomainService、ApplicationService等Authorize标签中间件
/// </summary>
public static class AuthorizationInterceptorRegistrar
{
    public static bool ShouldIntercept(Type type)
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

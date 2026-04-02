using System.Reflection;
using Microsoft.AspNetCore.Authorization;

namespace Monica.Authority.Authorization.Services.Support;

/// <summary>
/// Register non-controller types so DomainService, ApplicationService, and similar services can leverage Authorize-based middleware
/// </summary>
public static class InterceptionRegistrar
{
    public static bool ShouldIntercept(Type type)
    {
        return type.IsDefined(typeof(AuthorizeAttribute), true) || AnyMethodHasAuthorizeAttribute(type);
        //return type.IsAssignableTo<IApplicationService>(); //type.IsDefined(typeof(AuthorizeAttribute), true) || AnyMethodHasAuthorizeAttribute(type) || 
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

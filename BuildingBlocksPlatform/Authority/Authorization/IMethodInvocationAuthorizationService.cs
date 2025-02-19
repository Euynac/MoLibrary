using System.Reflection;
using System.Security.Claims;

namespace BuildingBlocksPlatform.Authority.Authorization;

public interface IMethodInvocationAuthorizationService
{
    Task CheckAsync(MethodInvocationAuthorizationContext context);
}
public class MethodInvocationAuthorizationContext(MethodInfo method, ClaimsPrincipal user)
{
    public ClaimsPrincipal User { get; } = user;
    public MethodInfo Method { get; } = method;
}

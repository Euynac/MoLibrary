using System.Reflection;
using System.Security.Claims;

namespace Monica.Authority.Authorization.Models;

public class MethodInvocationAuthorizationContext(MethodInfo method, ClaimsPrincipal user)
{
    public ClaimsPrincipal User { get; } = user;
    public MethodInfo Method { get; } = method;
}
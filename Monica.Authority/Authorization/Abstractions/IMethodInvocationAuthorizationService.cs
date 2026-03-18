using Monica.Authority.Authorization.Models;

namespace Monica.Authority.Authorization.Abstractions;

public interface IMethodInvocationAuthorizationService
{
    Task CheckAsync(MethodInvocationAuthorizationContext context);
}
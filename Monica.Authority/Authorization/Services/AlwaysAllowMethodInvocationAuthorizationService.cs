using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Authorization.Models;

namespace Monica.Authority.Authorization.Services;

public class AlwaysAllowMethodInvocationAuthorizationService : IMethodInvocationAuthorizationService
{
    public Task CheckAsync(MethodInvocationAuthorizationContext context)
    {
        return Task.CompletedTask;
    }
}

namespace BuildingBlocksPlatform.Authority.Authorization;

public class AlwaysAllowMethodInvocationAuthorizationService : IMethodInvocationAuthorizationService
{
    public Task CheckAsync(MethodInvocationAuthorizationContext context)
    {
        return Task.CompletedTask;
    }
}

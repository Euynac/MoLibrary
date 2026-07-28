using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Monica.Core.Execution;

namespace Monica.Authority.Authorization.Services.Support;

internal static class ExecutionAuthorizationMetadata
{
    public static bool RequiresAuthorization(ExecutionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return !AllowsAnonymous(descriptor.EntryMethod, descriptor.ComponentType)
               && GetAuthorizationData(descriptor.EntryMethod, descriptor.ComponentType).Any();
    }

    public static bool AllowsAnonymous(MethodInfo? entryMethod, Type componentType)
    {
        return entryMethod?.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any() is true
               || componentType.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any();
    }

    public static IEnumerable<IAuthorizeData> GetAuthorizationData(
        MethodInfo? entryMethod,
        Type componentType)
    {
        var componentData = componentType.GetCustomAttributes(true).OfType<IAuthorizeData>();
        return entryMethod is null
            ? componentData
            : entryMethod.GetCustomAttributes(true).OfType<IAuthorizeData>().Concat(componentData);
    }
}

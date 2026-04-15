using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Monica.Core.Modularity.Annotations;
using Monica.WebApi.Swagger.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Monica.WebApi.Swagger.Services.Support;

/// <summary>
/// Resolves which Swagger document should own a given API description.
/// </summary>
internal static class SwaggerDocumentKindResolver
{
    private static readonly ConcurrentDictionary<Assembly, bool> MonicaFrameworkAssemblyCache = new();

    public static ESwaggerDocumentKind Resolve(
        ApiDescription description,
        Func<ApiDescription, ESwaggerDocumentKind?>? overrideResolver)
    {
        var overriddenKind = overrideResolver?.Invoke(description);
        if (overriddenKind.HasValue)
        {
            return overriddenKind.Value;
        }

        var declaringAssembly = ResolveDeclaringAssembly(description);
        return declaringAssembly is not null && IsMonicaFrameworkAssembly(declaringAssembly)
            ? ESwaggerDocumentKind.Monica
            : ESwaggerDocumentKind.Business;
    }

    public static bool IsMonicaFrameworkAssembly(Assembly assembly)
    {
        return MonicaFrameworkAssemblyCache.GetOrAdd(assembly, static currentAssembly =>
        {
            foreach (var type in GetLoadableTypes(currentAssembly))
            {
                var moduleKeyAttribute = type.GetCustomAttribute<ModuleKeyAttribute>();
                if (moduleKeyAttribute?.Key.IsBuiltIn == true)
                {
                    return true;
                }
            }

            return false;
        });
    }

    private static Assembly? ResolveDeclaringAssembly(ApiDescription description)
    {
        if (description.ActionDescriptor is ControllerActionDescriptor controllerAction)
        {
            return controllerAction.ControllerTypeInfo.Assembly;
        }

        if (description.TryGetMethodInfo(out var methodInfo))
        {
            return methodInfo.DeclaringType?.Assembly ?? methodInfo.Module.Assembly;
        }

        foreach (var metadata in description.ActionDescriptor.EndpointMetadata)
        {
            switch (metadata)
            {
                case MethodInfo endpointMethod:
                    return endpointMethod.DeclaringType?.Assembly ?? endpointMethod.Module.Assembly;
                case Type endpointType:
                    return endpointType.Assembly;
            }
        }

        return null;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}

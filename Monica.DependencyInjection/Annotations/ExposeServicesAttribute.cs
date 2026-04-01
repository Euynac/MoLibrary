using System.Reflection;
using Monica.DependencyInjection.Abstractions.Internal;
using Monica.Tool.Extensions;

namespace Monica.DependencyInjection.Annotations;

/// <summary>
/// Declares which service types should be exposed when Monica conventionally registers the annotated implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExposeServicesAttribute(params Type[] serviceTypes) : Attribute, IExposedServiceTypesProvider
{
    /// <summary>
    /// Gets the explicitly declared service types.
    /// </summary>
    public Type[] ServiceTypes { get; } = serviceTypes;

    /// <summary>
    /// Gets or sets a value indicating whether matching interface conventions should be added automatically.
    /// </summary>
    public bool IncludeDefaults { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the concrete implementation type should also be exposed.
    /// </summary>
    public bool IncludeSelf { get; set; }

    public Type[] GetExposedServiceTypes(Type targetType)
    {
        var serviceList = ServiceTypes.ToList();

        if (IncludeDefaults)
        {
            foreach (var type in GetDefaultServices(targetType))
            {
                serviceList.AddIfNotContains(type);
            }

        }

        if (IncludeSelf)
        {
            serviceList.AddIfNotContains(targetType);
        }

        return [..serviceList];
    }

    private static List<Type> GetDefaultServices(Type type)
    {
        var serviceTypes = new List<Type>();

        foreach (var interfaceType in type.GetTypeInfo().GetInterfaces())
        {
            var interfaceName = interfaceType.Name;
            var typeName = type.Name;
            if (type.IsGenericType)
            {
                typeName = type.Name[..typeName.IndexOf('`')];
            }

            if (interfaceType.IsGenericType)
            {
                interfaceName = interfaceType.Name[..interfaceType.Name.IndexOf('`')];
            }

            if (interfaceName.StartsWith('I'))
            {
                interfaceName = interfaceName.Right(interfaceName.Length - 1);
            }

            if (typeName.EndsWith(interfaceName, StringComparison.OrdinalIgnoreCase))
            {
                serviceTypes.Add(interfaceType);
            }
        }

        return serviceTypes;
    }
}

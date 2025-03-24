using System.Reflection;
using MoLibrary.DependencyInjection.Implements;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Utils;

namespace MoLibrary.DependencyInjection.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExposeServicesAttribute(params Type[] serviceTypes) : Attribute, IExposedServiceTypesProvider
{
    public Type[] ServiceTypes { get; } = serviceTypes;

    public bool IncludeDefaults { get; set; }

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

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExposeKeyedServiceAttribute<TServiceType> : Attribute, IExposedKeyedServiceTypesProvider
    where TServiceType : class
{
    public ServiceIdentifier ServiceIdentifier { get; }

    public ExposeKeyedServiceAttribute(object serviceKey)
    {
        if (serviceKey == null)
        {
            throw new Exception($"{nameof(serviceKey)} can not be null! Use {nameof(ExposeServicesAttribute)} instead.");
        }

        ServiceIdentifier = new ServiceIdentifier(serviceKey, typeof(TServiceType));
    }

    public ServiceIdentifier[] GetExposedServiceTypes(Type targetType)
    {
        return [ServiceIdentifier];
    }
}

public interface IExposedServiceTypesProvider
{
    Type[] GetExposedServiceTypes(Type targetType);
}

public interface IExposedKeyedServiceTypesProvider
{
    ServiceIdentifier[] GetExposedServiceTypes(Type targetType);
}


public static class ExposedServiceExplorer
{
    private static readonly ExposeServicesAttribute _defaultExposeServicesAttribute =
        new()
        {
            IncludeDefaults = true,
            IncludeSelf = true
        };

    public static List<Type> GetExposedServices(Type type)
    {
        var exposedServiceTypesProviders = type
            .GetCustomAttributes(true)
            .OfType<IExposedServiceTypesProvider>()
            .ToList();

        if (exposedServiceTypesProviders.IsNullOrEmptySet() && type.GetCustomAttributes(true).OfType<IExposedKeyedServiceTypesProvider>().Any())
        {
            // If there is any IExposedKeyedServiceTypesProvider but no IExposedServiceTypesProvider, we will not expose the default services.
            return [];
        }

        return exposedServiceTypesProviders
            .DefaultIfEmpty(_defaultExposeServicesAttribute)
            .SelectMany(p => p.GetExposedServiceTypes(type))
            .Distinct()
            .ToList();
    }

    public static List<ServiceIdentifier> GetExposedKeyedServices(Type type)
    {
        return type
            .GetCustomAttributes(true)
            .OfType<IExposedKeyedServiceTypesProvider>()
            .SelectMany(p => p.GetExposedServiceTypes(type))
            .Distinct()
            .ToList();
    }
}
public interface IOnServiceExposingContext
{
    Type ImplementationType { get; }

    List<ServiceIdentifier> ExposedTypes { get; }
}

public class OnServiceExposingContext : IOnServiceExposingContext
{
    public Type ImplementationType { get; }

    public List<ServiceIdentifier> ExposedTypes { get; }

    public OnServiceExposingContext(Type implementationType, List<Type> exposedTypes)
    {
        ImplementationType = Check.NotNull(implementationType, nameof(implementationType));
        ExposedTypes = Check.NotNull(exposedTypes, nameof(exposedTypes)).ConvertAll(t => new ServiceIdentifier(t));
    }

    public OnServiceExposingContext(Type implementationType, List<ServiceIdentifier> exposedTypes)
    {
        ImplementationType = Check.NotNull(implementationType, nameof(implementationType));
        ExposedTypes = Check.NotNull(exposedTypes, nameof(exposedTypes));
    }
}

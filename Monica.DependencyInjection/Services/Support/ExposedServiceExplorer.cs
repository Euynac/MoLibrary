using Monica.DependencyInjection.Abstractions.Internal;
using Monica.DependencyInjection.Annotations;
using Monica.DependencyInjection.Models.Internal;
using Monica.Tool.Extensions;

namespace Monica.DependencyInjection.Services.Support;

internal static class ExposedServiceExplorer
{
    private static readonly ExposeServicesAttribute DefaultExposeServicesAttribute =
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

        if (exposedServiceTypesProviders.IsNullOrEmptySet() &&
            type.GetCustomAttributes(true).OfType<IExposedKeyedServiceTypesProvider>().Any())
        {
            // If there is any keyed-service exposure but no regular exposure, suppress the default service set.
            return [];
        }

        return exposedServiceTypesProviders
            .DefaultIfEmpty(DefaultExposeServicesAttribute)
            .SelectMany(provider => provider.GetExposedServiceTypes(type))
            .Distinct()
            .ToList();
    }

    public static List<ServiceIdentifier> GetExposedKeyedServices(Type type)
    {
        return type
            .GetCustomAttributes(true)
            .OfType<IExposedKeyedServiceTypesProvider>()
            .SelectMany(provider => provider.GetExposedServiceTypes(type))
            .Distinct()
            .ToList();
    }
}

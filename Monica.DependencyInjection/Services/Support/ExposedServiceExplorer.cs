using Monica.Core.TypeDiscovery.Models;
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

    public static List<Type> GetExposedServices(
        BusinessTypeShape shape,
        IReadOnlyList<Attribute> inheritedAttributes)
    {
        var exposedServiceTypesProviders = inheritedAttributes
            .OfType<IExposedServiceTypesProvider>()
            .ToList();

        if (exposedServiceTypesProviders.IsNullOrEmptySet() &&
            inheritedAttributes.OfType<IExposedKeyedServiceTypesProvider>().Any())
        {
            // If there is any keyed-service exposure but no regular exposure, suppress the default service set.
            return [];
        }

        return exposedServiceTypesProviders
            .DefaultIfEmpty(DefaultExposeServicesAttribute)
            .SelectMany(provider => provider.GetExposedServiceTypes(shape))
            .Distinct()
            .ToList();
    }

    public static List<ServiceIdentifier> GetExposedKeyedServices(
        IReadOnlyList<Attribute> inheritedAttributes)
    {
        return inheritedAttributes
            .OfType<IExposedKeyedServiceTypesProvider>()
            .SelectMany(provider => provider.GetExposedServiceTypes())
            .Distinct()
            .ToList();
    }
}

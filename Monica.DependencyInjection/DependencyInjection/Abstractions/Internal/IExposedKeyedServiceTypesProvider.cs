using Monica.DependencyInjection.DependencyInjection.Models.Internal;

namespace Monica.DependencyInjection.DependencyInjection.Abstractions.Internal;

internal interface IExposedKeyedServiceTypesProvider
{
    ServiceIdentifier[] GetExposedServiceTypes(Type targetType);
}

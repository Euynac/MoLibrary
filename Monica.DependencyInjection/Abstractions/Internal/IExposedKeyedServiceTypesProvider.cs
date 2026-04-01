using Monica.DependencyInjection.Models.Internal;

namespace Monica.DependencyInjection.Abstractions.Internal;

internal interface IExposedKeyedServiceTypesProvider
{
    ServiceIdentifier[] GetExposedServiceTypes(Type targetType);
}

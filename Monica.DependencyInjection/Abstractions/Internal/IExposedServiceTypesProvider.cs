using Monica.Core.TypeDiscovery.Models;

namespace Monica.DependencyInjection.Abstractions.Internal;

internal interface IExposedServiceTypesProvider
{
    Type[] GetExposedServiceTypes(BusinessTypeShape targetShape);
}

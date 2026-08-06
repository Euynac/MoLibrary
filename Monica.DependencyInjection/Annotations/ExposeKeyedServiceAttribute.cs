using Monica.DependencyInjection.Abstractions.Internal;
using Monica.DependencyInjection.Models.Internal;

namespace Monica.DependencyInjection.Annotations;

/// <summary>
/// Declares a keyed service exposure for the annotated implementation type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class ExposeKeyedServiceAttribute<TServiceType>(object serviceKey) : Attribute, IExposedKeyedServiceTypesProvider
    where TServiceType : class
{
    /// <summary>
    /// Gets the key that will be associated with the exposed service.
    /// </summary>
    public object ServiceKey { get; } = serviceKey ?? throw new ArgumentNullException(nameof(serviceKey));

    ServiceIdentifier[] IExposedKeyedServiceTypesProvider.GetExposedServiceTypes()
    {
        return [new ServiceIdentifier(ServiceKey, typeof(TServiceType))];
    }
}

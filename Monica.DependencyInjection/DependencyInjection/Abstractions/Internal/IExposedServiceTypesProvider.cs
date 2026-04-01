namespace Monica.DependencyInjection.DependencyInjection.Abstractions.Internal;

internal interface IExposedServiceTypesProvider
{
    Type[] GetExposedServiceTypes(Type targetType);
}

namespace MoLibrary.Core.Features.MoDecorator;

public class OpenGenericDecorationStrategy(
    Type serviceType,
    string? serviceKey,
    Type? decoratorType,
    Func<object, IServiceProvider, object>? decoratorFactory)
    : DecorationStrategy(serviceType, serviceKey)
{
    private Type? DecoratorType { get; } = decoratorType;

    private Func<object, IServiceProvider, object>? DecoratorFactory { get; } = decoratorFactory;

    protected override bool CanDecorate(Type serviceType) =>
        serviceType is { IsGenericType: true, IsGenericTypeDefinition: false}
        && serviceType.GetGenericTypeDefinition() == ServiceType.GetGenericTypeDefinition()
        && (DecoratorType is null || HasCompatibleGenericArguments(serviceType, DecoratorType));

    public override Func<IServiceProvider, object?, object> CreateDecorator(Type serviceType, string serviceKey)
    {
        if (DecoratorType is not null)
        {
            var genericArguments = serviceType.GetGenericArguments();
            var closedDecorator = DecoratorType.MakeGenericType(genericArguments);

            return TypeDecorator(serviceType, serviceKey, closedDecorator);
        }

        if (DecoratorFactory is not null)
        {
            return FactoryDecorator(serviceType, serviceKey, DecoratorFactory);
        }

        throw new InvalidOperationException($"Both {nameof(DecoratorType)} and {nameof(DecoratorFactory)} can not be null.");
    }

    public static bool HasCompatibleGenericArguments(Type type, Type genericTypeDefinition)
    {
        var genericArguments = type.GetGenericArguments();
        try
        {
            _ = genericTypeDefinition.MakeGenericType(genericArguments);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

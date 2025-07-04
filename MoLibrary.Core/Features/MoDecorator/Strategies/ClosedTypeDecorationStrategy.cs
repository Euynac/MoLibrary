namespace MoLibrary.Core.Features.MoDecorator.Strategies;

internal sealed class ClosedTypeDecorationStrategy(
    Type serviceType,
    string? serviceKey,
    Type? decoratorType,
    Func<object, IServiceProvider, object>? decoratorFactory)
    : DecorationStrategy(serviceType, serviceKey)
{
    private Type? DecoratorType { get; } = decoratorType;

    private Func<object, IServiceProvider, object>? DecoratorFactory { get; } = decoratorFactory;

    protected override bool CanDecorate(Type serviceType) => ServiceType == serviceType;

    public override Func<IServiceProvider, object?, object> CreateDecorator(Type serviceType, string serviceKey)
    {
        if (DecoratorType is not null)
        {
            return TypeDecorator(serviceType, serviceKey, DecoratorType);
        }

        if (DecoratorFactory is not null)
        {
            return FactoryDecorator(serviceType, serviceKey, DecoratorFactory);
        }

        throw new InvalidOperationException($"Both {nameof(DecoratorType)} and {nameof(DecoratorFactory)} can not be null.");
    }
}

namespace MoLibrary.Core.Features.MoDecorator;

public class DecorationException(DecorationStrategy strategy) : MissingTypeRegistrationException(strategy.ServiceType)
{
    public DecorationStrategy Strategy { get; } = strategy;
}

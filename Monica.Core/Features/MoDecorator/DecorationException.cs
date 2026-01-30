using Monica.Core.Features.MoDecorator.Strategies;

namespace Monica.Core.Features.MoDecorator;

public class DecorationException(DecorationStrategy strategy) : MissingTypeRegistrationException(strategy.ServiceType)
{
    public DecorationStrategy Strategy { get; } = strategy;
}

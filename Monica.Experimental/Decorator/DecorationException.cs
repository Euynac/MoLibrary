using Monica.Experimental.Decorator.Strategies;

namespace Monica.Experimental.Decorator;

public class DecorationException(DecorationStrategy strategy) : MissingTypeRegistrationException(strategy.ServiceType)
{
    public DecorationStrategy Strategy { get; } = strategy;
}

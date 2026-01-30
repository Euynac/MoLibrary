using Monica.Tool.Extensions;

namespace Monica.Core.Features.MoDecorator;

public class MissingTypeRegistrationException(Type serviceType)
    : InvalidOperationException($"Could not find any registered services for type '{serviceType.GetCleanFullName()}'.")
{
    public Type ServiceType { get; } = serviceType;
}

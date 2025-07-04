using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Features.MoDecorator;

public class MissingTypeRegistrationException(Type serviceType)
    : InvalidOperationException($"Could not find any registered services for type '{serviceType.GetCleanFullName()}'.")
{
    public Type ServiceType { get; } = serviceType;
}

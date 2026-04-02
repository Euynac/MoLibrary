using Monica.DependencyInjection.Abstractions;

namespace Monica.WebApi.Abstractions;

/// <summary>
/// This interface can be implemented by all domain services to identify them by convention.
/// </summary>
public interface IDomainService : ITransientDependency
{

}

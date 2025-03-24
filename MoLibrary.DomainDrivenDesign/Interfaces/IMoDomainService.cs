using MoLibrary.DependencyInjection.AppInterfaces;

namespace MoLibrary.DomainDrivenDesign.Interfaces;

/// <summary>
/// This interface can be implemented by all domain services to identify them by convention.
/// </summary>
public interface IMoDomainService : ITransientDependency
{

}

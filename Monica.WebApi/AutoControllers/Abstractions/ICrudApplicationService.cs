using Monica.Core.Execution;

namespace Monica.WebApi.AutoControllers.Abstractions;

/// <summary>
/// Marker interface used to identify application services that participate in automatic CRUD controller generation.
/// </summary>
public interface ICrudApplicationService : IExecutionAdapterOwnedComponent
{
}

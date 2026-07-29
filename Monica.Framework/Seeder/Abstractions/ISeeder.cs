using Monica.Core.Execution;

namespace Monica.Framework.Seeder.Abstractions;

/// <summary>
/// Defines one host-startup seed operation executed through Monica's seeder adapter.
/// </summary>
public interface ISeeder : IExecutionAdapterOwnedComponent
{
    /// <summary>
    /// Executes one startup seed operation.
    /// </summary>
    /// <param name="cancellationToken">Signals that host startup is being cancelled.</param>
    Task SeedAsync(CancellationToken cancellationToken);
}

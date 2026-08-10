using Monica.Framework.Seeder.Models;

namespace Monica.Framework.Seeder.Abstractions;

/// <summary>
/// Exposes the current host-owned seeder state without permitting consumers to mutate scheduler state.
/// </summary>
public interface ISeederState
{
    /// <summary>
    /// Captures one internally consistent snapshot of every discovered seeder.
    /// </summary>
    SeederStateSnapshot GetSnapshot();
}

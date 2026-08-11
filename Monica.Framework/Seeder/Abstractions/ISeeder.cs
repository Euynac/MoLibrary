namespace Monica.Framework.Seeder.Abstractions;

/// <summary>
/// Defines one finite, idempotent seed operation executed after the host has started.
/// </summary>
public interface ISeeder
{
    /// <summary>
    /// Executes one seed attempt through Monica's automatic-transaction execution pipeline.
    /// </summary>
    /// <param name="cancellationToken">
    /// Signals that the host is stopping or that another Seeder activated the FailFast run policy. A FailFast
    /// cancellation aborts only the current Seeder run; implementations must not infer that the host is stopping.
    /// </param>
    Task SeedAsync(CancellationToken cancellationToken);
}

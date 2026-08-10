using Microsoft.Extensions.Logging;
using Monica.Framework.Seeder.Abstractions;

namespace Monica.Framework.Seeder.Services;

/// <summary>
/// Base class for a finite, idempotent seed operation scheduled after application startup.
/// </summary>
public abstract class SeederBase : ISeeder
{
    /// <summary>
    /// Gets the host-owned logger for this seeder.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Initializes the seeder with logging owned by the current host.
    /// </summary>
    /// <param name="logger">The logger for the concrete seeder type.</param>
    protected SeederBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public virtual Task SeedAsync(CancellationToken cancellationToken)
    {
        return SeedingAsync(cancellationToken);
    }

    /// <summary>
    /// Implements the seeder's finite startup work.
    /// </summary>
    /// <param name="cancellationToken">Signals that the host is stopping.</param>
    public abstract Task SeedingAsync(CancellationToken cancellationToken);
}

using Microsoft.Extensions.Logging;
using Monica.Framework.Seeder.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.Framework.Seeder.Services;

/// <summary>
/// Specify that this class is a seed class, which will be automatically executed after starting the service.
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

    public virtual async Task SeedAsync()
    {
        try
        {
            await SeedingAsync();
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Seeder {SeederType} failed", GetType().GetCleanFullName());
        }

    }

    public abstract Task SeedingAsync();
}

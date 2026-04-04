using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.Tool.Extensions;

namespace Monica.Framework.MoSeeder;

/// <summary>
/// Specify that this class is a seed class, which will be automatically executed after starting the service.
/// </summary>
public abstract class MoSeeder : IMoSeeder
{

    /// <summary>
    /// Lazy-loaded logger instance for this module guide.
    /// </summary>
    private readonly Lazy<ILogger> _loggerLazy;

    /// <summary>
    /// Gets the logger instance for this module guide.
    /// </summary>
    public ILogger Logger => _loggerLazy.Value;

    protected MoSeeder()
    {
        _loggerLazy = new Lazy<ILogger>(() => LogManager.For(GetType()));
    }

    public virtual async Task SeedAsync()
    {
        try
        {
            await SeedingAsync();
        }
        catch (Exception e)
        {
            Logger.LogError(e, $"Seeder:{GetType().GetCleanFullName()} 出现异常");
        }

    }

    public abstract Task SeedingAsync();
}

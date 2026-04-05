using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.Framework.Seeder.Abstractions;
using Monica.Tool.Extensions;

namespace Monica.Framework.Seeder.Services;

/// <summary>
/// Specify that this class is a seed class, which will be automatically executed after starting the service.
/// </summary>
public abstract class SeederBase : ISeeder
{

    /// <summary>
    /// Lazy-loaded logger instance for this module guide.
    /// </summary>
    private readonly Lazy<ILogger> _loggerLazy;

    /// <summary>
    /// Gets the logger instance for this module guide.
    /// </summary>
    public ILogger Logger => _loggerLazy.Value;

    protected SeederBase()
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

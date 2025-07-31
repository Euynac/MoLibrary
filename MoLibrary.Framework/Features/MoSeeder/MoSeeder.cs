using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Framework.Features.MoSeeder;

/// <summary>
/// 指定该类是种子类，启动服务后将会自动执行一遍
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
        _loggerLazy = new Lazy<ILogger>(() => LogProvider.For(GetType()));
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
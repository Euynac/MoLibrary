using Microsoft.Extensions.Logging;
using MoLibrary.Authority.Security;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MoLibrary.Framework.Logging;

public static class CustomLoggerExtensions
{
    /// <summary>
    /// 关键字
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="logger"></param>
    /// <param name="ourKey"></param>
    /// <param name="message"></param>
    public static void LogInformation<T>(this ILogger<T> logger, object? ourKey, string message ) where T : class
    {
        var log = logger.SetNewLogger<T>(ourKey.ToJsonString()!.Trim('\"'),null);
        log.LogInformation(message);
    }
    /// <summary>
    /// 用户
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="logger"></param>
    /// <param name="user"></param>
    /// <param name="message"></param>
    public static void LogInformation<T>(this ILogger<T> logger, IOurCurrentUser? user, string message) where T : class
    {
        var log = logger.SetNewLogger(null, user);
        log.LogInformation(message);
    }
    /// <summary>
    /// 关键字 and 用户
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="logger"></param>
    /// <param name="ourKey"></param>
    /// <param name="user"></param>
    /// <param name="message"></param>
    public static void LogInformation<T>(this ILogger<T> logger, object? ourKey, IOurCurrentUser? user, string message) where T : class
    {
        var log = logger.SetNewLogger(ourKey.ToJsonString(false), user);
        log.LogInformation(message);
    }

    //public static void LogError<T>(this ILogger<T> logger, object ourKey, string message) where T : class
    //{
    //    var log = logger.SetKey<T>(ourKey.ToJsonString(false)!);
    //    log.LogError(message);
    //}

    public static ILogger SetNewLogger<T>(this ILogger<T> logger, string? key , IOurCurrentUser? user) where T : class
    {
        var log = Log.Logger;
        if (!key.IsNullOrWhiteSpace())
        {
            log = log.ForContext("OurKey", $"[K:{key}]");
        }
        if (user != null )
        {
            log =  log.ForContext("User",$"[U:{new { user.Username, user.IpAddress, user.Id }.ToJsonString(false)}]" ) ;
        }

        return new SerilogLoggerFactory(log).CreateLogger(typeof(T).FullName?? typeof(T).Name);
    }
    public static ILogger CreateNewLogger<T>() where T : class
    {
        var log = Log.Logger;
        return new SerilogLoggerFactory(log).CreateLogger(typeof(T).FullName ?? typeof(T).Name);
    }

}
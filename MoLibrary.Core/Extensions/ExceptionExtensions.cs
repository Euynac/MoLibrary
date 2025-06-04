using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;
using System.Text;

namespace MoLibrary.Core.Extensions;

public static class ExceptionExtensions
{
    /// <summary>
    /// 递归获取异常信息
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public static string GetMessageRecursively(this Exception e)
    {
        var curException = e;
        var sb = new StringBuilder();
        while (curException != null)
        {
            if (sb.Length != 0)
            {
                sb.Append(';');
            }
            sb.Append(curException.Message);
            curException = curException.InnerException;
        }
        return sb.ToString();
    }


    /// <summary>
    /// Uses <see cref="ExceptionDispatchInfo.Capture"/> method to re-throws exception
    /// while preserving stack trace.
    /// </summary>
    /// <param name="exception">Exception to be re-thrown</param>
    public static void ReThrow(this Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    /// <summary>
    /// Creates a new exception with formatted message and logs the error
    /// </summary>
    /// <param name="innerException">The inner exception to wrap</param>
    /// <param name="logger">The logger instance to log the error</param>
    /// <param name="message">The message template with placeholders</param>
    /// <param name="args">Arguments to format the message template</param>
    /// <returns>A new Exception with the formatted message and inner exception</returns>
    public static Exception CreateException(this Exception innerException, ILogger? logger, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[] args)
    {
        var msg = string.Format(message ?? "An error occurred", args);
        logger?.LogError(innerException, msg);
        return new Exception(msg, innerException);
    }


    public static void LogException(this ILogger logger, Exception ex, LogLevel level = LogLevel.Error)
    {
        logger.Log(level, ex, ex.Message);
        if (ex.Data.Count <= 0)
        {
            return;
        }

        var exceptionData = new StringBuilder();
        exceptionData.AppendLine("---------- Exception Data ----------");
        foreach (var key in ex.Data.Keys)
        {
            exceptionData.AppendLine($"{key} = {ex.Data[key]}");
        }

        logger.Log(level, exceptionData.ToString());
    }
}
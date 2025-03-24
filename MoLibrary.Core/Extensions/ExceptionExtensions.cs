using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.Extensions.Logging;

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
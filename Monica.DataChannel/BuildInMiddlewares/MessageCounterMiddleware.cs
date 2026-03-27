using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.BuildInMiddlewares;

/// <summary>
/// Example middleware that counts messages.
/// Inherits from <see cref="PipeInfoDisplayMiddlewareBase"/> and tracks message totals by category.
/// </summary>
public class MessageCounterMiddleware : PipeInfoDisplayMiddlewareBase
{
    /// <summary>
    /// Key for the total message counter.
    /// </summary>
    private const string TOTAL_MESSAGES_KEY = "消息总数";

    /// <summary>
    /// Key for the inbound message counter.
    /// </summary>
    private const string INPUT_MESSAGES_KEY = "输入消息数";

    /// <summary>
    /// Key for the outbound message counter.
    /// </summary>
    private const string OUTPUT_MESSAGES_KEY = "输出消息数";

    /// <summary>
    /// Key for the error message counter.
    /// </summary>
    private const string ERROR_MESSAGES_KEY = "错误消息数";

    /// <summary>
    /// Key for the last processed timestamp.
    /// </summary>
    private const string LAST_PROCESSED_KEY = "最后处理时间";

    /// <summary>
    /// Processes the data context synchronously.
    /// </summary>
    /// <param name="context">The data context.</param>
    /// <returns>The processed data context.</returns>
    public override DataContext Pass(DataContext context)
    {
        try
        {
            // Count every processed message.
            IncrementCounter(TOTAL_MESSAGES_KEY);

            // Count messages by their source side.
            switch (context.Source)
            {
                case EDataSource.Inner:
                    IncrementCounter(OUTPUT_MESSAGES_KEY);
                    break;
                case EDataSource.Outer:
                    IncrementCounter(INPUT_MESSAGES_KEY);
                    break;
            }
            
            // Update the last processed timestamp.
            SetInfo(LAST_PROCESSED_KEY, DateTime.Now);

            // Detect error messages by inspecting the payload or metadata.
            // Adjust this logic to match the actual business rules.
            if (IsErrorMessage(context))
            {
                IncrementCounter(ERROR_MESSAGES_KEY);
            }
            
            return context;
        }
        catch (Exception ex)
        {
            // Record middleware failures.
            IncrementCounter(ERROR_MESSAGES_KEY);
            SetInfo("最后异常", ex.Message);
            SetInfo("最后异常时间", DateTime.Now);

            // Return the original context so the data flow can continue.
            return context;
        }
    }

    /// <summary>
    /// Processes the data context asynchronously.
    /// </summary>
    /// <param name="context">The data context.</param>
    /// <returns>The processed data context.</returns>
    public override async Task<DataContext> PassAsync(DataContext context)
    {
        return await Task.FromResult(Pass(context));
    }

    /// <summary>
    /// Resets all counters.
    /// </summary>
    public void ResetCounters()
    {
        ResetCounter(TOTAL_MESSAGES_KEY);
        ResetCounter(INPUT_MESSAGES_KEY);
        ResetCounter(OUTPUT_MESSAGES_KEY);
        ResetCounter(ERROR_MESSAGES_KEY);
        SetInfo("重置时间", DateTime.Now);
    }

    /// <summary>
    /// Calculates the message-processing rate per minute.
    /// </summary>
    /// <returns>The number of messages processed per minute.</returns>
    public double GetMessagesPerMinute()
    {
        var totalMessages = GetInfo<long>(TOTAL_MESSAGES_KEY);
        var startTime = GetInfo<DateTime>("启动时间", DateTime.Now);
        var timeSpan = DateTime.Now - startTime;
        
        if (timeSpan.TotalMinutes < 0.1) return 0; // Avoid division by zero.
        
        return totalMessages / timeSpan.TotalMinutes;
    }

    /// <summary>
    /// Determines whether the message should be treated as an error message.
    /// Override or adjust this logic to match domain-specific rules.
    /// </summary>
    /// <param name="context">The data context.</param>
    /// <returns><see langword="true"/> if the message represents an error; otherwise, <see langword="false"/>.</returns>
    private bool IsErrorMessage(DataContext context)
    {
        // Example: check whether the payload contains error-related text.
        if (context.Data?.ToString()?.ToLower().Contains("error") == true)
            return true;

        // Example: check whether the metadata marks the message as an error.
        if (context.Metadata != null)
        {
            foreach (var kvp in context.Metadata)
            {
                if (kvp.Key.ToLower().Contains("error") || kvp.Key.ToLower().Contains("exception"))
                    return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Initializes the middleware-specific statistics.
    /// </summary>
    public void Initialize()
    {
        SetInfo("启动时间", DateTime.Now);
        SetInfo("中间件版本", "1.0.0");
        SetInfo("功能描述", "消息计数统计中间件");
    }
}

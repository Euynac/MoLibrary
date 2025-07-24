using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel.BuildInMiddlewares;

/// <summary>
/// 消息计数中间件示例
/// 继承自信息展示中间件基类，用于统计各种消息类型的数量
/// </summary>
public class MessageCounterMiddleware : PipeInfoDisplayMiddlewareBase
{
    /// <summary>
    /// 消息总数计数器键
    /// </summary>
    private const string TOTAL_MESSAGES_KEY = "消息总数";
    
    /// <summary>
    /// 输入消息计数器键
    /// </summary>
    private const string INPUT_MESSAGES_KEY = "输入消息数";
    
    /// <summary>
    /// 输出消息计数器键
    /// </summary>
    private const string OUTPUT_MESSAGES_KEY = "输出消息数";
    
    /// <summary>
    /// 错误消息计数器键
    /// </summary>
    private const string ERROR_MESSAGES_KEY = "错误消息数";
    
    /// <summary>
    /// 最后处理时间键
    /// </summary>
    private const string LAST_PROCESSED_KEY = "最后处理时间";

    /// <summary>
    /// 同步处理数据上下文
    /// </summary>
    /// <param name="context">数据上下文</param>
    /// <returns>处理后的数据上下文</returns>
    public override DataContext Pass(DataContext context)
    {
        try
        {
            // 统计消息总数
            IncrementCounter(TOTAL_MESSAGES_KEY);
            
            // 根据数据来源统计
            switch (context.Source)
            {
                case EDataSource.Inner:
                    IncrementCounter(OUTPUT_MESSAGES_KEY);
                    break;
                case EDataSource.Outer:
                    IncrementCounter(INPUT_MESSAGES_KEY);
                    break;
            }
            
            // 更新最后处理时间
            SetInfo(LAST_PROCESSED_KEY, DateTime.Now);
            
            // 可以通过检查数据内容或元数据来判断是否为错误消息
            // 开发者可以根据实际业务需求修改此处逻辑
            if (IsErrorMessage(context))
            {
                IncrementCounter(ERROR_MESSAGES_KEY);
            }
            
            return context;
        }
        catch (Exception ex)
        {
            // 记录处理异常
            IncrementCounter(ERROR_MESSAGES_KEY);
            SetInfo("最后异常", ex.Message);
            SetInfo("最后异常时间", DateTime.Now);
            
            // 返回原始上下文，不影响数据流
            return context;
        }
    }

    /// <summary>
    /// 异步处理数据上下文
    /// </summary>
    /// <param name="context">数据上下文</param>
    /// <returns>处理后的数据上下文</returns>
    public override async Task<DataContext> PassAsync(DataContext context)
    {
        return await Task.FromResult(Pass(context));
    }

    /// <summary>
    /// 重置所有计数器
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
    /// 获取消息处理速率（每分钟）
    /// </summary>
    /// <returns>每分钟处理的消息数</returns>
    public double GetMessagesPerMinute()
    {
        var totalMessages = GetInfo<long>(TOTAL_MESSAGES_KEY);
        var startTime = GetInfo<DateTime>("启动时间", DateTime.Now);
        var timeSpan = DateTime.Now - startTime;
        
        if (timeSpan.TotalMinutes < 0.1) return 0; // 避免除零
        
        return totalMessages / timeSpan.TotalMinutes;
    }

    /// <summary>
    /// 判断是否为错误消息
    /// 开发者可以根据业务需求修改此方法的判断逻辑
    /// </summary>
    /// <param name="context">数据上下文</param>
    /// <returns>是否为错误消息</returns>
    private bool IsErrorMessage(DataContext context)
    {
        // 示例：检查数据内容是否包含错误信息
        if (context.Data?.ToString()?.ToLower().Contains("error") == true)
            return true;
            
        // 示例：检查元数据中是否标记为错误
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
    /// 初始化方法，设置启动时间
    /// </summary>
    public void Initialize()
    {
        SetInfo("启动时间", DateTime.Now);
        SetInfo("中间件版本", "1.0.0");
        SetInfo("功能描述", "消息计数统计中间件");
    }
}
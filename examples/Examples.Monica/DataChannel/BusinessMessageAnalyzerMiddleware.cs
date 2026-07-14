using System.Text.Json;
using System.Text.RegularExpressions;
using Monica.DataChannel.Middlewares;
using Monica.DataChannel.Pipeline;

namespace Examples.Monica.DataChannel;

/// <summary>
/// Business message analysis middleware example
/// Inherited from the information display middleware base class, used to analyze and count business message types
/// Developers can modify this example to suit their business needs
/// </summary>
public sealed class BusinessMessageAnalyzerMiddleware : PipelineInfoDisplayMiddlewareBase
{
    /// <summary>
    /// Message type regular expression
    /// Can be modified according to actual business message format
    /// </summary>
    private readonly Dictionary<string, Regex> _messageTypePatterns = new()
    {
        { "订单消息", new Regex(@"""type"":\s*""order""", RegexOptions.IgnoreCase) },
        { "用户消息", new Regex(@"""type"":\s*""user""", RegexOptions.IgnoreCase) },
        { "支付消息", new Regex(@"""type"":\s*""payment""", RegexOptions.IgnoreCase) },
        { "库存消息", new Regex(@"""type"":\s*""inventory""", RegexOptions.IgnoreCase) },
        { "通知消息", new Regex(@"""type"":\s*""notification""", RegexOptions.IgnoreCase) }
    };

    /// <summary>
    /// Size limit (bytes)
    /// </summary>
    private const int LARGE_MESSAGE_THRESHOLD = 1024 * 10; // 10KB

    /// <summary>
    /// Constructor
    /// </summary>
    public BusinessMessageAnalyzerMiddleware()
    {
        Initialize();
    }

    /// <summary>
    /// Synchronize data context
    /// </summary>
    /// <param name="context">data context</param>
    /// <returns>Processed data context</returns>
    public override ChannelDataContext Pass(ChannelDataContext context)
    {
        try
        {
            // basic statistics
            IncrementCounter("总消息数");
            SetInfo("最后处理时间", DateTime.Now);

            // Analyze message content
            AnalyzeMessageContent(context);

            // Analyze message size
            AnalyzeMessageSize(context);

            // Analysis processing time
            AnalyzeProcessingTime();

            return context;
        }
        catch (Exception ex)
        {
            IncrementCounter("分析异常数");
            SetInfo("最后异常", ex.Message);
            SetInfo("最后异常时间", DateTime.Now);
            return context;
        }
    }

    /// <summary>
    /// Process data context asynchronously
    /// </summary>
    /// <param name="context">data context</param>
    /// <returns>Processed data context</returns>
    public override Task<ChannelDataContext> PassAsync(ChannelDataContext context)
    {
        return Task.FromResult(Pass(context));
    }

    /// <summary>
    /// Analyze message content
    /// </summary>
    /// <param name="context">data context</param>
    private void AnalyzeMessageContent(ChannelDataContext context)
    {
        if (context.Data == null) 
        {
            IncrementCounter("空消息数");
            return;
        }

        var messageContent = context.Data.ToString() ?? string.Empty;
        
        // Statistics message length
        var messageLength = messageContent.Length;
        SetInfo("平均消息长度", CalculateAverage("总字符数", messageLength, "总消息数"));

        // Identify message type
        var messageType = IdentifyMessageType(messageContent);
        if (!string.IsNullOrEmpty(messageType))
        {
            IncrementCounter($"消息类型-{messageType}");
            SetInfo("最后识别类型", messageType);
        }
        else
        {
            IncrementCounter("未知类型消息");
        }

        // Statistics JSON messages
        if (IsJsonMessage(messageContent))
        {
            IncrementCounter("JSON消息数");
            AnalyzeJsonMessage(messageContent);
        }
        else
        {
            IncrementCounter("非JSON消息数");
        }
    }

    /// <summary>
    /// Analyze message size
    /// </summary>
    /// <param name="context">data context</param>
    private void AnalyzeMessageSize(ChannelDataContext context)
    {
        if (context.Data == null) return;

        var messageSize = System.Text.Encoding.UTF8.GetByteCount(context.Data.ToString() ?? string.Empty);
        
        // Update statistics
        IncrementCounter("总字节数", messageSize);
        SetInfo("平均消息大小(字节)", CalculateAverage("总字节数", messageSize, "总消息数"));

        // big news statistics
        if (messageSize > LARGE_MESSAGE_THRESHOLD)
        {
            IncrementCounter("大消息数");
            SetInfo("最大消息大小", Math.Max(GetInfo<long>("最大消息大小"), messageSize));
        }

        // Small news statistics
        if (messageSize < 100)
        {
            IncrementCounter("小消息数");
        }
    }

    /// <summary>
    /// Analysis processing time
    /// </summary>
    private void AnalyzeProcessingTime()
    {
        var now = DateTime.Now;
        var lastTime = GetInfo<DateTime>("上次处理时间", now);
        
        if (lastTime != default && lastTime < now)
        {
            var interval = (now - lastTime).TotalMilliseconds;
            SetInfo("平均处理间隔(毫秒)", CalculateAverage("总间隔时间", interval, "总消息数"));
            SetInfo("最后处理间隔(毫秒)", interval);
        }
        
        SetInfo("上次处理时间", now);
    }

    /// <summary>
    /// Identify message type
    /// </summary>
    /// <param name="messageContent">Message content</param>
    /// <returns>Message type name</returns>
    private string? IdentifyMessageType(string messageContent)
    {
        foreach (var pattern in _messageTypePatterns)
        {
            if (pattern.Value.IsMatch(messageContent))
            {
                return pattern.Key;
            }
        }
        return null;
    }

    /// <summary>
    /// Check if it is a JSON message
    /// </summary>
    /// <param name="messageContent">Message content</param>
    /// <returns>Is it JSON?</returns>
    private bool IsJsonMessage(string messageContent)
    {
        try
        {
            JsonDocument.Parse(messageContent);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Analyze JSON messages
    /// </summary>
    /// <param name="messageContent">JSON message content</param>
    private void AnalyzeJsonMessage(string messageContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageContent);
            var root = doc.RootElement;

            // Statistical JSON hierarchy depth
            var depth = GetJsonDepth(root);
            SetInfo("最大JSON深度", Math.Max(GetInfo<int>("最大JSON深度"), depth));

            // Count the number of JSON fields
            if (root.ValueKind == JsonValueKind.Object)
            {
                var fieldCount = CountJsonFields(root);
                SetInfo("平均JSON字段数", CalculateAverage("总JSON字段数", fieldCount, "JSON消息数"));
            }
        }
        catch
        {
            IncrementCounter("JSON解析失败数");
        }
    }

    /// <summary>
    /// Get JSON depth
    /// </summary>
    /// <param name="element">JSON element</param>
    /// <returns>depth</returns>
    private int GetJsonDepth(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .Select(property => GetJsonDepth(property.Value))
                .DefaultIfEmpty(0)
                .Max() + 1,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(GetJsonDepth)
                .DefaultIfEmpty(0)
                .Max() + 1,
            _ => 1
        };
    }

    /// <summary>
    /// Count the number of JSON fields
    /// </summary>
    /// <param name="element">JSON element</param>
    /// <returns>Number of fields</returns>
    private int CountJsonFields(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Sum(p => 1 + CountJsonFields(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Sum(CountJsonFields),
            _ => 0
        };
    }

    /// <summary>
    /// Calculate average
    /// </summary>
    /// <param name="totalKey">total key</param>
    /// <param name="newValue">new value</param>
    /// <param name="countKey">Count key</param>
    /// <returns>average value</returns>
    private double CalculateAverage(string totalKey, double newValue, string countKey)
    {
        var total = GetInfo<double>(totalKey) + newValue;
        var count = GetInfo<long>(countKey);
        SetInfo(totalKey, total);
        return count > 0 ? total / count : 0;
    }

    /// <summary>
    /// Initialize middleware
    /// </summary>
    private void Initialize()
    {
        SetInfo("启动时间", DateTime.Now);
        SetInfo("中间件版本", "1.0.0");
        SetInfo("功能描述", "业务消息分析统计中间件");
        SetInfo("支持消息类型", string.Join(", ", _messageTypePatterns.Keys));
        SetInfo("大消息阈值(字节)", LARGE_MESSAGE_THRESHOLD);
    }

    /// <summary>
    /// Reset all statistics
    /// </summary>
    public void ResetStatistics()
    {
        ClearInfo();
        Initialize();
    }

    /// <summary>
    /// Get processing rate statistics
    /// </summary>
    /// <returns>Processing rate information</returns>
    public Dictionary<string, object> GetProcessingStats()
    {
        var stats = new Dictionary<string, object>();
        var totalMessages = GetInfo<long>("总消息数");
        var startTime = GetInfo<DateTime>("启动时间", DateTime.Now);
        var timeSpan = DateTime.Now - startTime;

        if (timeSpan.TotalMinutes > 0.1)
        {
            stats["每分钟处理数"] = totalMessages / timeSpan.TotalMinutes;
            stats["每小时处理数"] = totalMessages / timeSpan.TotalHours;
            stats["运行时长(分钟)"] = timeSpan.TotalMinutes;
        }

        return stats;
    }
}

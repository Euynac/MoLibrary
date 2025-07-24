using MoLibrary.DataChannel.Pipeline;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MoLibrary.DataChannel.BuildInMiddlewares;

/// <summary>
/// 业务消息分析中间件示例
/// 继承自信息展示中间件基类，用于分析和统计业务消息类型
/// 开发者可以根据自己的业务需求修改此示例
/// </summary>
public class BusinessMessageAnalyzerMiddleware : PipeInfoDisplayMiddlewareBase
{
    /// <summary>
    /// 消息类型正则表达式
    /// 可根据实际业务消息格式修改
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
    /// 大小限制（字节）
    /// </summary>
    private const int LARGE_MESSAGE_THRESHOLD = 1024 * 10; // 10KB

    /// <summary>
    /// 构造函数
    /// </summary>
    public BusinessMessageAnalyzerMiddleware()
    {
        Initialize();
    }

    /// <summary>
    /// 同步处理数据上下文
    /// </summary>
    /// <param name="context">数据上下文</param>
    /// <returns>处理后的数据上下文</returns>
    public override DataContext Pass(DataContext context)
    {
        try
        {
            // 基础统计
            IncrementCounter("总消息数");
            SetInfo("最后处理时间", DateTime.Now);

            // 分析消息内容
            AnalyzeMessageContent(context);

            // 分析消息大小
            AnalyzeMessageSize(context);

            // 分析处理时间
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
    /// 异步处理数据上下文
    /// </summary>
    /// <param name="context">数据上下文</param>
    /// <returns>处理后的数据上下文</returns>
    public override async Task<DataContext> PassAsync(DataContext context)
    {
        return await Task.FromResult(Pass(context));
    }

    /// <summary>
    /// 分析消息内容
    /// </summary>
    /// <param name="context">数据上下文</param>
    private void AnalyzeMessageContent(DataContext context)
    {
        if (context.Data == null) 
        {
            IncrementCounter("空消息数");
            return;
        }

        var messageContent = context.Data.ToString() ?? string.Empty;
        
        // 统计消息长度
        var messageLength = messageContent.Length;
        SetInfo("平均消息长度", CalculateAverage("总字符数", messageLength, "总消息数"));

        // 识别消息类型
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

        // 统计JSON消息
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
    /// 分析消息大小
    /// </summary>
    /// <param name="context">数据上下文</param>
    private void AnalyzeMessageSize(DataContext context)
    {
        if (context.Data == null) return;

        var messageSize = System.Text.Encoding.UTF8.GetByteCount(context.Data.ToString() ?? string.Empty);
        
        // 更新统计信息
        IncrementCounter("总字节数", messageSize);
        SetInfo("平均消息大小(字节)", CalculateAverage("总字节数", messageSize, "总消息数"));

        // 大消息统计
        if (messageSize > LARGE_MESSAGE_THRESHOLD)
        {
            IncrementCounter("大消息数");
            SetInfo("最大消息大小", Math.Max(GetInfo<long>("最大消息大小"), messageSize));
        }

        // 小消息统计
        if (messageSize < 100)
        {
            IncrementCounter("小消息数");
        }
    }

    /// <summary>
    /// 分析处理时间
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
    /// 识别消息类型
    /// </summary>
    /// <param name="messageContent">消息内容</param>
    /// <returns>消息类型名称</returns>
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
    /// 检查是否为JSON消息
    /// </summary>
    /// <param name="messageContent">消息内容</param>
    /// <returns>是否为JSON</returns>
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
    /// 分析JSON消息
    /// </summary>
    /// <param name="messageContent">JSON消息内容</param>
    private void AnalyzeJsonMessage(string messageContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageContent);
            var root = doc.RootElement;

            // 统计JSON层级深度
            var depth = GetJsonDepth(root);
            SetInfo("最大JSON深度", Math.Max(GetInfo<int>("最大JSON深度"), depth));

            // 统计JSON字段数量
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
    /// 获取JSON深度
    /// </summary>
    /// <param name="element">JSON元素</param>
    /// <returns>深度</returns>
    private int GetJsonDepth(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Max(p => GetJsonDepth(p.Value)) + 1,
            JsonValueKind.Array => element.EnumerateArray().Max(GetJsonDepth) + 1,
            _ => 1
        };
    }

    /// <summary>
    /// 计算JSON字段数量
    /// </summary>
    /// <param name="element">JSON元素</param>
    /// <returns>字段数量</returns>
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
    /// 计算平均值
    /// </summary>
    /// <param name="totalKey">总数键</param>
    /// <param name="newValue">新值</param>
    /// <param name="countKey">计数键</param>
    /// <returns>平均值</returns>
    private double CalculateAverage(string totalKey, double newValue, string countKey)
    {
        var total = GetInfo<double>(totalKey) + newValue;
        var count = GetInfo<long>(countKey);
        SetInfo(totalKey, total);
        return count > 0 ? total / count : 0;
    }

    /// <summary>
    /// 初始化中间件
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
    /// 重置所有统计信息
    /// </summary>
    public void ResetStatistics()
    {
        ClearInfo();
        Initialize();
    }

    /// <summary>
    /// 获取处理速率统计
    /// </summary>
    /// <returns>处理速率信息</returns>
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
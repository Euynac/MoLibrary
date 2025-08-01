using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using MoLibrary.Core.Extensions;
using MoLibrary.DataChannel.Pipeline;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.DataChannel.BuildInMiddlewares;

/// <summary>
/// 消息调试中间件
/// 用于监听和调试通过管道传输的消息内容
/// </summary>
public class MessageDebuggerMiddleware : PipeInfoDisplayMiddlewareBase
{
    /// <summary>
    /// 调试消息记录
    /// </summary>
    public class DebugMessage
    {
        public DateTime Timestamp { get; set; }
        public EDataSource Source { get; set; }
        public string FormattedContent { get; set; } = string.Empty;
        public string MessageType { get; set; } = string.Empty;
        public object? RawData { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// 是否激活调试
    /// </summary>
    private bool _isActive;
    
    /// <summary>
    /// 监听的关键字
    /// </summary>
    private string _filterKeyword = string.Empty;
    
    /// <summary>
    /// 队列最大长度
    /// </summary>
    private int _maxQueueSize = 100;
    
    /// <summary>
    /// 调试消息队列
    /// </summary>
    private readonly ConcurrentQueue<DebugMessage> _debugMessages = new();
    
    /// <summary>
    /// 锁对象，用于同步队列操作
    /// </summary>
    private readonly object _queueLock = new();

    /// <summary>
    /// 获取或设置是否激活调试
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            SetInfo("调试状态", value ? "已激活" : "未激活");
            SetInfo("状态更新时间", DateTime.Now);
        }
    }

    /// <summary>
    /// 获取或设置过滤关键字
    /// </summary>
    public string FilterKeyword
    {
        get => _filterKeyword;
        set
        {
            _filterKeyword = value ?? string.Empty;
            SetInfo("当前过滤关键字", _filterKeyword);
        }
    }

    /// <summary>
    /// 获取或设置队列最大长度
    /// </summary>
    public int MaxQueueSize
    {
        get => _maxQueueSize;
        set
        {
            _maxQueueSize = Math.Max(1, value);
            SetInfo("队列最大长度", _maxQueueSize);
            TrimQueue();
        }
    }

    /// <summary>
    /// 获取调试消息列表
    /// </summary>
    public List<DebugMessage> GetDebugMessages()
    {
        lock (_queueLock)
        {
            return _debugMessages.ToList();
        }
    }

    /// <summary>
    /// 清空调试消息
    /// </summary>
    public void ClearDebugMessages()
    {
        lock (_queueLock)
        {
            while (_debugMessages.TryDequeue(out _)) { }
            SetInfo("消息清空时间", DateTime.Now);
            SetInfo("已捕获消息数", 0);
        }
    }

    /// <summary>
    /// 同步处理数据上下文
    /// </summary>
    public override DataContext Pass(DataContext context)
    {
        if (!IsActive)
        {
            return context;
        }

        try
        {
            var formattedContent = FormatMessage(context.Data);
            
            if (ShouldCapture(formattedContent))
            {
                var debugMessage = new DebugMessage
                {
                    Timestamp = DateTime.Now,
                    Source = context.Source,
                    FormattedContent = formattedContent,
                    MessageType = context.Data?.GetType().GetCleanFullName() ?? "null",
                    RawData = context.Data,
                    Metadata = context.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? "")
                };

                AddDebugMessage(debugMessage);
            }
        }
        catch (Exception ex)
        {
            CollectException(ex, this, "消息调试处理失败");
        }

        return context;
    }

    /// <summary>
    /// 异步处理数据上下文
    /// </summary>
    public override async Task<DataContext> PassAsync(DataContext context)
    {
        return await Task.FromResult(Pass(context));
    }

    /// <summary>
    /// 格式化消息内容
    /// 可由子类重写以实现自定义格式化逻辑
    /// </summary>
    /// <param name="data">原始数据</param>
    /// <returns>格式化后的字符串</returns>
    protected virtual string FormatMessage(object? data)
    {
        if (data == null)
        {
            return "null";
        }

        try
        {
            return data.ToJsonStringForce()!;
        }
        catch(Exception e)
        {
            return $"{data.GetCleanFullName()}格式化出现异常：{e.GetMessageRecursively()}";
        }
    }

    /// <summary>
    /// 判断是否应该捕获消息
    /// 可由子类重写以实现自定义匹配逻辑
    /// </summary>
    /// <param name="formattedContent">格式化后的消息内容</param>
    /// <returns>是否应该捕获</returns>
    protected virtual bool ShouldCapture(string formattedContent)
    {
        if (string.IsNullOrWhiteSpace(FilterKeyword))
        {
            return true;
        }

        return formattedContent.Contains(FilterKeyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 添加调试消息到队列
    /// </summary>
    private void AddDebugMessage(DebugMessage message)
    {
        lock (_queueLock)
        {
            _debugMessages.Enqueue(message);
            TrimQueue();
            
            var count = _debugMessages.Count;
            SetInfo("已捕获消息数", count);
            SetInfo("最后捕获时间", message.Timestamp);
        }
    }

    /// <summary>
    /// 修剪队列以保持在最大长度限制内
    /// </summary>
    private void TrimQueue()
    {
        while (_debugMessages.Count > _maxQueueSize && _debugMessages.TryDequeue(out _))
        {
        }
    }

    /// <summary>
    /// 初始化中间件
    /// </summary>
    public void Initialize()
    {
        SetInfo("中间件名称", "消息调试器");
        SetInfo("调试状态", "未激活");
        SetInfo("队列最大长度", _maxQueueSize);
        SetInfo("当前过滤关键字", "");
        SetInfo("已捕获消息数", 0);
        SetInfo("初始化时间", DateTime.Now);
    }
}
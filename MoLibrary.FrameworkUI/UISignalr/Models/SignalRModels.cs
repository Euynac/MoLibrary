using MoLibrary.SignalR.Models;

namespace MoLibrary.FrameworkUI.UISignalr.Models
{
    /// <summary>
    /// Hub方法信息
    /// </summary>
    public class HubMethodInfo
    {
        /// <summary>
        /// 方法名称
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; } = "";
        
        /// <summary>
        /// 方法参数列表
        /// </summary>
        public List<SignalRMethodParameter> Args { get; set; } = [];
        
        /// <summary>
        /// 是否正在监听
        /// </summary>
        public bool IsListening { get; set; }
        
        /// <summary>
        /// 接收消息次数
        /// </summary>
        public int ReceivedCount { get; set; } = 0;
    }

    /// <summary>
    /// SignalR消息模型
    /// </summary>
    public class SignalRMessage
    {
        /// <summary>
        /// 消息来源
        /// </summary>
        public string Source { get; set; } = "";
        
        /// <summary>
        /// 消息内容
        /// </summary>
        public string Content { get; set; } = "";
        
        /// <summary>
        /// 消息详情
        /// </summary>
        public string Details { get; set; } = "";
        
        /// <summary>
        /// 消息类型
        /// </summary>
        public MessageType Type { get; set; }
        
        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// 是否为错误消息
        /// </summary>
        public bool IsError { get; set; }
    }

    /// <summary>
    /// 消息类型枚举
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// 已发送
        /// </summary>
        Sent,
        
        /// <summary>
        /// 已接收
        /// </summary>
        Received,
        
        /// <summary>
        /// 系统消息
        /// </summary>
        System,
        
        /// <summary>
        /// 成功消息
        /// </summary>
        Success,
        
        /// <summary>
        /// 错误消息
        /// </summary>
        Error,
        
        /// <summary>
        /// 信息消息
        /// </summary>
        Info
    }

    /// <summary>
    /// SignalR连接状态
    /// </summary>
    public class SignalRConnectionState
    {
        /// <summary>
        /// 连接状态
        /// </summary>
        public string Status { get; set; } = "未连接";
        
        /// <summary>
        /// 连接ID
        /// </summary>
        public string ConnectionId { get; set; } = "";
        
        /// <summary>
        /// 是否正在连接
        /// </summary>
        public bool IsConnecting { get; set; }
        
        /// <summary>
        /// 已接收消息总数
        /// </summary>
        public int TotalReceivedMessages { get; set; }
        
        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => Status == "Connected";
    }

    /// <summary>
    /// 方法调用参数
    /// </summary>
    public class MethodCallParameter
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// 参数值
        /// </summary>
        public string Value { get; set; } = "";
        
        /// <summary>
        /// 参数类型
        /// </summary>
        public string Type { get; set; } = "";
    }
} 
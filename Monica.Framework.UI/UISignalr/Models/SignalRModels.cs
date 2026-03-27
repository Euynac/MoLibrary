using Monica.SignalR.Models;

namespace Monica.Framework.UI.UISignalr.Models
{
    /// <summary>
    /// Hub method information
    /// </summary>
    public class HubMethodInfo
    {
        /// <summary>
        /// method name
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// display name
        /// </summary>
        public string DisplayName { get; set; } = "";
        
        /// <summary>
        /// Method parameter list
        /// </summary>
        public List<SignalRMethodParameter> Args { get; set; } = [];
        
        /// <summary>
        /// Is it listening?
        /// </summary>
        public bool IsListening { get; set; }
        
        /// <summary>
        /// Number of messages received
        /// </summary>
        public int ReceivedCount { get; set; } = 0;
    }

    /// <summary>
    /// SignalR message model
    /// </summary>
    public class SignalRMessage
    {
        /// <summary>
        /// Source
        /// </summary>
        public string Source { get; set; } = "";
        
        /// <summary>
        /// Message content
        /// </summary>
        public string Content { get; set; } = "";
        
        /// <summary>
        /// Message details
        /// </summary>
        public string Details { get; set; } = "";
        
        /// <summary>
        /// Message type
        /// </summary>
        public MessageType Type { get; set; }
        
        /// <summary>
        /// Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Is it an error message?
        /// </summary>
        public bool IsError { get; set; }
    }

    /// <summary>
    /// Message type enum
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Sent
        /// </summary>
        Sent,
        
        /// <summary>
        /// Received
        /// </summary>
        Received,
        
        /// <summary>
        /// System messages
        /// </summary>
        System,
        
        /// <summary>
        /// success message
        /// </summary>
        Success,
        
        /// <summary>
        /// error message
        /// </summary>
        Error,
        
        /// <summary>
        /// information message
        /// </summary>
        Info
    }

    /// <summary>
    /// SignalR connection status
    /// </summary>
    public class SignalRConnectionState
    {
        /// <summary>
        /// connection status
        /// </summary>
        public string Status { get; set; } = "未连接";
        
        /// <summary>
        /// Connection ID
        /// </summary>
        public string ConnectionId { get; set; } = "";
        
        /// <summary>
        /// Is connecting
        /// </summary>
        public bool IsConnecting { get; set; }
        
        /// <summary>
        /// Total number of messages received
        /// </summary>
        public int TotalReceivedMessages { get; set; }
        
        /// <summary>
        /// Is it connected?
        /// </summary>
        public bool IsConnected => Status == "Connected";
    }

    /// <summary>
    /// Method call parameters
    /// </summary>
    public class MethodCallParameter
    {
        /// <summary>
        /// Parameter name
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// Parameter value
        /// </summary>
        public string Value { get; set; } = "";
        
        /// <summary>
        /// Parameter type
        /// </summary>
        public string Type { get; set; } = "";
    }
} 
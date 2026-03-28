using System.Text.Json;
using Microsoft.JSInterop;
using Monica.Framework.UI.UISignalr.Models;
using Monica.SignalR.Services;
using Monica.SignalR.Models;
using Monica.Core.Results;

namespace Monica.Framework.UI.UISignalr.Services
{
    /// <summary>
    /// SignalR Debugging Service
    /// </summary>
    public class SignalRDebugService : IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly MoSignalRManageService _signalRService;
        private DotNetObjectReference<SignalRDebugService>? _dotNetRef;
        private bool _disposed = false;
        private readonly List<SignalRMessage> _messages = [];
        private readonly List<HubMethodInfo> _hubMethods = [];
        private readonly List<SignalRServerGroupInfo> _hubGroups = [];
        private readonly List<SignalRConnectedUserInfo> _connectedUsers = [];
        private readonly SignalRConnectionState _connectionState = new();

        /// <summary>
        /// Whether to enable detailed debugging logs
        /// </summary>
        public bool IsVerboseLoggingEnabled { get; set; } = false;

        /// <summary>
        /// Message receiving event
        /// </summary>
        public event Action<SignalRMessage>? MessageReceived;

        /// <summary>
        /// Connection status change event
        /// </summary>
        public event Action<SignalRConnectionState>? ConnectionStateChanged;

        /// <summary>
        /// Method to listen for state change events
        /// </summary>
        public event Action<HubMethodInfo>? MethodListenerChanged;

        /// <summary>
        /// Connected user list change event
        /// </summary>
        public event Action<IReadOnlyList<SignalRConnectedUserInfo>>? ConnectedUsersChanged;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="jsRuntime">JavaScript runtime</param>
        /// <param name="signalRService">SignalR business service</param>
        public SignalRDebugService(IJSRuntime jsRuntime, MoSignalRManageService signalRService)
        {
            _jsRuntime = jsRuntime;
            _signalRService = signalRService;
        }

        /// <summary>
        /// Initialize service
        /// </summary>
        public async Task InitializeAsync()
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            
            // Wait for JavaScript to finish loading before setting the callback
            await WaitForJavaScriptAsync();
            await SetupJavaScriptCallbacks();
        }

        /// <summary>
        /// Wait for JavaScript to finish loading
        /// </summary>
        private async Task WaitForJavaScriptAsync()
        {
            var maxRetries = 10;
            var retryDelay = 100;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Try calling the signalRDebug object's method to check if it has been loaded
                    await _jsRuntime.InvokeAsync<object>("signalRDebug.getHubsData");
                    return; // 成功，退出循环
                }
                catch
                {
                    // JavaScript has not been loaded yet, please wait a while and try again
                    await Task.Delay(retryDelay);
                    retryDelay = Math.Min(retryDelay * 2, 1000); // 指数退避，最大1秒
                }
            }
            
            AddMessage("系统", "JavaScript加载超时，某些功能可能无法正常工作", MessageType.Error);
        }

        /// <summary>
        /// Set JavaScript callback
        /// </summary>
        private async Task SetupJavaScriptCallbacks()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("signalRDebug.setMessageCallback", _dotNetRef);
                await _jsRuntime.InvokeVoidAsync("signalRDebug.setConnectionStatusCallback", _dotNetRef);
                await _jsRuntime.InvokeVoidAsync("signalRDebug.setConnectionIdCallback", _dotNetRef);
            }
            catch (Exception ex)
            {
                AddMessage("系统", $"设置JavaScript回调失败: {ex.Message}", MessageType.Error);
            }
        }

        /// <summary>
        /// Load Hub information
        /// </summary>
        /// <returns>Whether it was successful</returns>
        public async Task<bool> LoadHubsAsync()
        {
            try
            {
                var result = await _signalRService.GetHubInfosAsync();

                if (result.IsFailed(out var error, out var hubGroups))
                {
                    AddMessage("错误", $"加载Hub信息失败: {error}", MessageType.Error);
                    return false;
                }

                _hubMethods.Clear();
                _hubGroups.Clear();
                
                // Save original Hub group information
                _hubGroups.AddRange(hubGroups);
                
                foreach (var hubGroup in hubGroups)
                {
                    foreach (var method in hubGroup.Methods)
                    {
                        _hubMethods.Add(new HubMethodInfo
                        {
                            Name = method.Name,
                            DisplayName = $"{method.Desc} ({method.Name})",
                            Args = method.Args,
                            IsListening = false,
                            ReceivedCount = 0
                        });
                    }
                }

                AddMessage("系统", $"成功加载 {hubGroups.Count} 个Hub信息", MessageType.Success);
                return true;
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"加载Hub信息时发生异常: {ex.Message}", MessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// Load connected user information
        /// </summary>
        /// <returns>Whether it was successful</returns>
        public async Task<bool> LoadConnectedUsersAsync()
        {
            try
            {
                var result = await _signalRService.GetConnectedUsersAsync();

                if (result.IsFailed(out var error, out var users))
                {
                    AddMessage("错误", $"加载已连接用户失败: {error}", MessageType.Error);
                    return false;
                }

                _connectedUsers.Clear();
                _connectedUsers.AddRange(users);

                AddMessage("系统", $"成功加载 {users.Count} 个已连接用户", MessageType.Success);
                ConnectedUsersChanged?.Invoke(_connectedUsers.AsReadOnly());
                return true;
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"加载已连接用户时发生异常: {ex.Message}", MessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// Get the list of connected users
        /// </summary>
        /// <returns>List of connected users</returns>
        public IReadOnlyList<SignalRConnectedUserInfo> GetConnectedUsers() => _connectedUsers.AsReadOnly();

        /// <summary>
        /// Connect to SignalR Hub
        /// </summary>
        /// <param name="hubUrl">Hub URL</param>
        /// <param name="accessToken">Access Token</param>
        /// <returns>Whether it was successful</returns>
        public async Task<bool> ConnectAsync(string hubUrl, string accessToken)
        {
            try
            {
                _connectionState.IsConnecting = true;
                ConnectionStateChanged?.Invoke(_connectionState);

                var result = await _jsRuntime.InvokeAsync<JsonElement>("signalRDebug.connect", hubUrl, accessToken);

                if (result.GetProperty("success").GetBoolean())
                {
                    AddMessage("系统", $"成功连接到SignalR Hub: {hubUrl}", MessageType.Success);
                    return true;
                }
                else
                {
                    var error = result.GetProperty("error").GetString();
                    AddMessage("系统", $"连接失败: {error}", MessageType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddMessage("系统", $"连接失败: {ex.Message}", MessageType.Error);
                return false;
            }
            finally
            {
                _connectionState.IsConnecting = false;
                ConnectionStateChanged?.Invoke(_connectionState);
            }
        }

        /// <summary>
        /// Disconnect
        /// </summary>
        /// <returns>Whether it was successful</returns>
        public async Task<bool> DisconnectAsync()
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<JsonElement>("signalRDebug.disconnect");

                if (result.GetProperty("success").GetBoolean())
                {
                    AddMessage("系统", "SignalR连接已断开", MessageType.Info);
                    return true;
                }
                else
                {
                    var error = result.GetProperty("error").GetString();
                    AddMessage("系统", $"断开连接失败: {error}", MessageType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddMessage("系统", $"断开连接失败: {ex.Message}", MessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// Send message
        /// </summary>
        /// <param name="userName">Username</param>
        /// <param name="message">Message content</param>
        /// <returns>Whether it was successful</returns>
        public async Task<bool> SendMessageAsync(string userName, string message)
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<JsonElement>("signalRDebug.sendMessage", userName, message);

                if (result.GetProperty("success").GetBoolean())
                {
                    return true;
                }
                else
                {
                    var error = result.GetProperty("error").GetString();
                    AddMessage("错误", $"发送消息失败: {error}", MessageType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"发送消息失败: {ex.Message}", MessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// call method
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="parameters">Parameter list</param>
        /// <returns>Whether it was successful</returns>
        public async Task<bool> InvokeMethodAsync(string methodName, List<MethodCallParameter> parameters)
        {
            try
            {
                var args = new List<object>();
                var method = _hubMethods.FirstOrDefault(m => m.Name == methodName);

                if (method == null)
                {
                    AddMessage("错误", $"未找到方法: {methodName}", MessageType.Error);
                    return false;
                }

                // Detailed logging of the parameter conversion process (only when verbose logging is enabled)
                if (IsVerboseLoggingEnabled)
                {
                    AddMessage("系统", $"开始调用方法: {methodName}", MessageType.Info);
                }
                
                for (int i = 0; i < method.Args.Count; i++)
                {
                    var arg = method.Args[i];
                    var parameter = parameters.FirstOrDefault(p => p.Name == arg.Name);
                    var value = parameter?.Value ?? "";

                    if (IsVerboseLoggingEnabled)
                    {
                        AddMessage("系统", $"参数 {arg.Name} ({arg.Type}): '{value}'", MessageType.Info);
                    }

                    // Convert values ​​based on parameter type
                    var convertedValue = ConvertParameterValue(value, arg.Type);
                    args.Add(convertedValue ?? string.Empty);
                    
                    if (IsVerboseLoggingEnabled)
                    {
                        AddMessage("系统", $"转换后的值: {convertedValue?.GetType().Name ?? "null"} = {convertedValue}", MessageType.Info);
                    }
                }

                var result = await _jsRuntime.InvokeAsync<JsonElement>("signalRDebug.invokeMethod", methodName, args.ToArray());

                if (result.GetProperty("success").GetBoolean())
                {
                    return true;
                }

                var error = result.GetProperty("error").GetString();
                AddMessage("错误", $"调用方法失败: {error}", MessageType.Error);
                    
                // Add parameter information to help debugging
                AddMessage("调试", $"调用参数: {string.Join(", ", args.Select((arg, idx) => $"{method.Args[idx].Name}={arg}"))}", MessageType.Info);
                    
                return false;
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"调用方法失败: {ex.Message}", MessageType.Error);
                AddMessage("调试", $"异常详情: {ex}", MessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// Conversion parameter value
        /// </summary>
        /// <param name="value">String value</param>
        /// <param name="type">Target type</param>
        /// <returns>Converted value</returns>
        private object? ConvertParameterValue(string value, string type)
        {
            if (string.IsNullOrEmpty(value))
            {
                return GetDefaultValue(type);
            }

            try
            {
                var normalizedType = type.ToLower().Replace("system.", "");
                
                // Log conversion process (only when verbose logging is enabled)
                if (IsVerboseLoggingEnabled)
                {
                    AddMessage("调试", $"参数转换: '{value}' -> {type} (标准化: {normalizedType})", MessageType.Info);
                }
                
                var result = normalizedType switch
                {
                    // String type: Return the original value directly without any conversion
                    "string" => value,
                    
                    // Numeric type: Strictly follow type conversion
                    "int" or "int32" => int.Parse(value.Trim()),
                    "long" or "int64" => long.Parse(value.Trim()),
                    "double" => double.Parse(value.Trim()),
                    "float" or "single" => float.Parse(value.Trim()),
                    
                    // Boolean type: supports multiple formats
                    "bool" or "boolean" => ParseBooleanValue(value),
                    
                    // datetime type
                    "datetime" => DateTime.Parse(value.Trim()),
                    
                    // GUID type
                    "guid" => Guid.Parse(value.Trim()),
                    
                    // Handle full system type name
                    _ when normalizedType.StartsWith("system.") => ConvertSystemType(value, type),
                    
                    // Array and list types: Try JSON parsing
                    _ when normalizedType.Contains("[]") || normalizedType.Contains("list") || normalizedType.Contains("array") =>
                        TryParseAsJson(value, type),
                    
                    // Other complex types: try JSON parsing, and return the original string if it fails.
                    _ => TryParseComplexType(value, type)
                };
                
                if (IsVerboseLoggingEnabled)
                {
                    AddMessage("调试", $"转换结果: {result?.GetType().Name ?? "null"} = {result}", MessageType.Info);
                }
                return result;
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"参数转换失败: '{value}' -> {type}, 错误: {ex.Message}", MessageType.Error);
                
                // When the conversion fails, the original value is returned for the string type, and the default value is returned for other types.
                var normalizedType = type.ToLower().Replace("system.", "");
                if (normalizedType == "string")
                {
                    AddMessage("系统", $"转换失败，返回原字符串值: '{value}'", MessageType.Info);
                    return value;
                }
                
                return GetDefaultValue(type);
            }
        }

        /// <summary>
        /// parse boolean
        /// </summary>
        /// <param name="value">String value</param>
        /// <returns>Boolean value</returns>
        private bool ParseBooleanValue(string value)
        {
            var normalizedValue = value.ToLower().Trim();
            return normalizedValue switch
            {
                "true" or "1" or "yes" or "y" or "on" => true,
                "false" or "0" or "no" or "n" or "off" => false,
                _ => bool.Parse(value.Trim()) // 如果都不匹配，使用默认解析
            };
        }

        /// <summary>
        /// Convert system type
        /// </summary>
        /// <param name="value">String value</param>
        /// <param name="type">Type name</param>
        /// <returns>Converted value</returns>
        private object ConvertSystemType(string value, string type)
        {
            var typeName = type.Replace("System.", "").ToLower();
            return typeName switch
            {
                "string" => value, // 确保System.String也返回原字符串
                "int32" => int.Parse(value.Trim()),
                "int64" => long.Parse(value.Trim()),
                "double" => double.Parse(value.Trim()),
                "single" => float.Parse(value.Trim()),
                "boolean" => ParseBooleanValue(value),
                "datetime" => DateTime.Parse(value.Trim()),
                "guid" => Guid.Parse(value.Trim()),
                _ => value // 未知的系统类型，返回原字符串
            };
        }

        /// <summary>
        /// Try parsing to JSON
        /// </summary>
        /// <param name="value">String value</param>
        /// <param name="type">Type name</param>
        /// <returns>Analysis results</returns>
        private object TryParseAsJson(string value, string type)
        {
            try
            {
                var trimmedValue = value.Trim();
                if (trimmedValue.StartsWith("[") && trimmedValue.EndsWith("]"))
                {
                    return JsonSerializer.Deserialize<object[]>(trimmedValue) ?? new object[0];
                }
                else if (trimmedValue.StartsWith("{") && trimmedValue.EndsWith("}"))
                {
                    return JsonSerializer.Deserialize<object>(trimmedValue) ?? new object();
                }
                else
                {
                    // Not in JSON format, return the original string
                    return value;
                }
            }
            catch
            {
                // JSON parsing failed and original string returned
                return value;
            }
        }

        /// <summary>
        /// Try to parse complex types
        /// </summary>
        /// <param name="value">String value</param>
        /// <param name="type">Type name</param>
        /// <returns>Analysis results</returns>
        private object TryParseComplexType(string value, string type)
        {
            var trimmedValue = value.Trim();
            
            // If it looks like JSON, try parsing
            if ((trimmedValue.StartsWith("{") && trimmedValue.EndsWith("}")) ||
                (trimmedValue.StartsWith("[") && trimmedValue.EndsWith("]")))
            {
                try
                {
                    return JsonSerializer.Deserialize<object>(trimmedValue) ?? value;
                }
                catch
                {
                    // JSON parsing failed and original string returned
                    return value;
                }
            }
            
            // Unlike JSON, the original string is returned directly
            return value;
        }

        /// <summary>
        /// Get default value
        /// </summary>
        /// <param name="type">Type name</param>
        /// <returns>Default value</returns>
        private object GetDefaultValue(string type)
        {
            return type.ToLower() switch
            {
                "string" => "",
                "int" or "int32" => 0,
                "long" or "int64" => 0L,
                "double" => 0.0,
                "float" or "single" => 0.0f,
                "bool" or "boolean" => false,
                "datetime" => DateTime.Now,
                "guid" => Guid.Empty,
                _ => ""
            };
        }

        /// <summary>
        /// Switch method listening
        /// </summary>
        /// <param name="methodName">Method name</param>
        /// <param name="isListening">Whether to listen</param>
        /// <returns>Whether it was successful</returns>
        public async Task<bool> ToggleMethodListenerAsync(string methodName, bool isListening)
        {
            try
            {
                var method = _hubMethods.FirstOrDefault(m => m.Name == methodName);
                if (method == null) return false;

                JsonElement result;
                if (isListening)
                {
                    result = await _jsRuntime.InvokeAsync<JsonElement>("signalRDebug.registerListener", method.Name, method.DisplayName);
                }
                else
                {
                    result = await _jsRuntime.InvokeAsync<JsonElement>("signalRDebug.unregisterListener", method.Name, method.DisplayName);
                }

                if (result.GetProperty("success").GetBoolean())
                {
                    method.IsListening = isListening;
                    MethodListenerChanged?.Invoke(method);
                    return true;
                }
                else
                {
                    var error = result.GetProperty("error").GetString();
                    AddMessage("错误", $"{(isListening ? "注册" : "取消")}监听器失败: {error}", MessageType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"切换监听器失败: {ex.Message}", MessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// Enable all listeners
        /// </summary>
        /// <returns>The number of successful activations</returns>
        public async Task<int> EnableAllListenersAsync()
        {
            int successCount = 0;
            foreach (var method in _hubMethods)
            {
                if (!method.IsListening)
                {
                    if (await ToggleMethodListenerAsync(method.Name, true))
                    {
                        successCount++;
                    }
                }
            }
            return successCount;
        }

        /// <summary>
        /// Disable all listeners
        /// </summary>
        /// <returns>The number of successful bans</returns>
        public async Task<int> DisableAllListenersAsync()
        {
            int successCount = 0;
            foreach (var method in _hubMethods)
            {
                if (method.IsListening)
                {
                    if (await ToggleMethodListenerAsync(method.Name, false))
                    {
                        successCount++;
                    }
                }
            }
            return successCount;
        }

        /// <summary>
        /// Clear messages
        /// </summary>
        public void ClearMessages()
        {
            _messages.Clear();
            _connectionState.TotalReceivedMessages = 0;
            ConnectionStateChanged?.Invoke(_connectionState);
        }

        /// <summary>
        /// Get message list
        /// </summary>
        /// <returns>Message list</returns>
        public IReadOnlyList<SignalRMessage> GetMessages() => _messages.AsReadOnly();

        /// <summary>
        /// Get Hub method list
        /// </summary>
        /// <returns>Hub method list</returns>
        public IReadOnlyList<HubMethodInfo> GetHubMethods() => _hubMethods.AsReadOnly();
        
        /// <summary>
        /// Get the Hub group information list
        /// </summary>
        /// <returns>Hub group information list</returns>
        public IReadOnlyList<SignalRServerGroupInfo> GetHubGroups() => _hubGroups.AsReadOnly();

        /// <summary>
        /// Get connection status
        /// </summary>
        /// <returns>Connection status</returns>
        public SignalRConnectionState GetConnectionState() => _connectionState;

        /// <summary>
        /// JavaScript callback: receive message
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="content">Message content</param>
        /// <param name="type">Message type</param>
        [JSInvokable("Invoke")]
        public void Invoke(string source, string content, string type)
        {
            if (_disposed) return;
            
            var messageType = type switch
            {
                "Sent" => MessageType.Sent,
                "Received" => MessageType.Received,
                "System" => MessageType.System,
                "Success" => MessageType.Success,
                "Error" => MessageType.Error,
                "Info" => MessageType.Info,
                _ => MessageType.Info
            };

            if (messageType == MessageType.Received)
            {
                _connectionState.TotalReceivedMessages++;
                
                // Update method reception times
                var methodName = content.Split(':')[0];
                var method = _hubMethods.FirstOrDefault(m => m.DisplayName.Contains(methodName));
                if (method != null)
                {
                    method.ReceivedCount++;
                    MethodListenerChanged?.Invoke(method);
                }
            }

            AddMessage(source, content, messageType);
        }

        /// <summary>
        /// JavaScript callback: connection status change
        /// </summary>
        /// <param name="status">Connection status</param>
        [JSInvokable("OnConnectionStatusChanged")]
        public void OnConnectionStatusChanged(string status)
        {
            if (_disposed) return;
            
            AddMessage("系统", $"连接状态变化: {status}", MessageType.System);
            _connectionState.Status = status;
            ConnectionStateChanged?.Invoke(_connectionState);
        }

        /// <summary>
        /// JavaScript callback: connection ID changes
        /// </summary>
        /// <param name="id">Connection ID</param>
        [JSInvokable("SetConnectionId")]
        public void SetConnectionId(string id)
        {
            if (_disposed) return;
            
            _connectionState.ConnectionId = id;
            ConnectionStateChanged?.Invoke(_connectionState);
        }

        /// <summary>
        /// Add message
        /// </summary>
        /// <param name="source">Source</param>
        /// <param name="content">Message content</param>
        /// <param name="type">Message type</param>
        private void AddMessage(string source, string content, MessageType type)
        {
            var message = new SignalRMessage
            {
                Source = source,
                Content = content,
                Type = type,
                Timestamp = DateTime.Now,
                IsError = type == MessageType.Error
            };

            _messages.Insert(0, message);
            
            // Limit the number of messages
            if (_messages.Count > 1000)
            {
                _messages.RemoveAt(_messages.Count - 1);
            }

            MessageReceived?.Invoke(message);
        }

        /// <summary>
        /// Release resources
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                await _jsRuntime.InvokeVoidAsync("signalRDebug.disconnect");
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            _dotNetRef?.Dispose();
        }
    }
} 
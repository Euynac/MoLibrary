using Microsoft.JSInterop;
using MoLibrary.FrameworkUI.Models;
using MoLibrary.SignalR.Models;
using System.Text.Json;

namespace MoLibrary.FrameworkUI.Services
{
    /// <summary>
    /// SignalR调试服务
    /// </summary>
    public class SignalRDebugService : IAsyncDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private DotNetObjectReference<SignalRDebugService>? _dotNetRef;
        private readonly List<SignalRMessage> _messages = [];
        private readonly List<HubMethodInfo> _hubMethods = [];
        private readonly List<SignalRServerGroupInfo> _hubGroups = [];
        private readonly SignalRConnectionState _connectionState = new();

        /// <summary>
        /// 消息接收事件
        /// </summary>
        public event Action<SignalRMessage>? MessageReceived;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event Action<SignalRConnectionState>? ConnectionStateChanged;

        /// <summary>
        /// 方法监听状态变化事件
        /// </summary>
        public event Action<HubMethodInfo>? MethodListenerChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="jsRuntime">JavaScript运行时</param>
        public SignalRDebugService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        public async Task InitializeAsync()
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await SetupJavaScriptCallbacks();
        }

        /// <summary>
        /// 设置JavaScript回调
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
        /// 加载Hub信息
        /// </summary>
        /// <param name="apiUrl">API URL</param>
        /// <param name="jsonOptions">JSON选项</param>
        /// <returns>是否成功</returns>
        public async Task<bool> LoadHubsAsync(string apiUrl, JsonSerializerOptions jsonOptions)
        {
            try
            {
                var result = await _jsRuntime.InvokeAsync<JsonElement>("signalRDebug.loadHubs", apiUrl);

                if (result.GetProperty("success").GetBoolean())
                {
                    var dataProperty = result.GetProperty("data");
                    var hubGroups = JsonSerializer.Deserialize<List<SignalRServerGroupInfo>>(dataProperty.GetRawText(), jsonOptions);

                    _hubMethods.Clear();
                    _hubGroups.Clear();
                    
                    if (hubGroups != null)
                    {
                        // 保存原始的Hub组信息
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
                    }

                    AddMessage("系统", $"成功加载 {hubGroups?.Count ?? 0} 个Hub信息", MessageType.Success);
                    return true;
                }
                else
                {
                    var error = result.GetProperty("error").GetString();
                    AddMessage("错误", $"加载Hub信息失败: {error}", MessageType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"加载Hub信息时发生异常: {ex.Message}", MessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// 连接到SignalR Hub
        /// </summary>
        /// <param name="hubUrl">Hub URL</param>
        /// <param name="accessToken">访问令牌</param>
        /// <returns>是否成功</returns>
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
        /// 断开连接
        /// </summary>
        /// <returns>是否成功</returns>
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
        /// 发送消息
        /// </summary>
        /// <param name="userName">用户名</param>
        /// <param name="message">消息内容</param>
        /// <returns>是否成功</returns>
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
        /// 调用方法
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="parameters">参数列表</param>
        /// <returns>是否成功</returns>
        public async Task<bool> InvokeMethodAsync(string methodName, List<MethodCallParameter> parameters)
        {
            try
            {
                var args = new List<object>();
                var method = _hubMethods.FirstOrDefault(m => m.Name == methodName);

                if (method != null)
                {
                    for (int i = 0; i < method.Args.Count; i++)
                    {
                        var arg = method.Args[i];
                        var parameter = parameters.FirstOrDefault(p => p.Name == arg.Name);
                        var value = parameter?.Value ?? "";

                        // 根据参数类型转换值
                        var convertedValue = ConvertParameterValue(value, arg.Type);
                        args.Add(convertedValue);
                    }
                }

                var result = await _jsRuntime.InvokeAsync<JsonElement>("signalRDebug.invokeMethod", methodName, args.ToArray());

                if (result.GetProperty("success").GetBoolean())
                {
                    AddMessage("系统", $"方法 {methodName} 调用成功", MessageType.Success);
                    return true;
                }
                else
                {
                    var error = result.GetProperty("error").GetString();
                    AddMessage("错误", $"调用方法失败: {error}", MessageType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"调用方法失败: {ex.Message}", MessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// 转换参数值
        /// </summary>
        /// <param name="value">字符串值</param>
        /// <param name="type">目标类型</param>
        /// <returns>转换后的值</returns>
        private object ConvertParameterValue(string value, string type)
        {
            if (string.IsNullOrEmpty(value))
            {
                return GetDefaultValue(type);
            }

            try
            {
                return type.ToLower() switch
                {
                    "string" => value,
                    "int" or "int32" => int.Parse(value),
                    "long" or "int64" => long.Parse(value),
                    "double" => double.Parse(value),
                    "float" or "single" => float.Parse(value),
                    "bool" or "boolean" => bool.Parse(value),
                    "datetime" => DateTime.Parse(value),
                    "guid" => Guid.Parse(value),
                    _ when type.StartsWith("system.") => ConvertSystemType(value, type),
                    _ => TryParseAsJson(value, type)
                };
            }
            catch
            {
                return GetDefaultValue(type);
            }
        }

        /// <summary>
        /// 转换系统类型
        /// </summary>
        /// <param name="value">字符串值</param>
        /// <param name="type">类型名称</param>
        /// <returns>转换后的值</returns>
        private object ConvertSystemType(string value, string type)
        {
            var typeName = type.Replace("System.", "").ToLower();
            return typeName switch
            {
                "string" => value,
                "int32" => int.Parse(value),
                "int64" => long.Parse(value),
                "double" => double.Parse(value),
                "single" => float.Parse(value),
                "boolean" => bool.Parse(value),
                "datetime" => DateTime.Parse(value),
                "guid" => Guid.Parse(value),
                _ => value
            };
        }

        /// <summary>
        /// 尝试解析为JSON
        /// </summary>
        /// <param name="value">字符串值</param>
        /// <param name="type">类型名称</param>
        /// <returns>解析结果</returns>
        private object TryParseAsJson(string value, string type)
        {
            try
            {
                return JsonSerializer.Deserialize<object>(value) ?? value;
            }
            catch
            {
                return value;
            }
        }

        /// <summary>
        /// 获取默认值
        /// </summary>
        /// <param name="type">类型名称</param>
        /// <returns>默认值</returns>
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
        /// 切换方法监听
        /// </summary>
        /// <param name="methodName">方法名称</param>
        /// <param name="isListening">是否监听</param>
        /// <returns>是否成功</returns>
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
        /// 启用所有监听器
        /// </summary>
        /// <returns>成功启用的数量</returns>
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
        /// 禁用所有监听器
        /// </summary>
        /// <returns>成功禁用的数量</returns>
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
        /// 清空消息
        /// </summary>
        public void ClearMessages()
        {
            _messages.Clear();
            _connectionState.TotalReceivedMessages = 0;
            ConnectionStateChanged?.Invoke(_connectionState);
        }

        /// <summary>
        /// 获取消息列表
        /// </summary>
        /// <returns>消息列表</returns>
        public IReadOnlyList<SignalRMessage> GetMessages() => _messages.AsReadOnly();

        /// <summary>
        /// 获取Hub方法列表
        /// </summary>
        /// <returns>Hub方法列表</returns>
        public IReadOnlyList<HubMethodInfo> GetHubMethods() => _hubMethods.AsReadOnly();
        
        /// <summary>
        /// 获取Hub组信息列表
        /// </summary>
        /// <returns>Hub组信息列表</returns>
        public IReadOnlyList<SignalRServerGroupInfo> GetHubGroups() => _hubGroups.AsReadOnly();

        /// <summary>
        /// 获取连接状态
        /// </summary>
        /// <returns>连接状态</returns>
        public SignalRConnectionState GetConnectionState() => _connectionState;

        /// <summary>
        /// JavaScript回调：接收消息
        /// </summary>
        /// <param name="source">消息来源</param>
        /// <param name="content">消息内容</param>
        /// <param name="type">消息类型</param>
        [JSInvokable("Invoke")]
        public void Invoke(string source, string content, string type)
        {
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
                
                // 更新方法接收次数
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
        /// JavaScript回调：连接状态变化
        /// </summary>
        /// <param name="status">连接状态</param>
        [JSInvokable("OnConnectionStatusChanged")]
        public void OnConnectionStatusChanged(string status)
        {
            AddMessage("系统", $"连接状态变化: {status}", MessageType.System);
            _connectionState.Status = status;
            ConnectionStateChanged?.Invoke(_connectionState);
        }

        /// <summary>
        /// JavaScript回调：连接ID变化
        /// </summary>
        /// <param name="id">连接ID</param>
        [JSInvokable("SetConnectionId")]
        public void SetConnectionId(string id)
        {
            _connectionState.ConnectionId = id;
            ConnectionStateChanged?.Invoke(_connectionState);
        }

        /// <summary>
        /// 添加消息
        /// </summary>
        /// <param name="source">消息来源</param>
        /// <param name="content">消息内容</param>
        /// <param name="type">消息类型</param>
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
            
            // 限制消息数量
            if (_messages.Count > 1000)
            {
                _messages.RemoveAt(_messages.Count - 1);
            }

            MessageReceived?.Invoke(message);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("signalRDebug.disconnect");
            }
            catch
            {
                // 忽略清理错误
            }
            
            _dotNetRef?.Dispose();
        }
    }
} 
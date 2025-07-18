using System.Text.Json;
using Microsoft.JSInterop;
using MoLibrary.FrameworkUI.UISignalr.Models;
using MoLibrary.SignalR.Controllers;
using MoLibrary.SignalR.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UISignalr.Services
{
    /// <summary>
    /// SignalR调试服务
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
        private readonly SignalRConnectionState _connectionState = new();

        /// <summary>
        /// 是否启用详细调试日志
        /// </summary>
        public bool IsVerboseLoggingEnabled { get; set; } = false;

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
        /// <param name="signalRService">SignalR业务服务</param>
        public SignalRDebugService(IJSRuntime jsRuntime, MoSignalRManageService signalRService)
        {
            _jsRuntime = jsRuntime;
            _signalRService = signalRService;
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        public async Task InitializeAsync()
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            
            // 等待JavaScript加载完成后再设置回调
            await WaitForJavaScriptAsync();
            await SetupJavaScriptCallbacks();
        }

        /// <summary>
        /// 等待JavaScript加载完成
        /// </summary>
        private async Task WaitForJavaScriptAsync()
        {
            var maxRetries = 10;
            var retryDelay = 100;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // 尝试调用signalRDebug对象的方法来检查是否已加载
                    await _jsRuntime.InvokeAsync<object>("signalRDebug.getHubsData");
                    return; // 成功，退出循环
                }
                catch
                {
                    // JavaScript尚未加载完成，等待一会儿再试
                    await Task.Delay(retryDelay);
                    retryDelay = Math.Min(retryDelay * 2, 1000); // 指数退避，最大1秒
                }
            }
            
            AddMessage("系统", "JavaScript加载超时，某些功能可能无法正常工作", MessageType.Error);
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
        /// <returns>是否成功</returns>
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

                if (method == null)
                {
                    AddMessage("错误", $"未找到方法: {methodName}", MessageType.Error);
                    return false;
                }

                // 详细记录参数转换过程（仅在启用详细日志时）
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

                    // 根据参数类型转换值
                    var convertedValue = ConvertParameterValue(value, arg.Type);
                    args.Add(convertedValue);
                    
                    if (IsVerboseLoggingEnabled)
                    {
                        AddMessage("系统", $"转换后的值: {convertedValue?.GetType().Name ?? "null"} = {convertedValue}", MessageType.Info);
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
                    
                    // 添加参数信息帮助调试
                    AddMessage("调试", $"调用参数: {string.Join(", ", args.Select((arg, idx) => $"{method.Args[idx].Name}={arg}"))}", MessageType.Info);
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddMessage("错误", $"调用方法失败: {ex.Message}", MessageType.Error);
                AddMessage("调试", $"异常详情: {ex}", MessageType.Error);
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
                var normalizedType = type.ToLower().Replace("system.", "");
                
                // 记录转换过程（仅在启用详细日志时）
                if (IsVerboseLoggingEnabled)
                {
                    AddMessage("调试", $"参数转换: '{value}' -> {type} (标准化: {normalizedType})", MessageType.Info);
                }
                
                var result = normalizedType switch
                {
                    // 字符串类型：直接返回原值，不进行任何转换
                    "string" => value,
                    
                    // 数值类型：严格按照类型转换
                    "int" or "int32" => int.Parse(value.Trim()),
                    "long" or "int64" => long.Parse(value.Trim()),
                    "double" => double.Parse(value.Trim()),
                    "float" or "single" => float.Parse(value.Trim()),
                    
                    // 布尔类型：支持多种格式
                    "bool" or "boolean" => ParseBooleanValue(value),
                    
                    // 日期时间类型
                    "datetime" => DateTime.Parse(value.Trim()),
                    
                    // GUID类型
                    "guid" => Guid.Parse(value.Trim()),
                    
                    // 处理完整的系统类型名称
                    _ when normalizedType.StartsWith("system.") => ConvertSystemType(value, type),
                    
                    // 数组和列表类型：尝试JSON解析
                    _ when normalizedType.Contains("[]") || normalizedType.Contains("list") || normalizedType.Contains("array") =>
                        TryParseAsJson(value, type),
                    
                    // 其他复杂类型：尝试JSON解析，失败则返回原字符串
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
                
                // 转换失败时，对于string类型返回原值，其他类型返回默认值
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
        /// 解析布尔值
        /// </summary>
        /// <param name="value">字符串值</param>
        /// <returns>布尔值</returns>
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
        /// 尝试解析为JSON
        /// </summary>
        /// <param name="value">字符串值</param>
        /// <param name="type">类型名称</param>
        /// <returns>解析结果</returns>
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
                    // 不是JSON格式，返回原字符串
                    return value;
                }
            }
            catch
            {
                // JSON解析失败，返回原字符串
                return value;
            }
        }

        /// <summary>
        /// 尝试解析复杂类型
        /// </summary>
        /// <param name="value">字符串值</param>
        /// <param name="type">类型名称</param>
        /// <returns>解析结果</returns>
        private object TryParseComplexType(string value, string type)
        {
            var trimmedValue = value.Trim();
            
            // 如果看起来像JSON，尝试解析
            if ((trimmedValue.StartsWith("{") && trimmedValue.EndsWith("}")) ||
                (trimmedValue.StartsWith("[") && trimmedValue.EndsWith("]")))
            {
                try
                {
                    return JsonSerializer.Deserialize<object>(trimmedValue) ?? value;
                }
                catch
                {
                    // JSON解析失败，返回原字符串
                    return value;
                }
            }
            
            // 不像JSON，直接返回原字符串
            return value;
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
            if (_disposed) return;
            
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
            if (_disposed) return;
            
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
                // 忽略清理错误
            }
            
            _dotNetRef?.Dispose();
        }
    }
} 
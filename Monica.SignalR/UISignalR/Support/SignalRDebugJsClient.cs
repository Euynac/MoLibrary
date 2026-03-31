using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Monica.SignalR.Localization;
using Monica.SignalR.Models;
using Monica.SignalR.UISignalR.Models;

namespace Monica.SignalR.UISignalR.Support;

/// <summary>
/// Encapsulates browser-side SignalR debug interactions and JavaScript callbacks.
/// </summary>
public sealed class SignalRDebugJsClient(
    IJSRuntime jsRuntime,
    SignalRInvocationArgumentParser argumentParser,
    IStringLocalizer<SignalRResource> localizer)
    : IAsyncDisposable
{
    private const int MaxMessageCount = 1000;

    private readonly List<SignalRMessage> _messages = [];
    private readonly List<HubMethodInfo> _hubMethods = [];
    private readonly SignalRConnectionState _connectionState = new();
    private DotNetObjectReference<SignalRDebugJsClient>? _dotNetRef;
    private bool _disposed;

    /// <summary>
    /// Gets or sets a value indicating whether verbose argument conversion logs are emitted.
    /// </summary>
    public bool IsVerboseLoggingEnabled { get; set; }

    /// <summary>
    /// Raised when a new log message is received.
    /// </summary>
    public event Action<SignalRMessage>? MessageReceived;

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    public event Action<SignalRConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Raised when a hub method listener changes state.
    /// </summary>
    public event Action<HubMethodInfo>? MethodListenerChanged;

    /// <summary>
    /// Initializes JavaScript callbacks.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_dotNetRef is not null)
        {
            return;
        }

        _dotNetRef = DotNetObjectReference.Create(this);

        await WaitForJavaScriptAsync();
        await SetupJavaScriptLocalizationAsync();
        await SetupJavaScriptCallbacksAsync();
    }

    /// <summary>
    /// Rebuilds the client-side method catalog from server-side hub metadata.
    /// </summary>
    public void SetHubMetadata(IReadOnlyList<SignalRHubInfo> hubInfos)
    {
        _hubMethods.Clear();

        foreach (var hubInfo in hubInfos)
        {
            foreach (var methodInfo in hubInfo.Methods)
            {
                _hubMethods.Add(new HubMethodInfo
                {
                    Name = methodInfo.Name,
                    DisplayName = $"{methodInfo.Description} ({methodInfo.Name})",
                    Args = methodInfo.Parameters
                        .Select(parameterInfo => new SignalRHubParameterInfo
                        {
                            Name = parameterInfo.Name,
                            TypeName = parameterInfo.TypeName
                        })
                        .ToList()
                });
            }
        }
    }

    /// <summary>
    /// Connects to the selected SignalR hub.
    /// </summary>
    public async Task<bool> ConnectAsync(string hubUrl, string accessToken)
    {
        try
        {
            _connectionState.IsConnecting = true;
            RaiseConnectionStateChanged();

            var result = await jsRuntime.InvokeAsync<JsonElement>("signalRDebug.connect", hubUrl, accessToken);
            if (result.GetProperty("success").GetBoolean())
            {
                AddMessage("System", T("UISignalR:DebugClient:ConnectedToHub", hubUrl), MessageType.Success);
                return true;
            }

            AddMessage("System", T("UISignalR:DebugClient:ConnectionFailed", result.GetProperty("error").GetString() ?? string.Empty), MessageType.Error);
            return false;
        }
        catch (Exception ex)
        {
            AddMessage("System", T("UISignalR:DebugClient:ConnectionFailed", ex.Message), MessageType.Error);
            return false;
        }
        finally
        {
            _connectionState.IsConnecting = false;
            RaiseConnectionStateChanged();
        }
    }

    /// <summary>
    /// Disconnects the current browser-side SignalR connection.
    /// </summary>
    public async Task<bool> DisconnectAsync()
    {
        try
        {
            var result = await jsRuntime.InvokeAsync<JsonElement>("signalRDebug.disconnect");
            if (result.GetProperty("success").GetBoolean())
            {
                AddMessage("System", T("UISignalR:DebugClient:Disconnected"), MessageType.Info);
                return true;
            }

            AddMessage("System", T("UISignalR:DebugClient:DisconnectFailed", result.GetProperty("error").GetString() ?? string.Empty), MessageType.Error);
            return false;
        }
        catch (Exception ex)
        {
            AddMessage("System", T("UISignalR:DebugClient:DisconnectFailed", ex.Message), MessageType.Error);
            return false;
        }
    }

    /// <summary>
    /// Sends a quick message through the JavaScript debug client.
    /// </summary>
    public async Task<bool> SendMessageAsync(string userName, string message)
    {
        try
        {
            var result = await jsRuntime.InvokeAsync<JsonElement>("signalRDebug.sendMessage", userName, message);
            if (result.GetProperty("success").GetBoolean())
            {
                return true;
            }

            AddMessage("Error", T("UISignalR:DebugClient:SendFailed", result.GetProperty("error").GetString() ?? string.Empty), MessageType.Error);
            return false;
        }
        catch (Exception ex)
        {
            AddMessage("Error", T("UISignalR:DebugClient:SendFailed", ex.Message), MessageType.Error);
            return false;
        }
    }

    /// <summary>
    /// Invokes the selected hub method with typed arguments.
    /// </summary>
    public async Task<bool> InvokeMethodAsync(string methodName, IReadOnlyList<MethodCallParameter> parameters)
    {
        var method = _hubMethods.FirstOrDefault(candidate => candidate.Name == methodName);
        if (method is null)
        {
            AddMessage("Error", T("UISignalR:DebugClient:MethodNotFound", methodName), MessageType.Error);
            return false;
        }

        try
        {
            var arguments = new List<object?>(method.Args.Count);

            if (IsVerboseLoggingEnabled)
            {
                AddMessage("System", T("UISignalR:DebugClient:InvokingMethod", methodName), MessageType.Info);
            }

            foreach (var argumentInfo in method.Args)
            {
                var parameter = parameters.FirstOrDefault(candidate => candidate.Name == argumentInfo.Name);
                var rawValue = parameter?.Value ?? string.Empty;

                if (IsVerboseLoggingEnabled)
                {
                    AddMessage("System", T("UISignalR:DebugClient:ArgumentValue", argumentInfo.Name, argumentInfo.TypeName, rawValue), MessageType.Info);
                }

                var convertedValue = argumentParser.ConvertValue(rawValue, argumentInfo.TypeName);
                arguments.Add(convertedValue);

                if (IsVerboseLoggingEnabled)
                {
                    AddMessage(
                        "System",
                        T(
                            "UISignalR:DebugClient:ArgumentConverted",
                            argumentInfo.Name,
                            convertedValue?.GetType().Name ?? "null",
                            convertedValue?.ToString() ?? "null"),
                        MessageType.Info);
                }
            }

            var result = await jsRuntime.InvokeAsync<JsonElement>(
                "signalRDebug.invokeMethod",
                methodName,
                arguments.ToArray());

            if (result.GetProperty("success").GetBoolean())
            {
                return true;
            }

            AddMessage("Error", T("UISignalR:DebugClient:MethodInvocationFailed", result.GetProperty("error").GetString() ?? string.Empty), MessageType.Error);
            return false;
        }
        catch (Exception ex)
        {
            AddMessage("Error", T("UISignalR:DebugClient:MethodInvocationFailed", ex.Message), MessageType.Error);
            return false;
        }
    }

    /// <summary>
    /// Enables or disables a method listener.
    /// </summary>
    public async Task<bool> ToggleMethodListenerAsync(string methodName, bool isListening)
    {
        var method = _hubMethods.FirstOrDefault(candidate => candidate.Name == methodName);
        if (method is null)
        {
            return false;
        }

        try
        {
            var result = isListening
                ? await jsRuntime.InvokeAsync<JsonElement>("signalRDebug.registerListener", method.Name, method.DisplayName)
                : await jsRuntime.InvokeAsync<JsonElement>("signalRDebug.unregisterListener", method.Name, method.DisplayName);

            if (!result.GetProperty("success").GetBoolean())
            {
                AddMessage(
                    "Error",
                    isListening
                        ? T("UISignalR:DebugClient:RegisterListenerFailed", result.GetProperty("error").GetString() ?? string.Empty)
                        : T("UISignalR:DebugClient:UnregisterListenerFailed", result.GetProperty("error").GetString() ?? string.Empty),
                    MessageType.Error);
                return false;
            }

            method.IsListening = isListening;
            MethodListenerChanged?.Invoke(method);
            return true;
        }
        catch (Exception ex)
        {
            AddMessage("Error", T("UISignalR:DebugClient:ListenerToggleFailed", ex.Message), MessageType.Error);
            return false;
        }
    }

    /// <summary>
    /// Enables all available method listeners.
    /// </summary>
    public async Task<int> EnableAllListenersAsync()
    {
        var successCount = 0;

        foreach (var method in _hubMethods.Where(method => !method.IsListening))
        {
            if (await ToggleMethodListenerAsync(method.Name, true))
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <summary>
    /// Disables all active method listeners.
    /// </summary>
    public async Task<int> DisableAllListenersAsync()
    {
        var successCount = 0;

        foreach (var method in _hubMethods.Where(method => method.IsListening))
        {
            if (await ToggleMethodListenerAsync(method.Name, false))
            {
                successCount++;
            }
        }

        return successCount;
    }

    /// <summary>
    /// Clears the in-memory message log.
    /// </summary>
    public void ClearMessages()
    {
        _messages.Clear();
        _connectionState.TotalReceivedMessages = 0;
        RaiseConnectionStateChanged();
    }

    /// <summary>
    /// Returns a snapshot of the current message log.
    /// </summary>
    public IReadOnlyList<SignalRMessage> GetMessages() => _messages.AsReadOnly();

    /// <summary>
    /// Returns a snapshot of the current hub method catalog.
    /// </summary>
    public IReadOnlyList<HubMethodInfo> GetHubMethods() => _hubMethods.AsReadOnly();

    /// <summary>
    /// Returns a snapshot of the current connection state.
    /// </summary>
    public SignalRConnectionState GetConnectionState()
    {
        return new SignalRConnectionState
        {
            Status = _connectionState.Status,
            ConnectionId = _connectionState.ConnectionId,
            IsConnecting = _connectionState.IsConnecting,
            TotalReceivedMessages = _connectionState.TotalReceivedMessages
        };
    }

    /// <summary>
    /// Receives log messages from JavaScript callbacks.
    /// </summary>
    [JSInvokable("Invoke")]
    public void Invoke(string source, string content, string type)
    {
        if (_disposed)
        {
            return;
        }

        var messageType = type switch
        {
            "Sent" => MessageType.Sent,
            "Received" => MessageType.Received,
            "System" => MessageType.System,
            "Success" => MessageType.Success,
            "Error" => MessageType.Error,
            _ => MessageType.Info
        };

        if (messageType == MessageType.Received)
        {
            _connectionState.TotalReceivedMessages++;

            var methodDisplayName = content.Split(':', 2)[0];
            var method = _hubMethods.FirstOrDefault(candidate =>
                string.Equals(candidate.DisplayName, methodDisplayName, StringComparison.Ordinal));

            if (method is not null)
            {
                method.ReceivedCount++;
                MethodListenerChanged?.Invoke(method);
            }
        }

        AddMessage(source, content, messageType);
    }

    /// <summary>
    /// Receives connection status changes from JavaScript callbacks.
    /// </summary>
    [JSInvokable("OnConnectionStatusChanged")]
    public void OnConnectionStatusChanged(string status)
    {
        if (_disposed)
        {
            return;
        }

        _connectionState.Status = status;
        AddMessage("System", T("UISignalR:DebugClient:ConnectionStatusChanged", GetLocalizedStatus(status)), MessageType.System);
        RaiseConnectionStateChanged();
    }

    /// <summary>
    /// Receives connection identifier changes from JavaScript callbacks.
    /// </summary>
    [JSInvokable("SetConnectionId")]
    public void SetConnectionId(string connectionId)
    {
        if (_disposed)
        {
            return;
        }

        _connectionState.ConnectionId = connectionId;
        RaiseConnectionStateChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await jsRuntime.InvokeVoidAsync("signalRDebug.disconnect");
        }
        catch
        {
            // Ignore cleanup failures caused by browser refresh or circuit teardown.
        }

        _dotNetRef?.Dispose();
    }

    private async Task WaitForJavaScriptAsync()
    {
        var retryDelayMs = 100;

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                await jsRuntime.InvokeAsync<object>("signalRDebug.getHubsData");
                return;
            }
            catch
            {
                await Task.Delay(retryDelayMs);
                retryDelayMs = Math.Min(retryDelayMs * 2, 1000);
            }
        }

        AddMessage("System", T("UISignalR:DebugClient:JavaScriptLoadTimeout"), MessageType.Error);
    }

    private async Task SetupJavaScriptLocalizationAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("signalRDebug.setLocalization", new Dictionary<string, string>
            {
                ["NoArguments"] = T("UISignalR:JavaScript:NoArguments"),
                ["NotConnected"] = T("UISignalR:JavaScript:NotConnected"),
                ["ConnectionClosedWithError"] = T("UISignalR:JavaScript:ConnectionClosedWithError"),
                ["ConnectionClosed"] = T("UISignalR:JavaScript:ConnectionClosed"),
                ["Reconnecting"] = T("UISignalR:JavaScript:Reconnecting"),
                ["ReconnectedSuccessfully"] = T("UISignalR:JavaScript:ReconnectedSuccessfully"),
                ["ConnectedSuccessfully"] = T("UISignalR:JavaScript:ConnectedSuccessfully"),
                ["ConnectionFailed"] = T("UISignalR:JavaScript:ConnectionFailed"),
                ["Disconnected"] = T("UISignalR:JavaScript:Disconnected"),
                ["RegisteredListener"] = T("UISignalR:JavaScript:RegisteredListener"),
                ["UnregisteredListener"] = T("UISignalR:JavaScript:UnregisteredListener"),
                ["AllListenersCleared"] = T("UISignalR:JavaScript:AllListenersCleared")
            });
        }
        catch (Exception ex)
        {
            AddMessage("System", T("UISignalR:DebugClient:SetLocalizationFailed", ex.Message), MessageType.Error);
        }
    }

    private async Task SetupJavaScriptCallbacksAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("signalRDebug.setMessageCallback", _dotNetRef);
            await jsRuntime.InvokeVoidAsync("signalRDebug.setConnectionStatusCallback", _dotNetRef);
            await jsRuntime.InvokeVoidAsync("signalRDebug.setConnectionIdCallback", _dotNetRef);
        }
        catch (Exception ex)
        {
            AddMessage("System", T("UISignalR:DebugClient:SetCallbacksFailed", ex.Message), MessageType.Error);
        }
    }

    private void RaiseConnectionStateChanged()
    {
        ConnectionStateChanged?.Invoke(GetConnectionState());
    }

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
        if (_messages.Count > MaxMessageCount)
        {
            _messages.RemoveAt(_messages.Count - 1);
        }

        MessageReceived?.Invoke(message);
    }

    private string T(string key)
    {
        return localizer[key].Value;
    }

    private string T(string key, params object[] arguments)
    {
        return localizer[key, arguments].Value;
    }

    private string GetLocalizedStatus(string status)
    {
        return status switch
        {
            "Connected" => T("UISignalR:Common:Status:Connected"),
            "Connecting" => T("UISignalR:Common:Status:Connecting"),
            "Reconnecting" => T("UISignalR:Common:Status:Reconnecting"),
            "Disconnected" => T("UISignalR:Common:Status:Disconnected"),
            "Disconnecting" => T("UISignalR:Common:Status:Disconnecting"),
            _ => T("UISignalR:Common:Status:Unknown")
        };
    }
}

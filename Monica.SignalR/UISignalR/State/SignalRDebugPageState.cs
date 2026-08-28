using Microsoft.Extensions.Options;
using Monica.Core.Results;
using Monica.SignalR.Facades;
using Monica.SignalR.Models;
using Monica.SignalR.UISignalR.Models;
using Monica.SignalR.UISignalR.Support;
using Monica.Modules;

namespace Monica.SignalR.UISignalR.State;

/// <summary>
/// Holds all mutable state for the SignalR debug page.
/// </summary>
public sealed class SignalRDebugPageState(
    IOptions<ModuleSignalRUIOption> options,
    SignalRFacade signalRFacade,
    SignalRDebugJsClient jsClient,
    SignalRDebugTestTokenService testTokenService)
    : IAsyncDisposable
{
    private bool _disposed;
    private bool _initialized;
    private bool _verboseLogging;
    private bool _connectedUsersRefreshInProgress;

    /// <summary>
    /// Raised whenever the page should re-render.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Gets or sets the access token used when connecting from the browser.
    /// </summary>
    public string AccessToken { get; set; } = options.Value.DefaultAccessToken ?? string.Empty;

    /// <summary>
    /// Gets or sets the currently selected hub route.
    /// </summary>
    public string SelectedHubRoute { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user name used by the quick send box.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quick message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets the currently selected hub method name.
    /// </summary>
    public string SelectedMethodName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether verbose conversion logs are enabled.
    /// </summary>
    public bool VerboseLogging
    {
        get => _verboseLogging;
        set
        {
            if (_verboseLogging == value)
            {
                return;
            }

            _verboseLogging = value;
            jsClient.IsVerboseLoggingEnabled = value;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Gets the available hubs.
    /// </summary>
    public List<SignalRHubInfo> AvailableHubs { get; } = [];

    /// <summary>
    /// Gets the available methods for the selected hub.
    /// </summary>
    public List<HubMethodInfo> HubMethods { get; } = [];

    /// <summary>
    /// Gets the current method parameter values.
    /// </summary>
    public Dictionary<string, string> MethodParameters { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the message log.
    /// </summary>
    public List<SignalRMessage> Messages { get; } = [];

    /// <summary>
    /// Gets the connected user snapshot.
    /// </summary>
    public List<SignalRConnectedUserInfo> ConnectedUsers { get; } = [];

    /// <summary>
    /// Gets the latest server-side send diagnostics snapshot.
    /// </summary>
    public SignalRSendDiagnosticsSnapshot? SendDiagnostics { get; private set; }

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public SignalRConnectionState ConnectionState { get; private set; } = new();

    /// <summary>
    /// Gets a value indicating whether the host allows minting a random test-user token for local debugging.
    /// </summary>
    public bool CanGenerateTestToken => testTokenService.IsEnabled;

    /// <summary>
    /// Gets the username of the most recently generated test-user token, if any.
    /// </summary>
    public string? LastTestUserName { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the browser connection is established but the server treats it as anonymous.
    /// </summary>
    /// <remarks>
    /// This happens when the access token is missing, invalid, expired, or not accepted for the WebSocket transport,
    /// because anonymous connections are never registered in the server-side connected-user list.
    /// </remarks>
    public bool IsServerSideAnonymous { get; private set; }

    /// <summary>
    /// Initializes the page state and JavaScript client.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        jsClient.MessageReceived += OnMessageReceived;
        jsClient.ConnectionStateChanged += OnConnectionStateChanged;
        jsClient.MethodListenerChanged += OnMethodListenerChanged;

        await jsClient.InitializeAsync();
        jsClient.IsVerboseLoggingEnabled = _verboseLogging;

        await LoadHubsAsync();
        await LoadConnectedUsersAsync();
        await LoadSendDiagnosticsAsync();

        SyncFromJsClient();
        NotifyStateChanged();
    }

    /// <summary>
    /// Loads hub metadata from the SignalR facade.
    /// </summary>
    public async Task<bool> LoadHubsAsync()
    {
        var result = await signalRFacade.GetHubInfosAsync();
        if (result.IsFailed(out _, out var hubInfos) || hubInfos is null)
        {
            return false;
        }

        AvailableHubs.Clear();
        AvailableHubs.AddRange(hubInfos);

        if (string.IsNullOrWhiteSpace(SelectedHubRoute) && AvailableHubs.Count > 0)
        {
            SelectedHubRoute = AvailableHubs[0].Route;
        }

        jsClient.SetHubMetadata(hubInfos);
        SyncFromJsClient();
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Loads the currently connected users from the SignalR facade.
    /// </summary>
    public async Task<bool> LoadConnectedUsersAsync()
    {
        var result = await signalRFacade.GetConnectedUsersAsync();
        if (result.IsFailed(out _, out var users) || users is null)
        {
            return false;
        }

        ConnectedUsers.Clear();
        ConnectedUsers.AddRange(users);
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Loads the current server-side send diagnostics snapshot from the SignalR facade.
    /// </summary>
    public async Task<bool> LoadSendDiagnosticsAsync()
    {
        var result = await signalRFacade.GetSendDiagnosticsAsync();
        if (result.IsFailed(out _, out var snapshot) || snapshot is null)
        {
            return false;
        }

        SendDiagnostics = snapshot;
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Connects the browser debug client to the selected hub.
    /// </summary>
    public async Task<bool> ConnectAsync(string baseUri)
    {
        var hubUrl = BuildHubUrl(baseUri, SelectedHubRoute);
        var success = await jsClient.ConnectAsync(hubUrl, AccessToken);
        SyncFromJsClient();
        NotifyStateChanged();
        await RefreshConnectedUsersAfterConnectionChangeAsync();
        return success;
    }

    /// <summary>
    /// Mints a random test-user token, fills the token field with it, and connects to the selected hub.
    /// </summary>
    /// <param name="baseUri">The app base URI used to build the hub URL.</param>
    /// <returns><c>true</c> when the test user was minted and the connection succeeded.</returns>
    public async Task<bool> TestUserConnectAsync(string baseUri)
    {
        SignalRDebugTestToken token;
        try
        {
            token = testTokenService.GenerateTestUserToken();
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        // Keep the minted token in the input so it stays visible and copyable while stepping through breakpoints.
        AccessToken = token.AccessToken;
        LastTestUserName = token.Username;
        NotifyStateChanged();

        return await ConnectAsync(baseUri);
    }

    /// <summary>
    /// Disconnects the browser debug client.
    /// </summary>
    public async Task<bool> DisconnectAsync()
    {
        var success = await jsClient.DisconnectAsync();
        SyncFromJsClient();
        IsServerSideAnonymous = false;
        NotifyStateChanged();
        await RefreshConnectedUsersAfterConnectionChangeAsync();
        return success;
    }

    /// <summary>
    /// Sends the quick message currently entered on the page.
    /// </summary>
    public async Task<bool> SendMessageAsync()
    {
        var success = await jsClient.SendMessageAsync(UserName, Message);
        SyncFromJsClient();
        NotifyStateChanged();
        return success;
    }

    /// <summary>
    /// Invokes the currently selected hub method.
    /// </summary>
    public async Task<bool> InvokeSelectedMethodAsync()
    {
        var selectedMethod = HubMethods.FirstOrDefault(method => method.Name == SelectedMethodName);
        if (selectedMethod is null)
        {
            return false;
        }

        var parameters = selectedMethod.Args
            .Select(argumentInfo => new MethodCallParameter
            {
                Name = argumentInfo.Name,
                Type = argumentInfo.TypeName,
                Value = MethodParameters.GetValueOrDefault(argumentInfo.Name, string.Empty)
            })
            .ToList();

        var success = await jsClient.InvokeMethodAsync(SelectedMethodName, parameters);
        SyncFromJsClient();
        NotifyStateChanged();
        return success;
    }

    /// <summary>
    /// Enables or disables a single method listener.
    /// </summary>
    public async Task<bool> ToggleMethodListenerAsync(HubMethodInfo method, bool isListening)
    {
        var success = await jsClient.ToggleMethodListenerAsync(method.Name, isListening);
        SyncFromJsClient();
        NotifyStateChanged();
        return success;
    }

    /// <summary>
    /// Enables all available method listeners.
    /// </summary>
    public async Task<int> EnableAllListenersAsync()
    {
        var successCount = await jsClient.EnableAllListenersAsync();
        SyncFromJsClient();
        NotifyStateChanged();
        return successCount;
    }

    /// <summary>
    /// Disables all active method listeners.
    /// </summary>
    public async Task<int> DisableAllListenersAsync()
    {
        var successCount = await jsClient.DisableAllListenersAsync();
        SyncFromJsClient();
        NotifyStateChanged();
        return successCount;
    }

    /// <summary>
    /// Clears the page message log.
    /// </summary>
    public void ClearMessages()
    {
        jsClient.ClearMessages();
        SyncFromJsClient();
        NotifyStateChanged();
    }

    /// <summary>
    /// Applies a newly selected hub method and normalizes the parameter bag.
    /// </summary>
    public void SelectMethod(string methodName)
    {
        SelectedMethodName = methodName;

        var selectedMethod = HubMethods.FirstOrDefault(method => method.Name == methodName);
        if (selectedMethod is null)
        {
            MethodParameters.Clear();
            NotifyStateChanged();
            return;
        }

        foreach (var argumentInfo in selectedMethod.Args)
        {
            MethodParameters.TryAdd(argumentInfo.Name, string.Empty);
        }

        var activeArgumentNames = selectedMethod.Args
            .Select(argumentInfo => argumentInfo.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var key in MethodParameters.Keys.Where(key => !activeArgumentNames.Contains(key)).ToList())
        {
            MethodParameters.Remove(key);
        }

        NotifyStateChanged();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        jsClient.MessageReceived -= OnMessageReceived;
        jsClient.ConnectionStateChanged -= OnConnectionStateChanged;
        jsClient.MethodListenerChanged -= OnMethodListenerChanged;

        await jsClient.DisposeAsync();
    }

    private void OnMessageReceived(SignalRMessage _)
    {
        SyncFromJsClient();
        NotifyStateChanged();
    }

    private void OnConnectionStateChanged(SignalRConnectionState state)
    {
        SyncFromJsClient();
        NotifyStateChanged();
        _ = RefreshConnectedUsersAfterConnectionChangeAsync();
    }

    private void OnMethodListenerChanged(HubMethodInfo _)
    {
        SyncFromJsClient();
        NotifyStateChanged();
    }

    /// <summary>
    /// Refreshes the connected-user snapshot after the browser connection reached or left a stable state.
    /// </summary>
    /// <remarks>
    /// The server registers a connection in <c>OnConnectedAsync</c> before the browser reports the established
    /// connection, so refreshing here observes the debug client itself. Failures are swallowed because this is a
    /// best-effort auto refresh; the manual refresh button still surfaces errors.
    /// </remarks>
    private async Task RefreshConnectedUsersAfterConnectionChangeAsync()
    {
        if (_disposed
            || ConnectionState.Status is not ("Connected" or "Disconnected")
            || _connectedUsersRefreshInProgress)
        {
            return;
        }

        _connectedUsersRefreshInProgress = true;
        try
        {
            await LoadConnectedUsersAsync();
        }
        catch (Exception)
        {
            // Best-effort auto refresh; the manual refresh button reports failures to the user.
        }
        finally
        {
            _connectedUsersRefreshInProgress = false;
        }

        UpdateAnonymousConnectionDiagnostic();
        NotifyStateChanged();
    }

    /// <summary>
    /// Marks the connection as server-side anonymous when the browser reports it as established but the
    /// server-side connected-user registry does not contain its connection identifier.
    /// </summary>
    private void UpdateAnonymousConnectionDiagnostic()
    {
        IsServerSideAnonymous = ConnectionState.IsConnected
            && !string.IsNullOrEmpty(ConnectionState.ConnectionId)
            && ConnectedUsers.All(user =>
                !string.Equals(user.ConnectionId, ConnectionState.ConnectionId, StringComparison.Ordinal));
    }

    private void SyncFromJsClient()
    {
        Messages.Clear();
        Messages.AddRange(jsClient.GetMessages());

        HubMethods.Clear();
        HubMethods.AddRange(jsClient.GetHubMethods());

        ConnectionState = jsClient.GetConnectionState();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private static string BuildHubUrl(string baseUri, string selectedHubRoute)
    {
        var normalizedBaseUri = baseUri.TrimEnd('/');
        var normalizedHubRoute = selectedHubRoute.StartsWith("/", StringComparison.Ordinal)
            ? selectedHubRoute
            : $"/{selectedHubRoute}";

        return $"{normalizedBaseUri}{normalizedHubRoute}";
    }
}

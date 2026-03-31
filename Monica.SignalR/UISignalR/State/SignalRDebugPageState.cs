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
    SignalRDebugJsClient jsClient)
    : IAsyncDisposable
{
    private bool _disposed;
    private bool _initialized;
    private bool _verboseLogging;

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
    /// Gets the current connection state.
    /// </summary>
    public SignalRConnectionState ConnectionState { get; private set; } = new();

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
    /// Connects the browser debug client to the selected hub.
    /// </summary>
    public async Task<bool> ConnectAsync(string baseUri)
    {
        var hubUrl = BuildHubUrl(baseUri, SelectedHubRoute);
        var success = await jsClient.ConnectAsync(hubUrl, AccessToken);
        SyncFromJsClient();
        NotifyStateChanged();
        return success;
    }

    /// <summary>
    /// Disconnects the browser debug client.
    /// </summary>
    public async Task<bool> DisconnectAsync()
    {
        var success = await jsClient.DisconnectAsync();
        SyncFromJsClient();
        NotifyStateChanged();
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

    private void OnConnectionStateChanged(SignalRConnectionState _)
    {
        SyncFromJsClient();
        NotifyStateChanged();
    }

    private void OnMethodListenerChanged(HubMethodInfo _)
    {
        SyncFromJsClient();
        NotifyStateChanged();
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

using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Facades;
using Monica.AI.KnowledgeBase.Facades;
using Monica.AI.Models;
using Monica.AI.UI.Localization;
using Monica.AI.UI.UIChat.Models;
using Monica.AI.UI.UIChat.Support;
using Monica.Modules;
using Monica.UI.Shell.Support;
using MudBlazor;
using KnowledgeBaseModel = Monica.AI.KnowledgeBase.Models.KnowledgeBase;

namespace Monica.AI.UI.UIChat.State;

/// <summary>
/// Owns mutable page state and UI orchestration for the AI chat page.
/// </summary>
public sealed partial class ChatPageState : IDisposable
{
    private readonly ChatFacade _chatFacade;
    private readonly ChatSessionStore _sessionStore;
    private readonly ProviderFacade _providerFacade;
    private readonly AgentCapabilityFacade _capabilityFacade;
    private readonly ModuleAIUIOption _options;
    private readonly ISnackbar _snackbar;
    private readonly IDialogService _dialogService;
    private readonly KnowledgeBaseFacade _knowledgeBaseFacade;
    private readonly IStringLocalizer<AIResource> _localizer;
    private readonly IBrowserStorage _browserStorage;
    private bool _isAttached;

    /// <summary>
    /// Initializes the page state and its collaborators.
    /// </summary>
    public ChatPageState(
        ChatFacade chatFacade,
        ChatSessionStore sessionStore,
        ProviderFacade providerFacade,
        AgentCapabilityFacade capabilityFacade,
        IOptions<ModuleAIUIOption> options,
        ISnackbar snackbar,
        IDialogService dialogService,
        KnowledgeBaseFacade knowledgeBaseFacade,
        IStringLocalizer<AIResource> localizer,
        IBrowserStorage browserStorage)
    {
        _chatFacade = chatFacade;
        _sessionStore = sessionStore;
        _providerFacade = providerFacade;
        _capabilityFacade = capabilityFacade;
        _options = options.Value;
        _snackbar = snackbar;
        _dialogService = dialogService;
        _knowledgeBaseFacade = knowledgeBaseFacade;
        _localizer = localizer;
        _browserStorage = browserStorage;
    }

    /// <summary>
    /// Raised when the page should re-render.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Whether the session list should be shown.
    /// </summary>
    public bool ShowSessionList => _options.ShowSessionList;

    /// <summary>
    /// Whether the provider selector should be shown.
    /// </summary>
    public bool ShowProviderSelector => _options.ShowProviderSelector;

    /// <summary>
    /// All available chat sessions.
    /// </summary>
    public IReadOnlyList<ChatSession> Sessions => _sessionStore.Sessions;

    /// <summary>
    /// Current chat session identifier.
    /// </summary>
    public string? CurrentSessionId => _sessionStore.CurrentSessionId;

    /// <summary>
    /// Current provider display name shown by the page.
    /// </summary>
    public string CurrentProviderName { get; private set; } = string.Empty;

    /// <summary>
    /// Available providers shown by the page.
    /// </summary>
    public IReadOnlyList<AIProviderInfo> Providers { get; private set; } = [];

    /// <summary>
    /// Models available for the currently selected provider.
    /// </summary>
    public IReadOnlyList<AIModelInfo> CurrentProviderModels { get; private set; } = [];

    /// <summary>
    /// Default provider identifier used before a session exists.
    /// </summary>
    public string? DefaultProviderId { get; private set; }

    /// <summary>
    /// Default model name used before a session exists.
    /// </summary>
    public string? DefaultModelName { get; private set; }

    /// <summary>
    /// Current chat session instance.
    /// </summary>
    public ChatSession? CurrentSession { get; private set; }

    /// <summary>
    /// Current provider identifier resolved from the session or defaults.
    /// </summary>
    public string? CurrentProviderId => CurrentSession?.ProviderId ?? DefaultProviderId;

    /// <summary>
    /// Current model name resolved from the session or defaults.
    /// </summary>
    public string? CurrentModelName => CurrentSession?.ModelName ?? DefaultModelName;

    /// <summary>
    /// Current message list shown by the page.
    /// </summary>
    public IReadOnlyList<AIChatMessage> CurrentMessages
        => CurrentSession?.Messages ?? (IReadOnlyList<AIChatMessage>)Array.Empty<AIChatMessage>();

    /// <summary>
    /// Per-request token usage records for the latest assistant message.
    /// </summary>
    public IReadOnlyList<AIChatRequestUsage> LatestRequestUsages
        => CurrentMessages.LastOrDefault(message => message.Role == AIChatRole.Assistant)?.RequestUsages
           ?? [];

    /// <summary>
    /// Whether a request is currently in flight.
    /// </summary>
    public bool IsSending { get; private set; }

    /// <summary>
    /// Current error message shown by the page.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Whether retry is currently available.
    /// </summary>
    public bool CanRetry { get; private set; }

    /// <summary>
    /// Last user message sent by the page.
    /// </summary>
    public string? LastMessage { get; private set; }

    /// <summary>
    /// Current streaming response sequence, if any.
    /// </summary>
    public IAsyncEnumerable<AgentResponseUpdate>? StreamingContent { get; private set; }

    /// <summary>
    /// Current cancellation token source for the active request.
    /// </summary>
    public CancellationTokenSource? CancellationTokenSource { get; private set; }

    /// <summary>
    /// Current cancellation token for the active request.
    /// </summary>
    public CancellationToken CancellationToken { get; private set; }

    /// <summary>
    /// Whether reasoning mode is currently enabled.
    /// </summary>
    public bool ReasoningEnabled { get; private set; }

    /// <summary>
    /// Whether the selected provider/model supports reasoning.
    /// </summary>
    public bool SupportsReasoning { get; private set; }

    /// <summary>
    /// Whether tool-call debug display is enabled.
    /// </summary>
    public bool ToolDebugEnabled { get; private set; }

    /// <summary>
    /// Available knowledge bases shown by the chat page.
    /// </summary>
    public IReadOnlyList<KnowledgeBaseModel> KnowledgeBases { get; private set; } = [];

    /// <summary>
    /// Selected knowledge-base identifiers for the current page session.
    /// </summary>
    public List<string> SelectedKnowledgeBaseIds { get; private set; } = [];

    /// <summary>
    /// Reference-completion candidates for explicit Skill and MCP references.
    /// </summary>
    public IReadOnlyList<AgentCapabilityReferenceCandidate> CapabilityCandidates { get; private set; } = [];

    /// <summary>
    /// Initialize the page for the current visit.
    /// </summary>
    public async Task InitializeAsync()
    {
        Attach();
        ResetPageState();

        Providers = ChatProviderResolver.GetChatProviders(_chatFacade.GetProviders());
        InitializeDefaultProvider();
        InitializeKnowledgeBaseSelection();
        await LoadPersistedPreferencesAsync();
        await LoadKnowledgeBasesAsync();
        await LoadCapabilityCandidatesAsync();
        await EnsureSessionExistsAsync();
        UpdateCurrentSession();
        NotifyStateChanged();
    }

    /// <summary>
    /// Creates the current chat-container parameter object.
    /// </summary>
    public ChatContainerParameters BuildChatContainerParameters()
    {
        return new ChatContainerParameters
        {
            Messages = CurrentMessages,
            StreamingContent = StreamingContent,
            IsSending = IsSending,
            ShowRetry = CanRetry,
            ProviderName = CurrentProviderName,
            ModelName = CurrentModelName,
            ShowProviderInfo = !ShowProviderSelector,
            AvailableModels = CurrentProviderModels,
            LatestRequestUsages = LatestRequestUsages,
            ErrorMessage = ErrorMessage,
            CancellationToken = CancellationToken,
            EnableMarkdown = _options.EnableMarkdown,
            EnableAutoScroll = _options.EnableAutoScroll,
            SupportsReasoning = SupportsReasoning,
            ReasoningEnabled = ReasoningEnabled,
            ToolDebugEnabled = ToolDebugEnabled,
            KnowledgeBases = KnowledgeBases,
            SelectedKnowledgeBaseIds = SelectedKnowledgeBaseIds,
            CapabilityCandidates = CapabilityCandidates,
            OnSendMessage = EventCallback.Factory.Create<ChatSendRequest>(this, SendMessageAsync),
            OnStreamComplete = EventCallback.Factory.Create<string>(this, CompleteStream),
            OnStreamError = EventCallback.Factory.Create<string>(this, SetStreamError),
            OnCancel = EventCallback.Factory.Create(this, CancelAsync),
            OnRetry = EventCallback.Factory.Create(this, RetryLastMessageAsync),
            OnErrorDismissed = EventCallback.Factory.Create(this, DismissError),
            OnEditMessage = EventCallback.Factory.Create<(AIChatMessage, string)>(this, EditMessage),
            OnRetryMessage = EventCallback.Factory.Create<AIChatMessage>(this, RetryMessage),
            OnModelChanged = EventCallback.Factory.Create<string>(this, ChangeModelAsync),
            ReasoningEnabledChanged = EventCallback.Factory.Create<bool>(this, SetReasoningEnabled),
            SelectedKnowledgeBaseIdsChanged = EventCallback.Factory.Create<List<string>>(this, SetSelectedKnowledgeBases)
        };
    }

    /// <summary>
    /// Resolve the current tool-debug toggle tooltip.
    /// </summary>
    public string GetToolDebugTooltip()
        => ToolDebugEnabled
            ? _localizer["Chat:ToolCalls:DisableDebug"]
            : _localizer["Chat:ToolCalls:EnableDebug"];

    private void Attach()
    {
        if (_isAttached)
        {
            return;
        }

        _sessionStore.CurrentSessionChanged += OnCurrentSessionChanged;
        _sessionStore.SessionsChanged += OnSessionsChanged;
        _isAttached = true;
    }

    private void ResetPageState()
    {
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
        CancellationToken = CancellationToken.None;
        StreamingContent = null;
        IsSending = false;
        LastMessage = null;
        CurrentSession = null;
        CurrentProviderName = string.Empty;
        CurrentProviderModels = [];
        ClearError();
    }

    private void InitializeDefaultProvider()
    {
        var defaultProvider = ChatProviderResolver.GetPreferredChatProvider(
            Providers,
            _chatFacade.GetDefaultProvider()?.ProviderId);
        if (defaultProvider == null)
        {
            DefaultProviderId = null;
            DefaultModelName = null;
            CurrentProviderName = string.Empty;
            CurrentProviderModels = [];
            SupportsReasoning = false;
            SetPageError(_localizer["Provider:NoChatProvider"], showSnackbar: false);
            return;
        }

        ClearError();
        DefaultProviderId = defaultProvider.ProviderId;
        DefaultModelName = ChatProviderResolver.GetPreferredChatModel(defaultProvider, defaultProvider.DefaultModel);
        CurrentProviderName = defaultProvider.DisplayName;
        CurrentProviderModels = ChatProviderResolver.GetChatModels(defaultProvider);
        SupportsReasoning = ChatProviderResolver.GetReasoningSupport(
            Providers,
            defaultProvider.ProviderId,
            DefaultModelName);
    }

    private void InitializeKnowledgeBaseSelection()
    {
        SelectedKnowledgeBaseIds = new List<string>(_options.DefaultKnowledgeBaseIds);
    }

    private void OnCurrentSessionChanged()
    {
        UpdateCurrentSession();
        NotifyStateChanged();
    }

    private void OnSessionsChanged()
    {
        NotifyStateChanged();
    }

    private void UpdateCurrentSession()
    {
        var currentSession = _sessionStore.CurrentSession;
        CurrentSession = currentSession;

        if (currentSession == null)
        {
            return;
        }

        var provider = ChatProviderResolver.FindProvider(Providers, currentSession.ProviderId);
        if (provider != null)
        {
            CurrentProviderName = provider.DisplayName;
            CurrentProviderModels = ChatProviderResolver.GetChatModels(provider);
        }
    }

    private void ClearError()
    {
        ErrorMessage = null;
        CanRetry = false;
    }

    private void SetError(string message, bool canRetry = false)
    {
        ErrorMessage = message;
        CanRetry = canRetry;
    }

    private void SetupCancellationToken()
    {
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = new CancellationTokenSource();
        CancellationToken = CancellationTokenSource.Token;

        if (_options.RequestTimeoutMs > 0)
        {
            CancellationTokenSource.CancelAfter(_options.RequestTimeoutMs);
        }
    }

    private void SetPageError(string message, bool canRetry = false, bool showSnackbar = true)
    {
        IsSending = false;
        StreamingContent = null;
        SetError(message, canRetry);

        if (showSnackbar)
        {
            _snackbar.Add(message, Severity.Error);
        }

        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
        CancellationToken = CancellationToken.None;
        StreamingContent = null;
        IsSending = false;

        if (_isAttached)
        {
            _sessionStore.CurrentSessionChanged -= OnCurrentSessionChanged;
            _sessionStore.SessionsChanged -= OnSessionsChanged;
            _isAttached = false;
        }
    }
}

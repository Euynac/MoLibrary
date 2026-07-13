using System.Net.Http;
using Monica.AI.Chat.Models;
using Monica.AI.Models;
using Monica.AI.UI.UIChat.Components;
using Monica.AI.UI.UIChat.Models;
using Monica.AI.UI.UIChat.Support;
using Monica.Core.Results;
using MudBlazor;

namespace Monica.AI.UI.UIChat.State;

public sealed partial class ChatPageState
{
    private async Task SendMessageAsync(ChatSendRequest request)
    {
        var message = request.Message;
        if (string.IsNullOrWhiteSpace(message) || IsSending)
        {
            return;
        }

        ClearError();
        LastMessage = message;

        var sessionId = await EnsureCurrentSessionAsync();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        var resolvedSessionId = sessionId!;
        var session = _workspace.GetLoadedSession(resolvedSessionId);
        if (session is null)
        {
            SetPageError(_localizer["Error:Generic"]);
            return;
        }

        if (!CanContinueSession(session))
        {
            _snackbar.Add(_localizer["Chat:History:Warnings:ProviderUnavailable"], Severity.Warning);
            return;
        }

        _ = _chatFacade.UpdateSettings(
            session,
            session.Settings with { ReasoningEnabled = ReasoningEnabled });
        _ = _chatFacade.UpdateRuntimeContext(
            session,
            BuildRuntimeContext(SelectedKnowledgeBaseIds));
        if (session.Messages.Count == 0)
        {
            _ = _chatFacade.Rename(session, ChatProviderResolver.GenerateSessionTitle(message));
        }

        await StartStreamingMessageAsync(resolvedSessionId, message);
    }

    private async Task StartStreamingMessageAsync(string sessionId, string message)
    {
        IsSending = true;
        SetupCancellationToken();
        NotifyStateChanged();
        ChatSession? session = null;

        try
        {
            session = _workspace.GetLoadedSession(sessionId);
            if (session == null)
            {
                SetPageError(_localizer["Error:Generic"]);
                return;
            }

            StreamingState = new ChatStreamingState();
            UpdateCurrentSession();
            NotifyStateChanged();
            await ConsumeStreamAsync(
                session,
                _chatFacade.SendMessageStreamingAsync(session, message, CancellationToken));
        }
        catch (OperationCanceledException)
        {
            await CompleteStreamAsync(session);
        }
        catch (HttpRequestException ex)
        {
            await RecordStreamErrorAsync(session, $"{_localizer["Error:NetworkError"]}: {ex.Message}");
        }
        catch (Exception ex)
        {
            await RecordStreamErrorAsync(session, $"{_localizer["Error:Generic"]}: {ex.Message}");
        }
    }

    private async Task RecordStreamErrorAsync(ChatSession? session, string error)
    {
        if (session is null)
        {
            SetPageError(error);
            return;
        }

        _ = _chatFacade.RecordError(session, error);
        await CompleteStreamAsync(session);
    }

    private async Task CompleteStreamAsync(ChatSession? session)
    {
        IsSending = false;
        StreamingState = null;
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
        CancellationToken = CancellationToken.None;
        UpdateCurrentSession();
        if (session is not null)
        {
            await _workspace.SaveSessionAsync(session);
            if (session.RestorationState == ChatSessionRestorationState.TranscriptFallback)
            {
                _snackbar.Add(_localizer["Chat:History:Warnings:RuntimeFallback"], Severity.Warning);
            }
        }

        NotifyStateChanged();
    }

    private async Task CancelAsync()
    {
        if (CancellationTokenSource != null && !CancellationTokenSource.IsCancellationRequested)
        {
            await CancellationTokenSource.CancelAsync();
            _snackbar.Add(_localizer["Chat:Status:GenerationStopped"], Severity.Info);
        }

        NotifyStateChanged();
    }

    private async Task RetryLastMessageAsync()
    {
        if (string.IsNullOrEmpty(LastMessage))
        {
            return;
        }

        ClearError();
        await SendMessageAsync(new ChatSendRequest(LastMessage));
    }

    private async Task EditMessage((AIChatMessage Message, string NewContent) args)
    {
        if (string.IsNullOrWhiteSpace(args.NewContent) || IsSending)
        {
            return;
        }

        ClearError();
        LastMessage = args.NewContent;
        IsSending = true;
        SetupCancellationToken();

        var session = _workspace.CurrentSession;
        if (session == null)
        {
            SetPageError(_localizer["Error:Generic"]);
            return;
        }

        _ = _chatFacade.UpdateSettings(
            session,
            session.Settings with { ReasoningEnabled = ReasoningEnabled });

        StreamingState = new ChatStreamingState();
        UpdateCurrentSession();
        NotifyStateChanged();
        await ConsumeStreamAsync(
            session,
            _chatFacade.EditMessageAsync(session, args.Message.Id, args.NewContent, CancellationToken));
    }

    private async Task RetryMessage(AIChatMessage message)
    {
        if (IsSending)
        {
            return;
        }

        ClearError();
        IsSending = true;
        SetupCancellationToken();

        var session = _workspace.CurrentSession;
        if (session == null)
        {
            SetPageError(_localizer["Error:Generic"]);
            return;
        }

        _ = _chatFacade.UpdateSettings(
            session,
            session.Settings with { ReasoningEnabled = ReasoningEnabled });

        StreamingState = new ChatStreamingState();
        UpdateCurrentSession();
        NotifyStateChanged();
        await ConsumeStreamAsync(
            session,
            _chatFacade.RetryMessageAsync(session, message.Id, CancellationToken));
    }

    private void DismissError()
    {
        ClearError();
        NotifyStateChanged();
    }

    private async Task ConsumeStreamAsync(
        ChatSession session,
        IAsyncEnumerable<Res<ChatStreamEvent>> stream)
    {
        ChatApprovalRequestEvent? pendingApproval = null;
        try
        {
            await foreach (var result in stream)
            {
                if (result.IsFailed(out var error, out var streamEvent))
                {
                    await RecordStreamErrorAsync(session, error.Message ?? _localizer["Error:Generic"]);
                    return;
                }

                StreamingState ??= new ChatStreamingState();
                StreamingState.Apply(streamEvent);
                pendingApproval = streamEvent as ChatApprovalRequestEvent ?? pendingApproval;
                UpdateCurrentSession();
                NotifyStateChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // The chat facade normally converts cancellation to a completion event. This guard also
            // covers cancellation before the first provider update is available.
        }
        catch (Exception ex)
        {
            await RecordStreamErrorAsync(session, $"{_localizer["Error:Generic"]}: {ex.Message}");
            return;
        }

        if (pendingApproval is not null)
        {
            await ContinueAfterApprovalAsync(session, pendingApproval);
            return;
        }

        await CompleteStreamAsync(session);
    }

    private async Task ContinueAfterApprovalAsync(
        ChatSession session,
        ChatApprovalRequestEvent approval)
    {
        var parameters = new DialogParameters
        {
            [nameof(ExternalScriptApprovalDialog.Request)] = approval
        };
        var dialog = await _dialogService.ShowAsync<ExternalScriptApprovalDialog>(
            _localizer["Chat:Approval:Title"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = false });
        var result = await dialog.Result;
        var approved = result is { Canceled: false, Data: true };
        StreamingState?.ResumeAfterApproval();
        IsSending = true;
        NotifyStateChanged();

        var reason = approved
            ? _localizer["Chat:Approval:ApprovedReason"].Value
            : _localizer["Chat:Approval:RejectedReason"].Value;
        await ConsumeStreamAsync(
            session,
            _chatFacade.ContinueApprovalAsync(
                session,
                approval.ApprovalId,
                approved,
                reason,
                CancellationToken));
    }

    private bool CanContinueSession(ChatSession session)
    {
        var provider = ChatProviderResolver.FindProvider(Providers, session.ProviderId);
        return provider is not null
               && ChatProviderResolver.IsChatModelValid(provider, session.ModelName);
    }
}

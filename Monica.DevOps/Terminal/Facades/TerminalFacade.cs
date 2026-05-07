using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.DevOps.Localization;
using Monica.DevOps.Terminal.Models;
using Monica.DevOps.Terminal.Services;

namespace Monica.DevOps.Terminal.Facades;

/// <summary>
/// Public terminal entry point used by the DevOps terminal UI.
/// </summary>
public sealed class TerminalFacade(
    TerminalSessionService sessionService,
    ILogger<TerminalFacade> logger,
    IStringLocalizer<TerminalResource> localizer)
{
    /// <summary>
    /// Gets the local terminal environment.
    /// </summary>
    /// <returns>The detected environment information.</returns>
    public Task<Res<TerminalEnvironmentInfo>> GetEnvironmentAsync()
        => ExecuteAsync(
            () => Task.FromResult(sessionService.GetEnvironment()),
            localizer["Messages:LoadEnvironmentFailed"].Value);

    /// <summary>
    /// Gets active terminal sessions for the requested owner.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <returns>The active sessions.</returns>
    public Task<Res<IReadOnlyList<TerminalSession>>> GetSessionsAsync(string ownerKey)
        => ExecuteAsync(
            () => Task.FromResult(sessionService.GetSessions(ownerKey)),
            "Failed to load terminal sessions.");

    /// <summary>
    /// Creates a terminal session for the requested owner.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="request">Session creation request.</param>
    /// <returns>The created session.</returns>
    public Task<Res<TerminalSession>> CreateSessionAsync(string ownerKey, TerminalSessionCreateRequest? request = null)
        => ExecuteAsync(
            () => Task.FromResult(sessionService.CreateSession(ownerKey, request)),
            localizer["Messages:CreateSessionFailed"].Value);

    /// <summary>
    /// Closes an owner-scoped terminal session.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <returns>Operation result.</returns>
    public Task<Res> CloseSessionAsync(string ownerKey, string sessionId)
        => ExecuteAsync(
            () =>
            {
                sessionService.CloseSession(ownerKey, sessionId);
                return Task.CompletedTask;
            },
            "Terminal session closed.",
            "Failed to close terminal session.");

    /// <summary>
    /// Cancels the current command in an owner-scoped terminal session.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <returns>Operation result.</returns>
    public Task<Res> CancelCommandAsync(string ownerKey, string sessionId)
        => ExecuteAsync(
            () =>
            {
                sessionService.CancelCommand(ownerKey, sessionId);
                return Task.CompletedTask;
            },
            "Terminal command cancellation requested.",
            localizer["Messages:CancelFailed"].Value);

    /// <summary>
    /// Clears retained output for an owner-scoped terminal session.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <returns>Operation result.</returns>
    public Task<Res> ClearOutputAsync(string ownerKey, string sessionId)
        => ExecuteAsync(
            () =>
            {
                sessionService.ClearOutput(ownerKey, sessionId);
                return Task.CompletedTask;
            },
            "Terminal output cleared.",
            "Failed to clear terminal output.");

    /// <summary>
    /// Executes a command in an owner-scoped terminal session.
    /// </summary>
    /// <param name="ownerKey">Stable owner key that isolates browser or user terminal sessions.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <param name="request">Command request.</param>
    /// <param name="outputCallback">Optional callback invoked when a command output line is captured.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command result.</returns>
    public Task<Res<TerminalCommandResult>> ExecuteCommandAsync(
        string ownerKey,
        string sessionId,
        TerminalCommandRequest request,
        Func<TerminalOutputLine, CancellationToken, ValueTask>? outputCallback = null,
        CancellationToken ct = default)
        => ExecuteAsync(
            () => sessionService.ExecuteCommandAsync(ownerKey, sessionId, request, outputCallback, ct),
            localizer["Messages:RunCommandFailed"].Value);

    private async Task<Res<T>> ExecuteAsync<T>(
        Func<Task<T>> action,
        string failureMessage)
    {
        try
        {
            return Res.Ok(await action());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{FailureMessage}", failureMessage);
            return Res.Fail($"{failureMessage}: {ex.GetMessageRecursively()}", GetResultStatus(ex));
        }
    }

    private async Task<Res> ExecuteAsync(
        Func<Task> action,
        string successMessage,
        string failureMessage)
    {
        try
        {
            await action();
            return Res.Ok(successMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{FailureMessage}", failureMessage);
            return Res.Fail($"{failureMessage}: {ex.GetMessageRecursively()}", GetResultStatus(ex));
        }
    }

    private static ResStatus GetResultStatus(Exception exception)
    {
        return exception switch
        {
            ArgumentException => ResStatus.BadRequest,
            InvalidOperationException => ResStatus.BadRequest,
            KeyNotFoundException => ResStatus.BadRequest,
            DirectoryNotFoundException => ResStatus.BadRequest,
            UnauthorizedAccessException => ResStatus.Forbidden,
            _ => ResStatus.InternalError
        };
    }
}

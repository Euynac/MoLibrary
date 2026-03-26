using Monica.Tool.Extensions;
using Monica.Tool.MoResponse;

namespace Monica.Core.ExceptionHandling.Exceptions;

/// <summary>
/// Base exception type that carries a user-facing message.
/// Use this to separate the message shown to end users from the technical detail used for diagnostics.
/// </summary>
/// <remarks>
/// <para><see cref="DisplayMessage"/> is written to <c>Res.Message</c> for UI display.</para>
/// <para><see cref="TechnicalDetail"/> is written to <c>Res.ExtraInfo["detail"]</c> for diagnostics.</para>
/// </remarks>
public abstract class DisplayMessageException : Exception
{
    /// <summary>
    /// Gets the user-friendly message shown to the client.
    /// </summary>
    public string DisplayMessage { get; }

    /// <summary>
    /// Gets the technical detail used for logging and diagnostics.
    /// </summary>
    public string? TechnicalDetail { get; }

    /// <summary>
    /// Gets the response code returned to the client.
    /// Derived types can override the default <see cref="ResponseCode.BadRequest"/>.
    /// </summary>
    public virtual ResponseCode ResponseCode => ResponseCode.BadRequest;

    /// <summary>
    /// Initializes a new exception with a user-facing message and optional technical detail.
    /// </summary>
    /// <param name="displayMessage">The user-facing error message.</param>
    /// <param name="technicalDetail">Optional technical detail for debugging.</param>
    protected DisplayMessageException(string displayMessage, string? technicalDetail = null)
        : base($"{displayMessage}{technicalDetail?.BeAfter(": ")}")
    {
        DisplayMessage = displayMessage;
        TechnicalDetail = technicalDetail;
    }
}

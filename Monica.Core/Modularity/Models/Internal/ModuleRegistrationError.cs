using System.Text;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Represents an error that occurred during module registration.
/// </summary>
internal sealed class ModuleRegistrationError
{
    /// <summary>
    /// The type of the module where the error occurred.
    /// </summary>
    public required Type ModuleType { get; set; }

    /// <summary>
    /// Description of the error that occurred.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// The type of error that occurred during module registration.
    /// </summary>
    public ModuleRegistrationErrorType ErrorType { get; set; } = ModuleRegistrationErrorType.General;

    /// <summary>
    /// The configuration phase where the error occurred.
    /// </summary>
    public ModulePhase? Phase { get; set; }

    /// <summary>
    /// Gets or sets the stable startup-work identity when the failure belongs to a worker or serial commit.
    /// </summary>
    public string? WorkItemId { get; set; }

    /// <summary>
    /// The stack trace of the error that occurred during module registration.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Returns a formatted string representation of the module registration error.
    /// </summary>
    /// <returns>A detailed formatted error message.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"  - Error Type: {ErrorType}");
        sb.AppendLine($"  - Details: {ErrorMessage}");

        if (Phase.HasValue)
        {
            sb.AppendLine($"  - Phase: {Phase}");
        }

        if (StackTrace is not null)
        {
            sb.AppendLine($"  - Stack Trace: {StackTrace}");
        }

        return sb.ToString();
    }
}

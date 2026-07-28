using Monica.Authority.Authorization.Models;

namespace Monica.Authority.Authorization.Abstractions;

/// <summary>
/// Enforces authorization metadata for one Monica execution boundary.
/// </summary>
/// <remarks>
/// Implementations must return successfully when the descriptor has no authorization metadata. Authorization failures
/// are reported through the module's standard authorization exceptions.
/// </remarks>
public interface IExecutionAuthorizationService
{
    /// <summary>
    /// Verifies that the current principal may execute the described operation.
    /// </summary>
    /// <param name="context">The execution descriptor and principal to authorize.</param>
    /// <param name="cancellationToken">
    /// Signals cancellation before authorization and between underlying ASP.NET Core operations that do not accept a
    /// cancellation token themselves.
    /// </param>
    Task CheckAsync(
        ExecutionAuthorizationContext context,
        CancellationToken cancellationToken);
}

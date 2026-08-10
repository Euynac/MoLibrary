namespace Monica.UI.Shell.Support;

/// <summary>
/// Defines the authorization boundary for a page registered with the Monica UI shell.
/// </summary>
/// <remarks>
/// Implementations are resolved from the current Blazor circuit scope. They must fail closed when required host
/// services or configuration are unavailable and must not invoke the protected page's facade while evaluating access.
/// </remarks>
public interface IPageAccessPolicy
{
    /// <summary>
    /// Determines whether the current circuit may navigate to and render the protected page.
    /// </summary>
    /// <param name="cancellationToken">Cancels the current access evaluation.</param>
    /// <returns><see langword="true"/> when access is allowed; otherwise <see langword="false"/>.</returns>
    Task<bool> IsAuthorizedAsync(CancellationToken cancellationToken = default);
}

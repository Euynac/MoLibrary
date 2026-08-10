using Monica.UI.Shell.Models;

namespace Monica.UI.Shell.Support;

/// <summary>
/// Evaluates page access metadata for both shell navigation and direct page requests.
/// </summary>
/// <remarks>
/// The evaluator is scoped to the current Blazor circuit so policies can safely consume circuit-owned authentication
/// state. Missing policy registrations, invalid policy implementations, and policy failures deny access.
/// </remarks>
public sealed class PageAccessEvaluator(IPageCatalog pageCatalog, IServiceProvider services)
{
    /// <summary>
    /// Determines whether the current circuit may access a registered route.
    /// </summary>
    /// <param name="route">The route registered with the UI shell.</param>
    /// <param name="cancellationToken">Cancels the current access evaluation.</param>
    /// <returns><see langword="true"/> when the route exists and its policy allows access; otherwise <see langword="false"/>.</returns>
    public Task<bool> IsAuthorizedAsync(string route, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        var normalizedRoute = route.Trim().Trim('/');
        var page = pageCatalog.GetRegisteredPages().FirstOrDefault(page =>
            string.Equals(page.Route, normalizedRoute, StringComparison.OrdinalIgnoreCase));

        return page is null
            ? Task.FromResult(false)
            : IsAuthorizedAsync(page, cancellationToken);
    }

    /// <summary>
    /// Determines whether the current circuit may access a registered page.
    /// </summary>
    /// <param name="page">The immutable page registration.</param>
    /// <param name="cancellationToken">Cancels the current access evaluation.</param>
    /// <returns><see langword="true"/> when no policy is required or its policy allows access.</returns>
    public async Task<bool> IsAuthorizedAsync(
        PageDefinition page,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        cancellationToken.ThrowIfCancellationRequested();

        if (page.AccessPolicyType is null)
        {
            return true;
        }

        try
        {
            if (services.GetService(page.AccessPolicyType) is not IPageAccessPolicy policy)
            {
                return false;
            }

            return await policy.IsAuthorizedAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the current circuit may access the page represented by a navigation item.
    /// </summary>
    /// <param name="navigationItem">The immutable navigation registration.</param>
    /// <param name="cancellationToken">Cancels the current access evaluation.</param>
    /// <returns><see langword="true"/> when its page policy allows access.</returns>
    public Task<bool> IsAuthorizedAsync(
        NavigationItem navigationItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(navigationItem);
        return IsAuthorizedAsync(navigationItem.Page, cancellationToken);
    }
}

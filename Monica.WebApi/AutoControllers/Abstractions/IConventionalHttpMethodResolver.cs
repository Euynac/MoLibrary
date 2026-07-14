namespace Monica.WebApi.AutoControllers.Abstractions;

/// <summary>
/// Resolves host-specific HTTP method conventions for generated AutoController actions.
/// </summary>
/// <remarks>
/// Implementations are expected to be safe for concurrent use. The built-in implementation
/// snapshots the host's configured HTTP method rules when the resolver is first created,
/// so later mutations of an options object do not alter an active endpoint graph.
/// </remarks>
public interface IConventionalHttpMethodResolver
{
    /// <summary>
    /// Infers an HTTP method from an action name by applying the configured prefixes.
    /// </summary>
    /// <param name="actionName">The action name to inspect.</param>
    /// <returns>The configured HTTP method, or the host's fallback method when no prefix matches.</returns>
    string Resolve(string actionName);

    /// <summary>
    /// Removes the configured prefix for an HTTP method from an action name.
    /// </summary>
    /// <param name="actionName">The action name to normalize.</param>
    /// <param name="httpMethod">The resolved HTTP method.</param>
    /// <returns>The action name without a matching conventional prefix.</returns>
    string RemovePrefix(string actionName, string httpMethod);
}

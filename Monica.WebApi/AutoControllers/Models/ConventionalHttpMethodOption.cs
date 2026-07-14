namespace Monica.WebApi.AutoControllers.Models;

/// <summary>
/// Configures how one host infers HTTP methods from AutoController action names.
/// </summary>
public sealed class ConventionalHttpMethodOption
{
    /// <summary>
    /// Gets or sets the HTTP method used when no configured prefix matches an action name.
    /// The default is <c>POST</c>.
    /// </summary>
    public string DefaultHttpMethod { get; set; } = ConventionalHttpMethodDefaults.DefaultHttpMethod;

    /// <summary>
    /// Gets the host-owned mapping from HTTP methods to action-name prefixes.
    /// </summary>
    /// <remarks>
    /// Matching is case-insensitive and favors the longest matching prefix. A prefix may belong
    /// to only one HTTP method. Modify this dictionary only while configuring the host; the
    /// runtime resolver takes an immutable snapshot when endpoint composition resolves it.
    /// </remarks>
    public IDictionary<string, string[]> Prefixes { get; } = ConventionalHttpMethodDefaults.Prefixes
        .ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
}

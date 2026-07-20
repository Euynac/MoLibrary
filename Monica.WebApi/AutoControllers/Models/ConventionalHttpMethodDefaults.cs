using System.Collections.Immutable;

namespace Monica.WebApi.AutoControllers.Models;

/// <summary>
/// Defines immutable defaults for AutoController HTTP method inference.
/// </summary>
public static class ConventionalHttpMethodDefaults
{
    /// <summary>
    /// Gets the HTTP method used when no action-name prefix matches.
    /// </summary>
    public const string DefaultHttpMethod = "POST";

    /// <summary>
    /// Gets the immutable default mapping from HTTP methods to action-name prefixes.
    /// </summary>
    public static ImmutableDictionary<string, ImmutableArray<string>> Prefixes { get; } =
        new Dictionary<string, ImmutableArray<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["GET"] = ["GetList", "GetAll", "Get", "List"],
            ["PUT"] = ["Put", "Update"],
            ["DELETE"] = ["Delete", "Remove"],
            ["POST"] = ["Create", "Add", "Insert", "Post"],
            ["PATCH"] = ["Patch"]
        }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
}

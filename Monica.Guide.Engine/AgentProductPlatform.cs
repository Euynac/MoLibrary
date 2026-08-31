namespace Monica.Guide;

/// <summary>
/// One native platform slice of a product's release contract. A product publishes one
/// bundle per platform; the entry executable name, distribution kind, and archive suffix
/// all resolve through this record instead of a single Windows pin.
/// </summary>
public sealed record AgentProductPlatform
{
    /// <summary>Runtime identifier of the platform bundle, for example <c>win-x64</c>.</summary>
    public required string RuntimeIdentifier { get; init; }

    /// <summary>Distribution kind of the platform bundle, for example <c>portable-win-x64</c>.</summary>
    public required string DistributionKind { get; init; }

    /// <summary>
    /// Executable file name inside the bundle's <c>app/</c> directory; Windows keeps the
    /// <c>.exe</c> suffix while Unix platforms ship an extension-less binary.
    /// </summary>
    public required string ExecutableName { get; init; }

    /// <summary>The runtime identifier of the running process, for example <c>win-x64</c>.</summary>
    public static string CurrentRuntimeIdentifier =>
        System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
}

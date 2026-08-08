namespace Monica.Configuration.Models;

/// <summary>
/// Configures metadata for a JSON file registered through Monica.Configuration.
/// </summary>
public sealed class ManagedJsonConfigurationSourceOptions
{
    /// <summary>
    /// Gets or sets the operator-facing display name. Defaults to the file name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets an optional operator-facing description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this source can be modified from the configuration UI.
    /// </summary>
    public bool IsWritable { get; set; } = true;
}

/// <summary>
/// Captures a JSON source registered through Monica.Configuration.
/// </summary>
public sealed record ManagedJsonConfigurationSourceRegistration
{
    /// <summary>
    /// Gets the configured JSON source path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets whether the source is optional.
    /// </summary>
    public bool Optional { get; init; }

    /// <summary>
    /// Gets whether the runtime configuration source reloads when the file changes.
    /// </summary>
    /// <remarks>Short-lived bootstrap and startup-snapshot providers always disable file watching.</remarks>
    public bool ReloadOnChange { get; init; }

    /// <summary>
    /// Gets the operator-facing display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets an optional operator-facing description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether this source can be modified from the configuration UI.
    /// </summary>
    public bool IsWritable { get; init; }

    internal static ManagedJsonConfigurationSourceRegistration Create(
        string path,
        bool optional,
        bool reloadOnChange,
        Action<ManagedJsonConfigurationSourceOptions>? configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var options = new ManagedJsonConfigurationSourceOptions
        {
            DisplayName = System.IO.Path.GetFileName(path)
        };
        configure?.Invoke(options);

        return new ManagedJsonConfigurationSourceRegistration
        {
            Path = path,
            Optional = optional,
            ReloadOnChange = reloadOnChange,
            DisplayName = string.IsNullOrWhiteSpace(options.DisplayName)
                ? System.IO.Path.GetFileName(path)
                : options.DisplayName,
            Description = options.Description,
            IsWritable = options.IsWritable
        };
    }
}

namespace Monica.Configuration.Stores.File;

/// <summary>
/// Options for the file-backed Monica.Configuration store bundle.
/// </summary>
public sealed class ConfigurationFileStoreOptions
{
    /// <summary>
    /// Gets or sets the root directory used for generated configuration storage files.
    /// </summary>
    public string RootDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "monica-configuration");
}

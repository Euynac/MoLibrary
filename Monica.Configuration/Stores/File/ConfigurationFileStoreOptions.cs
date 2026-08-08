namespace Monica.Configuration.Stores.File;

/// <summary>
/// Options for the file-backed Monica.Configuration store bundle.
/// </summary>
public sealed class ConfigurationFileStoreOptions
{
    /// <summary>
    /// Gets or sets the root directory used for generated configuration storage files.
    /// </summary>
    /// <remarks>
    /// Definition and effective-value files below this directory use identity-addressed filenames. A legacy store
    /// whose files are named from definition keys must be migrated or recreated before it can be opened.
    /// </remarks>
    public string RootDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "monica-configuration");
}

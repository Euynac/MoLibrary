namespace Monica.Guide;

/// <summary>
/// User-local paths of the guide engine itself. The engine root holds the unified ownership
/// ledger, the mutation lock, and engine logs; it is shared by every product the guide
/// installs. Override with <c>MONICA_GUIDE_DATA_ROOT</c>.
/// </summary>
public sealed class GuidePaths
{
    public GuidePaths(string engineDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineDataRoot);
        EngineDataRoot = Path.GetFullPath(engineDataRoot);
    }

    public string EngineDataRoot { get; }

    public string StateDirectory => Path.Combine(EngineDataRoot, "state");

    /// <summary>The unified ownership ledger shared by every installed product.</summary>
    public string GuideLedgerFile => Path.Combine(StateDirectory, "guide.json");

    /// <summary>Mutex serializing guide mutations across every product.</summary>
    public string GuideLockFile => Path.Combine(StateDirectory, "guide.lock");

    /// <summary>Machine-global issue-reporting preference shared by every installed product.</summary>
    public string IssuePreferencesFile => Path.Combine(StateDirectory, "issue-preferences.json");

    public string LogsDirectory => Path.Combine(EngineDataRoot, "logs");

    public static GuidePaths ForCurrentUser(IGuideHostEnvironment? environment = null)
    {
        environment ??= new GuideHostEnvironment();
        var overridden = environment.GetEnvironmentVariable("MONICA_GUIDE_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return new GuidePaths(overridden);
        }

        return new GuidePaths(CombineEngineRoot(environment));
    }

    private static string CombineEngineRoot(IGuideHostEnvironment environment)
        => Path.Combine(UserApplicationDataRoot(environment), "Monica");

    /// <summary>
    /// Per-host user root for Monica data: <c>%LOCALAPPDATA%</c> on Windows, XDG data on
    /// Linux, and <c>~/Library/Application Support</c> on macOS. Products with their own
    /// data-root resolution share it so every Monica root lands in one per-host place.
    /// </summary>
    public static string UserApplicationDataRoot(IGuideHostEnvironment environment)
        => environment.Platform switch
        {
            // macOS keeps user application support under ~/Library/Application Support.
            GuideHostPlatform.MacOS => Path.Combine(
                environment.UserHomeDirectory, "Library", "Application Support"),
            _ => environment.LocalApplicationDataDirectory
        };
}

/// <summary>
/// User-local paths owned by one product. The product root keeps the bootstrap locator,
/// persisted serve configuration, transactions, logs, and the local manifest copy; the
/// ownership ledger itself lives in the engine root.
/// </summary>
public sealed class AgentProductPaths
{
    public AgentProductPaths(string productDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productDataRoot);
        ProductDataRoot = Path.GetFullPath(productDataRoot);
    }

    public string ProductDataRoot { get; }

    public string StateDirectory => Path.Combine(ProductDataRoot, "state");

    public string ConfigurationDirectory => Path.Combine(ProductDataRoot, "configuration");

    public string ServerConfigurationFile => Path.Combine(ConfigurationDirectory, "server.json");

    public string GuidePreferencesFile => Path.Combine(ConfigurationDirectory, "preferences.json");

    public string InstallationLocatorFile => Path.Combine(ProductDataRoot, "installation.json");

    public string TransactionDirectory => Path.Combine(ProductDataRoot, "transactions");

    public string LogsDirectory => Path.Combine(ProductDataRoot, "logs");

    public string ReleaseManifestFile => Path.Combine(ProductDataRoot, "release-manifest.json");

    public static AgentProductPaths ForCurrentUser(
        AgentProductDefinition definition,
        IGuideHostEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        environment ??= new GuideHostEnvironment();
        var overridden = environment.GetEnvironmentVariable(ProductDataRootOverride(definition));
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return new AgentProductPaths(overridden);
        }

        return new AgentProductPaths(Path.Combine(
            GuidePaths.UserApplicationDataRoot(environment),
            definition.ProductDataRootName));
    }

    private static string ProductDataRootOverride(AgentProductDefinition definition)
        => "MONICA_GUIDE_PRODUCT_DATA_ROOT_" +
           definition.ProductDataRootName.Replace(".", "_", StringComparison.Ordinal)
               .Replace("-", "_", StringComparison.Ordinal)
               .ToUpperInvariant();
}

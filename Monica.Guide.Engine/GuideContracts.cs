namespace Monica.Guide;

/// <summary>Schema version of the public guide engine contracts.</summary>
public static class GuideContractVersions
{
    public const int CURRENT = 1;
}

/// <summary>Agent hosts understood by the guide; detection is advisory information only.</summary>
public enum GuideAgent
{
    Codex,
    Claude
}

/// <summary>Skill projection targets the guide can install into one environment.</summary>
public enum GuideTarget
{
    /// <summary>The agent-agnostic shared catalog at <c>~/.agents/skills</c>.</summary>
    Shared,

    /// <summary>Claude Code's discovery projection at <c>~/.claude/skills</c>.</summary>
    Claude,

    /// <summary>
    /// A workspace-local directory configured per project (default <c>.agents/skills</c>).
    /// The exact directory is recorded per installation; native environments only.
    /// </summary>
    Project
}

/// <summary>One selected agent configuration scope.</summary>
public sealed record GuideEnvironment(
    string Selector,
    string Kind,
    string? Distribution = null,
    string? User = null);

/// <summary>Result of one stable guide check.</summary>
public enum GuideCheckStatus
{
    Ok,
    Warning,
    Error
}

/// <summary>Aggregate health of a guide command.</summary>
public enum GuideStatus
{
    Ready,
    Warning,
    Error
}

/// <summary>Kind of deterministic mutation represented by a guide plan action.</summary>
public enum GuidePlanActionKind
{
    /// <summary>Replace one catalog-owned skill directory wholesale from the release bundle.</summary>
    ReplaceSkillDirectory,

    /// <summary>Delete one catalog-owned skill directory wholesale.</summary>
    DeleteSkillDirectory,

    /// <summary>Write one guide-owned local configuration file.</summary>
    WriteFile,

    /// <summary>Delete one guide-owned local configuration file.</summary>
    DeleteFile
}

/// <summary>One machine-stable diagnostic check.</summary>
public sealed record GuideCheck(
    string Id,
    GuideCheckStatus Status,
    string Message,
    string? Remediation = null,
    IReadOnlyDictionary<string, string>? Details = null);

/// <summary>
/// One live progress phase reported while a guide command runs. Keys are stable
/// machine-facing identifiers; Message is an English human-readable detail. Completed
/// and Total support determinate progress surfaces, where zero Total means indeterminate.
/// </summary>
public sealed record GuidePhase(
    string Key,
    string Message,
    int Completed = 0,
    int Total = 0);

/// <summary>Common report returned by overview and status commands.</summary>
public sealed record GuideReport(
    int SchemaVersion,
    string ProductVersion,
    GuideStatus Status,
    IReadOnlyList<GuideCheck> Checks,
    GuideSummary Summary,
    GuidePlan? Plan = null);

/// <summary>Stable human-readable summary in every guide report envelope.</summary>
public sealed record GuideSummary(
    string Command,
    string Message,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<string> NextActions);

/// <summary>Public loopback health payload. It deliberately contains no private filesystem paths.</summary>
public sealed record GuidePublicHealth(
    string ProductVersion,
    string DistributionKind,
    string ReleaseManifestDigest,
    Uri? UiUrl,
    Uri? McpUrl,
    string HealthStatus,
    string? McpToolSchemaDigest);

/// <summary>Authoritative doctor report for an installed product and its localhost runtime.</summary>
public sealed record GuideHealthReport(
    int SchemaVersion,
    string ProductVersion,
    GuideStatus Status,
    IReadOnlyList<GuideCheck> Checks,
    GuideSummary Summary,
    GuidePlan? Plan = null);

/// <summary>
/// One deterministic action in a preview-first configuration plan. Digests carry the existing
/// and expected per-skill verification digest for skill directories, or file digests for
/// guide-owned configuration files; null means absent.
/// </summary>
public sealed record GuidePlanAction(
    string Id,
    GuidePlanActionKind Kind,
    string Target,
    string? BeforeDigest,
    string? AfterDigest,
    string Description);

/// <summary>Preview or result of one configure/unconfigure operation.</summary>
public sealed record GuidePlan(
    int SchemaVersion,
    string Operation,
    string PlanDigest,
    bool Applied,
    bool IsNoOp,
    IReadOnlyList<GuidePlanAction> Actions,
    IReadOnlyList<GuideCheck> Checks);

/// <summary>Targets selected for one environment.</summary>
public sealed record GuideTargetSelection(
    GuideEnvironment Environment,
    IReadOnlyList<GuideTarget> Targets);

/// <summary>One skill installation recorded in the guide state.</summary>
public sealed record GuideInstallationView(
    GuideEnvironment Environment,
    GuideTarget Target,
    string TargetRoot,
    int SkillCount,
    string? Workspace = null);

/// <summary>
/// Desired guide-managed host configuration. Empty selections refresh every installation
/// recorded in the guide state from the selected release bundle. A workspace installs the
/// configured profile's skill closure into the workspace-local target directories instead.
/// </summary>
public sealed record GuideConfigureRequest(
    string? ExecutablePath,
    Uri? BaseAddress,
    IReadOnlyList<GuideTargetSelection> Selections,
    string? ReleaseManifestPath = null,
    string? SkillsRootPath = null,
    string? SkillCatalogPath = null,
    string? Profile = null,
    IReadOnlyList<string>? Skills = null,
    string? Workspace = null);

/// <summary>
/// Selection for removal of guide-managed skill projections. Null targets removes every
/// recorded target in scope; null environments covers every recorded environment; a
/// workspace removes exactly that workspace's recorded installations.
/// </summary>
public sealed record GuideUnconfigureRequest(
    IReadOnlyList<GuideTarget>? Targets = null,
    IReadOnlyList<GuideEnvironment>? Environments = null,
    string? Workspace = null);

/// <summary>One registered workspace observed for a listing surface.</summary>
public sealed record GuideWorkspaceView(
    string Workspace,
    string ProductId,
    string Profile,
    IReadOnlyList<string> Capabilities,
    bool DirectoryExists,
    bool ConfigurationCurrent,
    int InstalledSkillCount,
    int ProfileSkillCount,
    bool InstructionsCurrent,
    IReadOnlyList<string> Issues);

/// <summary>Selectors shared by status and doctor.</summary>
public sealed record GuideInspectRequest(
    IReadOnlyList<GuideEnvironment>? Environments = null);

/// <summary>Stable bootstrap locator stored in the product data root after configuration.</summary>
public sealed record GuideInstallationLocator(
    int SchemaVersion,
    string ProductId,
    string ExecutablePath,
    string BundleRoot,
    string ProductVersion,
    string ReleaseManifestDigest)
{
    /// <summary>The installation.json format; independent of the guide contract version.</summary>
    public const int CurrentSchemaVersion = 2;
}

/// <summary>Public persisted server settings consumed by the product before host startup.</summary>
public sealed record GuideServerConfiguration(
    int SchemaVersion,
    int Port)
{
    /// <summary>The on-disk server.json format; independent of the guide contract version.</summary>
    public const int CurrentSchemaVersion = 1;
}

/// <summary>Program-facing guide command service for one product.</summary>
public interface IAgentGuideService
{
    Task<GuideReport> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<GuideReport> GetStatusAsync(
        GuideInspectRequest? request = null,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default);

    Task<GuideHealthReport> DiagnoseAsync(
        GuideInspectRequest? request = null,
        Uri? baseAddress = null,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Probes only the live loopback runtime (healthz, UI, MCP handshake, tool catalog, and
    /// one read-only call) without re-inspecting host configuration. Setup surfaces use this
    /// for a focused functional test.
    /// </summary>
    Task<GuideHealthReport> ProbeRuntimeAsync(
        Uri? baseAddress = null,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default);

    Task<GuideReport> PreviewConfigureAsync(
        GuideConfigureRequest request,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default);

    Task<GuideReport> ApplyConfigureAsync(
        GuideConfigureRequest request,
        string expectedPlanDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default);

    Task<GuideReport> PreviewUnconfigureAsync(
        GuideUnconfigureRequest request,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default);

    Task<GuideReport> ApplyUnconfigureAsync(
        GuideUnconfigureRequest request,
        string expectedPlanDigest,
        IProgress<GuidePhase>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lists the installations recorded in the guide state, ordered for display.</summary>
    IReadOnlyList<GuideInstallationView> ListRecordedInstallations();
}

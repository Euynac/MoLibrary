namespace Monica.Guide;

/// <summary>One immutable file in a release asset.</summary>
public sealed record ReleaseFile(
    string RelativePath,
    long Length,
    string Sha256);

/// <summary>One skill shipped beside the program.</summary>
public sealed record ReleaseSkill(
    string Name,
    string RelativePath,
    string? Sha256 = null,
    string? Role = null,
    string? TreeDigest = null,
    IReadOnlyList<string>? Files = null,
    IReadOnlyList<string>? Dependencies = null,
    IReadOnlyList<string>? Profiles = null);

/// <summary>
/// Co-versioned release contract. A release bundle contains an optional runnable program and a
/// separately discoverable skills payload from the same immutable release; the manifest is the
/// self-describing extension contract any guide host can install.
/// </summary>
public sealed record ReleaseManifest
{
    /// <summary>The release-manifest.json format; independent of the guide contract version.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; }
    public string ProductId { get; init; } = string.Empty;
    public string? ProductName { get; init; }
    public string ProductVersion { get; init; } = string.Empty;
    /// <summary>The actual four-part CLR AssemblyName.Version; it is not a SemVer alias.</summary>
    public string AssemblyVersion { get; init; } = string.Empty;
    /// <summary>The MCP surface version; must equal the product version when present.</summary>
    public string? McpVersion { get; init; }
    public string? Tag { get; init; }
    public string SourceCommit { get; init; } = string.Empty;
    public string? FrameworkCommit { get; init; }
    public string DotnetSdk { get; init; } = string.Empty;
    public string RuntimeIdentifier { get; init; } = string.Empty;
    public string DistributionKind { get; init; } = string.Empty;
    public bool? SelfContained { get; init; }
    /// <summary>Shared ASP.NET Core runtime major.minor a framework-dependent release requires; null for self-contained legacy releases.</summary>
    public string? RequiredRuntime { get; init; }
    public bool? Trimmed { get; init; }
    public bool? PublishSingleFile { get; init; }
    public string? ProgramEntryPoint { get; init; }
    public IReadOnlyList<ReleaseFile> ProgramFiles { get; init; } = [];
    public IReadOnlyList<string> SupportedHosts { get; init; } = [];
    public IReadOnlyList<string> SupportedRoutes { get; init; } = [];
    public IReadOnlyDictionary<string, string> DefaultRoutes { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string> DefaultJourneys { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
    public string? McpServerName { get; init; }
    public string? McpPath { get; init; }
    /// <summary>Read-only MCP tool the doctor onboarding probe calls; required with MCP.</summary>
    public string? DoctorReadOnlyTool { get; init; }
    public IReadOnlyList<ReleaseFile> Files { get; init; } = [];
    public IReadOnlyList<ReleaseSkill> Skills { get; init; } = [];
    public ReleaseSkillsAsset? SkillsAsset { get; init; }
    public string McpToolSchemaDigest { get; init; } = string.Empty;
    public string? SkillCatalogDigest { get; init; }
}

/// <summary>Legacy skill-catalog projection carried by self-contained releases; absent from current single-archive manifests.</summary>
public sealed record ReleaseSkillsAsset(
    string AssetName,
    string CatalogPath,
    string CatalogSha256,
    string TreeDigest,
    int SkillCount,
    IReadOnlyList<ReleaseSkill> Skills);

/// <summary>Exact packaged skill catalog. Installers must copy only the listed files.</summary>
public sealed record SkillCatalog(
    int SchemaVersion,
    int SkillCount,
    string TreeDigest,
    IReadOnlyList<SkillCatalogEntry> Skills,
    GuideManagedInstructions? ManagedInstructions = null,
    IReadOnlyDictionary<string, GuideSourceRepositoryDefinition>? SourceRepositories = null,
    IReadOnlyDictionary<string, GuideSkillAlias>? Aliases = null);

/// <summary>One exact skill tree in the packaged catalog.</summary>
public sealed record SkillCatalogEntry(
    string Name,
    string Path,
    string Role,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Dependencies,
    string TreeDigest,
    IReadOnlyList<string>? Profiles = null);

/// <summary>
/// Managed workspace instruction contract projected from the canonical catalog. The guide
/// engine renders one marked block inside a workspace AGENTS.md from these templates.
/// </summary>
public sealed record GuideManagedInstructions(
    int Version,
    GuideInstructionMarkers Markers,
    IReadOnlyDictionary<string, GuideInstructionTemplate> Templates);

/// <summary>HTML comment markers bounding the guide-owned instruction span.</summary>
public sealed record GuideInstructionMarkers(
    string Start,
    string End);

/// <summary>Per-profile instruction template: routed skills plus routing rules.</summary>
public sealed record GuideInstructionTemplate(
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Rules);

/// <summary>One first-party source repository a workspace or skill set can bind globally.</summary>
public sealed record GuideSourceRepositoryDefinition(
    string Repository,
    IReadOnlyList<string> Aliases);

/// <summary>One retired skill name and the canonical skill that replaced it.</summary>
public sealed record GuideSkillAlias(
    string Canonical,
    string Diagnostic);

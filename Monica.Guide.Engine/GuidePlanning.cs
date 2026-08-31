using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Guide;

internal static class GuidePlanning
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) }
    };

    internal static string DigestPlan(
        string operation,
        IReadOnlyList<GuidePlanAction> actions,
        IReadOnlyList<GuideCheck> checks)
    {
        var payload = new
        {
            schemaVersion = GuideContractVersions.CURRENT,
            operation,
            actions = actions.Select(static action => new
            {
                action.Id,
                kind = action.Kind.ToString(),
                action.Target,
                action.BeforeDigest,
                action.AfterDigest,
                action.Description
            }).ToArray(),
            checks = checks.Select(static check => new
            {
                check.Id,
                status = check.Status.ToString(),
                check.Message,
                check.Remediation
            }).ToArray()
        };
        return Sha256(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
    }

    internal static string Sha256(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static string? DigestFile(string path)
        => File.Exists(path) ? Sha256(File.ReadAllBytes(path)) : null;

    internal static byte[] JsonBytes<T>(T value)
        => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);

    internal static byte[] Utf8(string value)
        => Encoding.UTF8.GetBytes(value);
}

/// <summary>
/// The unified guide ownership ledger for one user: one product state per installed product.
/// It lives in the guide engine data root, not in any product's own data directory, because
/// the ledger's concern is "which product owns which skill directories" across products.
/// Schema 2 keys installations by environment and normalized target root, so project-local
/// directories coexist with the shared global targets.
/// </summary>
internal sealed record GuideLedger(
    int SchemaVersion,
    IReadOnlyList<ProductGuideState> Products)
{
    public const int CurrentSchemaVersion = 2;
}

/// <summary>Persisted guide ownership state of one product; installed trees verify by digest.</summary>
internal sealed record ProductGuideState(
    string ProductId,
    string ProductVersion,
    string ExecutablePath,
    string BundleRoot,
    string ReleaseManifestDigest,
    Uri? BaseAddress,
    IReadOnlyList<GuideSkillInstallation> Installations);

/// <summary>
/// One installed skill projection. Identity is the environment plus the normalized target
/// root; WorkspaceRoot is set exactly for project-local installations and anchors the
/// workspace-scoped configure, unconfigure, and forget flows.
/// </summary>
internal sealed record GuideSkillInstallation(
    GuideEnvironment Environment,
    GuideTarget Target,
    string TargetRoot,
    string? WorkspaceRoot,
    IReadOnlyList<GuideSkillTree> Trees);

/// <summary>One catalog-owned skill directory and its exact tree digest.</summary>
internal sealed record GuideSkillTree(
    string Name,
    string TreeDigest);

/// <summary>Base for one planned mutation; derived records carry their execution payload.</summary>
internal abstract record GuidePlannedMutation(GuidePlanAction PublicAction);

/// <summary>Replace one catalog skill directory: stage the new files, then swap directories.</summary>
internal sealed record GuideSkillReplace(
    GuidePlanAction PublicAction,
    GuideEnvironment Environment,
    string TargetRoot,
    string SkillName,
    string StagingRoot,
    IReadOnlyList<GuideCatalogFile> Files) : GuidePlannedMutation(PublicAction);

/// <summary>Delete the catalog skill directories of one installed projection wholesale.</summary>
internal sealed record GuideSkillRemove(
    GuidePlanAction PublicAction,
    GuideEnvironment Environment,
    string TargetRoot,
    IReadOnlyList<string> SkillNames) : GuidePlannedMutation(PublicAction);

/// <summary>
/// Delete whole skill directories below one native project target root; used by the
/// workspace forget flow, which never goes through the environment runtime.
/// </summary>
internal sealed record GuideProjectTreeRemove(
    GuidePlanAction PublicAction,
    string TargetRoot,
    IReadOnlyList<string> SkillNames) : GuidePlannedMutation(PublicAction);

/// <summary>Remove one guide-owned directory when it is empty after file removals.</summary>
internal sealed record GuideDirectoryCleanup(
    GuidePlanAction PublicAction,
    string Path) : GuidePlannedMutation(PublicAction);

/// <summary>Write or delete (null content) one guide-owned local configuration file.</summary>
internal sealed record GuideLocalFileMutation(
    GuidePlanAction PublicAction,
    string Path,
    byte[]? Content) : GuidePlannedMutation(PublicAction);

internal sealed record GuideCatalogFile(
    string RelativePath,
    byte[] Content);

internal sealed record GuidePreparedPlan(
    GuidePlan PublicPlan,
    IReadOnlyList<GuidePlannedMutation> Mutations,
    ProductGuideState? NextState);

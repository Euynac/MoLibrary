using System.Collections.Frozen;
using System.Text.Encodings.Web;
using System.Text.Json;
using Monica.AI.AgentCapabilities.Models;
using Monica.AI.Skills.Services;
using Monica.Core.Skills;
using Monica.Core.Skills.Annotations;
using Monica.Core.Skills.Models;
using Monica.Modules;

namespace Monica.AI.Skills;

/// <summary>
/// Provides read-only local file inspection tools for Monica agents.
/// </summary>
internal sealed class ReadOnlyFileAccessSkill(ReadOnlyFileAccessService fileAccess)
    : Skill<ReadOnlyFileAccessSkill>
{
    private static readonly JsonSerializerOptions _toolJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <inheritdoc />
    public override SkillDefinition Definition { get; } = new(
        "read-only-file-access",
        "Inspect configured local filesystem roots with bounded read, file listing, and ripgrep search tools.",
        "Use these scripts when the user asks Monica to inspect local project files, search source text, find files by glob, " +
        "or read exact file ranges. First call list-file-roots to discover the configured root names. Always pass one " +
        "of those root names and a relative path inside that root; absolute paths and path escapes are rejected. Prefer " +
        "search-files or list-files before read-file when the target file is unknown. Returned content is bounded, so " +
        "continue with the returned NextOffset or NextStartLine when HasMore is true. Tool calls must always pass a JSON " +
        "object with named arguments; for example pass ignoreCase as a boolean property inside the search-files object.");

    /// <inheritdoc />
    public override IReadOnlySet<Type> RequiredModules { get; } =
        new[] { typeof(ModuleSkillSystem) }.ToFrozenSet();

    /// <inheritdoc />
    public override bool IsEnabled => fileAccess.HasRoots;

    /// <inheritdoc />
    public override string? DisabledReason => fileAccess.HasRoots
        ? null
        : SkillCapabilityMessageCode.DisabledReason.ReadOnlyFileAccessNoRoots;

    /// <summary>
    /// Lists configured filesystem roots available to this read-only skill.
    /// </summary>
    /// <returns>Serialized root names, descriptions, resolved paths, and existence status.</returns>
    [SkillTool(
        Name = "list-file-roots",
        Description = "List configured read-only filesystem roots. Use a returned root name in the other file tools.")]
    public string ListFileRoots()
        => JsonSerializer.Serialize(fileAccess.ListRoots(), _toolJsonOptions);

    /// <summary>
    /// Reads a bounded line window from a text file inside a configured root.
    /// </summary>
    /// <param name="rootName">Root name returned by <c>list-file-roots</c>.</param>
    /// <param name="path">Relative file path inside the root. Absolute paths and path escapes are rejected.</param>
    /// <param name="startLine">One-based line number where reading should start. Defaults to the first line.</param>
    /// <param name="maxLines">Maximum lines to return, clamped by module configuration.</param>
    /// <param name="maxTokens">Optional token budget for returned content, clamped by module configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized file content window and continuation metadata.</returns>
    [SkillTool(
        Name = "read-file",
        Description = "Read a bounded line window from a text file inside a configured root.")]
    public async Task<string> ReadFileAsync(
        string rootName,
        string path,
        int startLine = 1,
        int maxLines = 160,
        int? maxTokens = null,
        CancellationToken ct = default)
    {
        var result = await fileAccess.ReadFileAsync(rootName, path, startLine, maxLines, maxTokens, ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    /// <summary>
    /// Searches file content inside a configured root using ripgrep.
    /// </summary>
    /// <param name="rootName">Root name returned by <c>list-file-roots</c>.</param>
    /// <param name="pattern">Regular expression or literal pattern accepted by ripgrep.</param>
    /// <param name="directoryPath">Optional relative directory inside the root to search. Defaults to the root.</param>
    /// <param name="glob">Optional ripgrep glob filter such as <c>*.cs</c> or <c>**/*.razor</c>.</param>
    /// <param name="ignoreCase">Whether matching should ignore case.</param>
    /// <param name="contextLines">Context lines before and after matches, clamped by module configuration.</param>
    /// <param name="offset">Number of returned match/context rows to skip for continuation.</param>
    /// <param name="maxResults">Maximum rows to return, clamped by module configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized ripgrep match and context rows.</returns>
    [SkillTool(
        Name = "search-files",
        Description = "Search file content in a configured root using ripgrep. Pass a JSON object with rootName and pattern; optional ignoreCase is a boolean, contextLines is an integer, and nextOffset continues pagination.")]
    public async Task<string> SearchFilesAsync(
        string rootName,
        string pattern,
        string? directoryPath = null,
        string? glob = null,
        bool ignoreCase = false,
        int contextLines = 0,
        int offset = 0,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        var result = await fileAccess.SearchFilesAsync(
            rootName,
            pattern,
            directoryPath,
            glob,
            ignoreCase,
            contextLines,
            offset,
            maxResults,
            ct);

        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    /// <summary>
    /// Lists files inside a configured root using ripgrep file discovery.
    /// </summary>
    /// <param name="rootName">Root name returned by <c>list-file-roots</c>.</param>
    /// <param name="directoryPath">Optional relative directory inside the root to list. Defaults to the root.</param>
    /// <param name="glob">Optional ripgrep glob filter such as <c>*.cs</c> or <c>**/*.razor</c>.</param>
    /// <param name="offset">Number of paths to skip for continuation.</param>
    /// <param name="maxResults">Maximum paths to return, clamped by module configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Serialized relative file paths.</returns>
    [SkillTool(
        Name = "list-files",
        Description = "List files in a configured root using ripgrep file discovery and optional glob filtering. Use nextOffset when hasMore is true.")]
    public async Task<string> ListFilesAsync(
        string rootName,
        string? directoryPath = null,
        string? glob = null,
        int offset = 0,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        var result = await fileAccess.ListFilesAsync(rootName, directoryPath, glob, offset, maxResults, ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }
}

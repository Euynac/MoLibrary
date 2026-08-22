namespace Monica.AI.Mcp.Models;

/// <summary>
/// A rendered agent skill pack that exposes one Monica-hosted HTTP MCP server over plain HTTP
/// helper scripts, so agent hosts can invoke its tools without registering an MCP client.
/// </summary>
public sealed record McpSkillPack
{
    /// <summary>
    /// Creates a rendered skill pack.
    /// </summary>
    public McpSkillPack(
        string skillName,
        string serverName,
        string serverTitle,
        string serverVersion,
        string endpointUrl,
        string endpointEnvironmentVariable,
        string toolSchemaDigest,
        int toolCount,
        IReadOnlyList<McpSkillPackFile> files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointEnvironmentVariable);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolSchemaDigest);
        ArgumentNullException.ThrowIfNull(files);
        if (toolCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toolCount));
        }

        SkillName = skillName;
        ServerName = serverName;
        ServerTitle = serverTitle;
        ServerVersion = serverVersion;
        EndpointUrl = endpointUrl;
        EndpointEnvironmentVariable = endpointEnvironmentVariable;
        ToolSchemaDigest = toolSchemaDigest;
        ToolCount = toolCount;
        Files = files;
    }

    /// <summary>
    /// Skill directory name, also the <c>name</c> in the generated <c>SKILL.md</c> frontmatter.
    /// </summary>
    public string SkillName { get; }

    /// <summary>
    /// Logical MCP server name the pack was rendered from.
    /// </summary>
    public string ServerName { get; }

    /// <summary>
    /// Display title of the MCP server used in the generated documentation headings.
    /// </summary>
    public string ServerTitle { get; }

    /// <summary>
    /// Server version the pack was rendered from; part of the pack's drift identity.
    /// </summary>
    public string ServerVersion { get; }

    /// <summary>
    /// Absolute HTTP endpoint the bundled helper scripts call by default.
    /// </summary>
    public string EndpointUrl { get; }

    /// <summary>
    /// Environment variable name that overrides <see cref="EndpointUrl"/> at invocation time.
    /// </summary>
    public string EndpointEnvironmentVariable { get; }

    /// <summary>
    /// Canonical tool schema digest of the rendered catalog, in the <c>sha256:&lt;hex&gt;</c> form.
    /// </summary>
    public string ToolSchemaDigest { get; }

    /// <summary>
    /// Number of tools documented by the pack.
    /// </summary>
    public int ToolCount { get; }

    /// <summary>
    /// Rendered files, ordered deterministically.
    /// </summary>
    public IReadOnlyList<McpSkillPackFile> Files { get; }
}

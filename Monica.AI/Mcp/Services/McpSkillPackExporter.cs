using System.Text;
using System.Text.Json;
using Monica.AI.AgentCapabilities.Services;
using Monica.AI.Mcp.Internal;
using Monica.AI.Mcp.Models;

namespace Monica.AI.Mcp.Services;

/// <summary>
/// Renders and writes deterministic agent skill packs that expose one Monica-hosted HTTP MCP server
/// through plain HTTP helper scripts instead of a registered MCP client.
/// </summary>
/// <remarks>
/// A pack contains a <c>SKILL.md</c> invocation contract, a full tool reference with input schemas, and
/// environment-agnostic helper scripts (bash and PowerShell) that call the server's stateless Streamable
/// HTTP endpoint. Rendering is deterministic for one server version and tool catalog, so callers can pin
/// releases by the embedded <see cref="McpToolSchemaDigest"/> value.
/// </remarks>
internal sealed class McpSkillPackExporter
{
    private const string SKILL_FILE = "SKILL.md";
    private const string TOOL_REFERENCE_FILE = "references/tools.md";
    private const string BASH_SCRIPT_FILE = "scripts/mcp-call.sh";
    private const string POWERSHELL_SCRIPT_FILE = "scripts/mcp-call.ps1";

    private readonly MonicaMcpCatalog catalog;

    /// <summary>
    /// Creates the skill pack exporter bound to the local MCP server catalog.
    /// </summary>
    public McpSkillPackExporter(MonicaMcpCatalog catalog)
    {
        this.catalog = catalog;
    }

    /// <summary>
    /// Renders the skill pack for one local HTTP MCP server.
    /// </summary>
    /// <param name="serverName">Logical MCP server name in the local catalog.</param>
    /// <param name="endpointUrl">Absolute HTTP endpoint the helper scripts call by default.</param>
    /// <returns>The rendered pack with deterministic file contents.</returns>
    /// <exception cref="KeyNotFoundException">No local MCP server with that name exists.</exception>
    /// <exception cref="InvalidOperationException">The server does not use HTTP transport.</exception>
    public McpSkillPack Render(string serverName, Uri endpointUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ArgumentNullException.ThrowIfNull(endpointUrl);
        if (!endpointUrl.IsAbsoluteUri || endpointUrl.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException($"Endpoint URL '{endpointUrl}' must be an absolute HTTP or HTTPS URL.");
        }

        var entry = catalog.FindLocalServer(serverName)
            ?? throw new KeyNotFoundException($"MCP server '{serverName}' was not found in the local catalog.");
        if (entry.TransportKind != McpServerTransportKind.Http)
        {
            throw new InvalidOperationException(
                $"MCP server '{serverName}' does not use HTTP transport and cannot be exported as an agent skill pack.");
        }

        var tools = entry.Tools
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .ToList();
        var digest = McpToolSchemaDigest.Compute(
            tools.Select(tool => JsonSerializer.SerializeToElement(
                    tool.SdkTool.ProtocolTool,
                    ModelContextProtocol.McpJsonUtilities.DefaultOptions))
                .ToList());

        var skillName = DeriveSkillName(entry.Definition.Name);
        return new McpSkillPack(
            skillName,
            entry.Definition.Name,
            string.IsNullOrWhiteSpace(entry.Definition.Title) ? entry.Definition.Name : entry.Definition.Title,
            entry.Definition.Version,
            endpointUrl.AbsoluteUri,
            DeriveEndpointEnvironmentVariable(entry.Definition.Name),
            digest,
            tools.Count,
        [
            new McpSkillPackFile(SKILL_FILE, RenderSkillMarkdown(entry, tools, endpointUrl, digest)),
            new McpSkillPackFile(TOOL_REFERENCE_FILE, RenderToolReference(entry, tools, digest)),
            new McpSkillPackFile(BASH_SCRIPT_FILE, RenderBashScript(entry, endpointUrl)),
            new McpSkillPackFile(POWERSHELL_SCRIPT_FILE, RenderPowerShellScript(entry, endpointUrl))
        ]);
    }

    /// <summary>
    /// Writes a rendered pack below one target skills directory, refusing to overwrite existing content.
    /// </summary>
    /// <param name="pack">The rendered pack.</param>
    /// <param name="targetRoot">Absolute directory that receives <c>targetRoot/skillName</c>.</param>
    /// <returns>The export result with absolute written paths.</returns>
    /// <exception cref="InvalidOperationException">The target skill directory already exists.</exception>
    public McpSkillPackExportResult WriteToDirectory(McpSkillPack pack, string targetRoot)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);

        var skillDirectoryPath = Path.Combine(Path.GetFullPath(targetRoot), pack.SkillName);
        if (Directory.Exists(skillDirectoryPath))
        {
            throw new InvalidOperationException(
                $"Skill directory '{skillDirectoryPath}' already exists. Remove it and export again.");
        }

        if (File.Exists(skillDirectoryPath))
        {
            throw new InvalidOperationException(
                $"Skill target '{skillDirectoryPath}' exists and is a file.");
        }

        var writtenFiles = new List<string>();
        try
        {
            foreach (var file in pack.Files)
            {
                var path = Path.Combine(skillDirectoryPath, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (!OperatingSystem.IsWindows() && file.RelativePath.EndsWith(".sh", StringComparison.Ordinal))
                {
                    File.SetUnixFileMode(
                        path,
                        File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }

                writtenFiles.Add(path);
            }
        }
        catch
        {
            try
            {
                Directory.Delete(skillDirectoryPath, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; the original failure is the actionable error.
            }

            throw;
        }

        return new McpSkillPackExportResult(pack, skillDirectoryPath, writtenFiles);
    }

    /// <summary>
    /// Derives the skill directory name for one MCP server name.
    /// </summary>
    internal static string DeriveSkillName(string serverName)
    {
        return $"{serverName.Trim()}-operations";
    }

    /// <summary>
    /// Derives the endpoint override environment variable for one MCP server name, for example
    /// <c>monica-workflow</c> becomes <c>MONICA_WORKFLOW_MCP_URL</c>.
    /// </summary>
    internal static string DeriveEndpointEnvironmentVariable(string serverName)
    {
        var builder = new StringBuilder(serverName.Trim().ToUpperInvariant())
            .Replace('-', '_')
            .Replace(' ', '_');
        return builder.Append("_MCP_URL").ToString();
    }

    private static string RenderSkillMarkdown(
        LocalMcpServerEntry entry,
        IReadOnlyList<McpServerToolDescriptor> tools,
        Uri endpointUrl,
        string digest)
    {
        var envVar = DeriveEndpointEnvironmentVariable(entry.Definition.Name);
        var serverName = entry.Definition.Name;
        var serverTitle = string.IsNullOrWhiteSpace(entry.Definition.Title) ? serverName : entry.Definition.Title;
        var digestShort = digest["sha256:".Length..("sha256:".Length + 12)];

        var builder = new StringBuilder()
            .Append("---\n")
            .Append("name: ").Append(DeriveSkillName(serverName)).Append('\n')
            .Append("description: Invoke the ").Append(serverName)
            .Append(" MCP machine API (").Append(tools.Count).Append(" tools, schema digest ")
            .Append(digestShort).Append(") over plain HTTP through bundled helper scripts, without registering an MCP client.\n")
            .Append("---\n\n")
            .Append("# ").Append(serverTitle).Append(" operations\n\n")
            .Append("Generated invocation surface for the `").Append(serverName).Append("` MCP server, version ")
            .Append(entry.Definition.Version)
            .Append(". It lets an agent host call every advertised tool over plain HTTP instead of holding a registered MCP client connection.\n\n")
            .Append("## Invocation\n\n")
            .Append("- Endpoint: `").Append(endpointUrl.AbsoluteUri).Append("`. Override with the `")
            .Append(envVar).Append("` environment variable when the server moves.\n")
            .Append("- Bash (Linux, macOS, WSL, Git Bash), from this skill's directory: `")
            .Append(BASH_SCRIPT_FILE).Append(" <tool-name> '<json-arguments>'`.\n")
            .Append("- PowerShell (Windows), from this skill's directory: `pwsh -File ")
            .Append(POWERSHELL_SCRIPT_FILE).Append(" <tool-name> '<json-arguments>'` (`powershell -File` on Windows PowerShell 5.1).\n")
            .Append("- `<json-arguments>` is the tool's input object, for example `{\"projectPath\":\"D:\\\\Code\\\\Demo\"}`; use `{}` for tools without parameters.\n")
            .Append("- The helper prints the raw JSON-RPC response. A top-level `error` object means the call failed; read `error.message` before retrying.\n")
            .Append("- The server speaks stateless Streamable HTTP; the helper sets the required headers and strips the SSE framing.\n")
            .Append("- On Linux and macOS, make the bash helper executable once with `chmod +x ").Append(BASH_SCRIPT_FILE).Append("`.\n\n");

        if (!string.IsNullOrWhiteSpace(entry.Definition.Instructions))
        {
            builder
                .Append("## Server instructions\n\n> ")
                .Append(entry.Definition.Instructions.Trim().Replace("\n", "\n> "))
                .Append("\n\n");
        }

        builder
            .Append("## Tool catalog\n\n")
            .Append(tools.Count).Append(" tools, sorted by name. One-line index below; full descriptions and input schemas in `")
            .Append(TOOL_REFERENCE_FILE).Append("`.\n\n");
        foreach (var tool in tools)
        {
            builder
                .Append("- `").Append(tool.Name).Append("` — ")
                .Append(ToSingleLine(tool.Description))
                .Append('\n');
        }

        builder
            .Append('\n')
            .Append("## Drift\n\n")
            .Append("This pack was rendered from server version ").Append(entry.Definition.Version)
            .Append(" with tool schema digest `").Append(digest)
            .Append("`. After a server upgrade, re-export the pack; calls to renamed or reshaped tools return JSON-RPC errors.\n");
        return builder.ToString();
    }

    private static string RenderToolReference(
        LocalMcpServerEntry entry,
        IReadOnlyList<McpServerToolDescriptor> tools,
        string digest)
    {
        var builder = new StringBuilder()
            .Append("# ").Append(entry.Definition.Name).Append(" tool reference\n\n")
            .Append("Server version ").Append(entry.Definition.Version).Append("; tool schema digest `")
            .Append(digest).Append("`. Tools are sorted by name. Each block shows the tool's MCP input schema.\n");

        foreach (var tool in tools)
        {
            builder
                .Append("\n## `").Append(tool.Name).Append("`\n\n")
                .Append(ToSingleLine(tool.Description))
                .Append('\n');

            var schema = AgentCapabilitySchemaParser.FormatSchema(tool.SdkTool.ProtocolTool.InputSchema);
            if (!string.IsNullOrWhiteSpace(schema))
            {
                builder
                    .Append("\nInput schema:\n\n```json\n")
                    .Append(schema.Trim())
                    .Append("\n```\n");
            }
        }

        return builder.ToString();
    }

    private static string RenderBashScript(LocalMcpServerEntry entry, Uri endpointUrl)
    {
        return BASH_SCRIPT_TEMPLATE
            .Replace("__SERVER_NAME__", entry.Definition.Name)
            .Replace("__ENVVAR__", DeriveEndpointEnvironmentVariable(entry.Definition.Name))
            .Replace("__ENDPOINT__", endpointUrl.AbsoluteUri);
    }

    private static string RenderPowerShellScript(LocalMcpServerEntry entry, Uri endpointUrl)
    {
        return POWERSHELL_SCRIPT_TEMPLATE
            .Replace("__SERVER_NAME__", entry.Definition.Name)
            .Replace("__ENVVAR__", DeriveEndpointEnvironmentVariable(entry.Definition.Name))
            .Replace("__ENDPOINT__", endpointUrl.AbsoluteUri);
    }

    private static string ToSingleLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(no description)";
        }

        return string.Join(
            ' ',
            text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private const string BASH_SCRIPT_TEMPLATE = """
        #!/usr/bin/env bash
        # Calls one tool on the __SERVER_NAME__ MCP machine API.
        # Usage: mcp-call.sh <tool-name> ['{"parameter":"value"}']
        set -euo pipefail

        endpoint="${__ENVVAR__:-__ENDPOINT__}"
        tool="${1:-}"
        arguments="${2:-}"
        if [ -z "$arguments" ]; then
          arguments='{}'
        fi

        if [ -z "$tool" ]; then
          echo "usage: mcp-call.sh <tool-name> [json-arguments]" >&2
          exit 2
        fi

        request='{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"'"$tool"'","arguments":'"$arguments"'}}'

        raw="$(curl -sS -X POST "$endpoint" \
          -H 'Content-Type: application/json' \
          -H 'Accept: application/json, text/event-stream' \
          -H 'User-Agent: monica-mcp-skill/1.0' \
          --data "$request" \
          -w '\n%{http_code}')"

        http_code="${raw##*$'\n'}"
        payload="${raw%$'\n'*}"

        if [ "$http_code" != "200" ]; then
          echo "HTTP $http_code from $endpoint" >&2
          printf '%s\n' "$payload" >&2
          exit 1
        fi

        # The endpoint answers with SSE framing; keep only the JSON message lines.
        printf '%s\n' "$payload" | tr -d '\r' | sed -n 's/^data://p' | sed 's/^ //'
        """;

    private const string POWERSHELL_SCRIPT_TEMPLATE = """
        # Calls one tool on the __SERVER_NAME__ MCP machine API.
        # Usage: mcp-call.ps1 <tool-name> ['{"parameter":"value"}']
        param(
            [string]$Tool,
            [string]$Arguments
        )

        if ([string]::IsNullOrWhiteSpace($Tool)) {
            Write-Error 'usage: mcp-call.ps1 <tool-name> [json-arguments]'
            exit 2
        }
        if ([string]::IsNullOrWhiteSpace($Arguments)) {
            $Arguments = '{}'
        }

        $endpoint = if ($env:__ENVVAR__) { $env:__ENVVAR__ } else { '__ENDPOINT__' }

        try {
            $argumentsJson = $Arguments | ConvertFrom-Json
        } catch {
            Write-Error "Arguments are not valid JSON: $Arguments"
            exit 2
        }

        $request = @{
            jsonrpc = '2.0'
            id = 1
            method = 'tools/call'
            params = @{
                name = $Tool
                arguments = $argumentsJson
            }
        } | ConvertTo-Json -Depth 64 -Compress

        try {
            $response = Invoke-WebRequest -Uri $endpoint -Method Post -Body $request -ContentType 'application/json' `
                -Headers @{ Accept = 'application/json, text/event-stream' } `
                -UserAgent 'monica-mcp-skill/1.0' `
                -UseBasicParsing -TimeoutSec 300
        } catch {
            Write-Error "HTTP request to $endpoint failed: $($_.Exception.Message)"
            exit 1
        }

        # The endpoint answers with SSE framing; keep only the JSON message lines.
        $lines = ([string]$response.Content) -split "`r?`n" | Where-Object { $_ -like 'data:*' }
        ($lines | ForEach-Object { $_.Substring(5).TrimStart(' ') }) -join "`n"
        """;
}

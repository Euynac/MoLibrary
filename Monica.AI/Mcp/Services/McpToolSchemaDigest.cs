using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Monica.AI.Mcp.Services;

/// <summary>
/// Computes the canonical SHA-256 digest over the tool schemas an MCP server advertises.
/// </summary>
/// <remarks>
/// <para>
/// The digest is the shared identity of an MCP tool catalog. It normalizes each tool to the whitelisted
/// protocol properties (<c>annotations</c>, <c>description</c>, <c>execution</c>, <c>inputSchema</c>,
/// <c>name</c>, <c>outputSchema</c>, <c>title</c>), sorts tools and object properties by ordinal name,
/// and hashes the canonical JSON as lowercase hex prefixed with <c>sha256:</c>.
/// </para>
/// <para>
/// The same algorithm accepts raw <c>tools/list</c> payload elements and in-process SDK tool definitions,
/// so live probes, health reports, and generated agent artifacts can be compared byte-for-byte.
/// </para>
/// </remarks>
public static class McpToolSchemaDigest
{
    private static readonly string[] CANONICAL_TOOL_PROPERTIES =
        ["annotations", "description", "execution", "inputSchema", "name", "outputSchema", "title"];

    /// <summary>
    /// Computes the canonical digest from raw MCP <c>tools/list</c> tool elements.
    /// </summary>
    /// <param name="tools">Tool objects as returned by an MCP <c>tools/list</c> response.</param>
    /// <returns>The digest in the <c>sha256:&lt;hex&gt;</c> form.</returns>
    /// <exception cref="InvalidDataException">A tool is missing its <c>name</c> or the list contains duplicate names.</exception>
    public static string Compute(IReadOnlyList<JsonElement> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var names = new HashSet<string>(StringComparer.Ordinal);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartArray();
            foreach (var tool in tools.OrderBy(static tool => RequiredName(tool), StringComparer.Ordinal))
            {
                var name = RequiredName(tool);
                if (!names.Add(name))
                {
                    throw new InvalidDataException($"MCP tools/list returned duplicate tool '{name}'.");
                }

                writer.WriteStartObject();
                foreach (var propertyName in CANONICAL_TOOL_PROPERTIES)
                {
                    if (!tool.TryGetProperty(propertyName, out var property)
                        || property.ValueKind == JsonValueKind.Undefined)
                    {
                        continue;
                    }

                    writer.WritePropertyName(propertyName);
                    WriteCanonicalJson(writer, property);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return $"sha256:{Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant()}";
    }

    /// <summary>
    /// Computes the canonical digest from in-process MCP SDK tool definitions serialized with the SDK's
    /// protocol options, producing the same value a live <c>tools/list</c> probe would observe.
    /// </summary>
    /// <param name="tools">Protocol tool definitions owned by a local MCP server.</param>
    /// <returns>The digest in the <c>sha256:&lt;hex&gt;</c> form.</returns>
    public static string Compute(IReadOnlyList<ModelContextProtocol.Protocol.Tool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        var elements = tools
            .Select(tool => JsonSerializer.SerializeToElement(tool, ModelContextProtocol.McpJsonUtilities.DefaultOptions))
            .ToList();
        return Compute(elements);
    }

    private static string RequiredName(JsonElement tool)
    {
        if (tool.ValueKind != JsonValueKind.Object
            || !tool.TryGetProperty("name", out var name)
            || name.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(name.GetString()))
        {
            throw new InvalidDataException("MCP tools/list returned a tool without a name.");
        }

        return name.GetString()!;
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException($"Unsupported MCP JSON kind {value.ValueKind}.");
        }
    }
}

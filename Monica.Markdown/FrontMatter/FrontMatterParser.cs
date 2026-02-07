using Monica.Markdown.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Monica.Markdown.FrontMatter;

/// <summary>
/// Parses YAML front matter from markdown content or files.
/// Front matter is delimited by --- markers at the beginning of the content.
/// </summary>
public static class FrontMatterParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Parses YAML front matter from markdown content string.
    /// </summary>
    /// <param name="content">The markdown content potentially containing front matter.</param>
    /// <returns>Parsed front matter, or null if no valid front matter found.</returns>
    public static MarkdownFrontMatter? Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var yamlBlock = ExtractYamlBlock(content);
        if (yamlBlock is null)
            return null;

        return ParseYaml(yamlBlock);
    }

    /// <summary>
    /// Parses YAML front matter from a markdown file.
    /// Reads only the first ~8KB for efficiency.
    /// </summary>
    /// <param name="filePath">Absolute path to the markdown file.</param>
    /// <returns>Parsed front matter, or null if no valid front matter found.</returns>
    public static async Task<MarkdownFrontMatter?> ParseFromFileAsync(
        string filePath)
    {
        const int bufferSize = 8192;
        var buffer = new char[bufferSize];

        using var reader = new StreamReader(filePath);
        var charsRead = await reader.ReadAsync(buffer, 0, bufferSize);
        if (charsRead == 0)
            return null;

        var content = new string(buffer, 0, charsRead);
        return Parse(content);
    }

    /// <summary>
    /// Extracts the YAML block between --- delimiters.
    /// </summary>
    private static string? ExtractYamlBlock(string content)
    {
        var span = content.AsSpan().TrimStart();
        if (!span.StartsWith("---"))
            return null;

        // Find the end of the first --- line
        var firstLineEnd = span.IndexOfAny('\r', '\n');
        if (firstLineEnd < 0)
            return null;

        // Skip past the first line's newline characters
        var afterFirst = firstLineEnd;
        while (afterFirst < span.Length && (span[afterFirst] == '\r' || span[afterFirst] == '\n'))
            afterFirst++;

        // Find the closing ---
        var remaining = span[afterFirst..];
        var closingIndex = FindClosingDelimiter(remaining);
        if (closingIndex < 0)
            return null;

        return remaining[..closingIndex].ToString();
    }

    /// <summary>
    /// Finds the position of the closing --- delimiter in the remaining content.
    /// </summary>
    private static int FindClosingDelimiter(ReadOnlySpan<char> content)
    {
        var pos = 0;
        while (pos < content.Length)
        {
            // Find next newline
            var lineStart = pos;
            var lineEnd = content[pos..].IndexOfAny('\r', '\n');
            if (lineEnd < 0)
                lineEnd = content.Length - pos;

            var line = content.Slice(pos, lineEnd).Trim();
            if (line is "---" or "...")
                return lineStart;

            pos += lineEnd;
            while (pos < content.Length && (content[pos] == '\r' || content[pos] == '\n'))
                pos++;
        }

        return -1;
    }

    /// <summary>
    /// Parses a YAML string into a MarkdownFrontMatter instance.
    /// </summary>
    private static MarkdownFrontMatter? ParseYaml(string yaml)
    {
        try
        {
            var raw = Deserializer.Deserialize<Dictionary<string, object?>>(yaml);
            if (raw is null)
                return null;

            var frontMatter = new MarkdownFrontMatter();
            var caseInsensitive = new Dictionary<string, object?>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in raw)
            {
                caseInsensitive[kvp.Key] = kvp.Value;
            }

            frontMatter.RawMetadata = caseInsensitive;

            // Extract well-known typed fields
            if (caseInsensitive.TryGetValue("title", out var titleObj)
                && titleObj is string title)
            {
                frontMatter.Title = title;
            }

            if (caseInsensitive.TryGetValue("date", out var dateObj))
            {
                frontMatter.Date = dateObj switch
                {
                    DateTime dt => dt,
                    string dateStr when DateTime.TryParse(dateStr, out var parsed) => parsed,
                    _ => null
                };
            }

            if (caseInsensitive.TryGetValue("tags", out var tagsObj))
            {
                frontMatter.Tags = tagsObj switch
                {
                    List<object> list => list
                        .Where(t => t is not null)
                        .Select(t => t.ToString()!)
                        .ToList(),
                    string singleTag => [singleTag],
                    _ => []
                };
            }

            return frontMatter;
        }
        catch
        {
            return null;
        }
    }
}
using System.Text;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Utils;

/// <summary>
/// Formats and parses Monica logical paths at storage and API boundaries.
/// </summary>
public static class ConfigurationPathFormatter
{
    private const char ESCAPE_CHAR = '\\';

    /// <summary>
    /// Formats a structured logical path to a canonical string.
    /// </summary>
    /// <param name="path">The logical path.</param>
    /// <returns>The canonical string.</returns>
    public static string Format(LogicalPath path)
    {
        var builder = new StringBuilder();
        foreach (var segment in path.Segments)
        {
            switch (segment)
            {
                case PropertySegment property:
                    if (builder.Length > 0)
                    {
                        builder.Append('.');
                    }
                    builder.Append(Escape(property.Name));
                    break;
                case DictionaryKeySegment dictionaryKey:
                    builder.Append('[').Append(Escape(dictionaryKey.Key)).Append(']');
                    break;
                case ListItemKeySegment itemKey:
                    builder.Append('[').Append(Escape(itemKey.ItemKey)).Append(']');
                    break;
                case ListIndexSegment index:
                    builder.Append('[').Append(index.Index).Append(']');
                    break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses a canonical logical path string.
    /// </summary>
    /// <param name="canonical">The canonical path.</param>
    /// <returns>The structured path.</returns>
    public static LogicalPath Parse(string canonical)
    {
        if (string.IsNullOrWhiteSpace(canonical))
        {
            return LogicalPath.Root;
        }

        var segments = new List<ConfigurationPathSegment>();
        var token = new StringBuilder();
        for (var i = 0; i < canonical.Length; i++)
        {
            var current = canonical[i];
            if (current == '.')
            {
                AddPropertySegment(segments, token);
                continue;
            }

            if (current == '[')
            {
                AddPropertySegment(segments, token);
                var bracketValue = ReadBracketValue(canonical, ref i);
                segments.Add(int.TryParse(bracketValue, out var index)
                    ? new ListIndexSegment(index)
                    : new DictionaryKeySegment(bracketValue));
                continue;
            }

            if (current == ESCAPE_CHAR && i + 1 < canonical.Length)
            {
                token.Append(canonical[++i]);
                continue;
            }

            token.Append(current);
        }

        AddPropertySegment(segments, token);
        return new LogicalPath(segments);
    }

    private static void AddPropertySegment(List<ConfigurationPathSegment> segments, StringBuilder token)
    {
        if (token.Length == 0)
        {
            return;
        }

        segments.Add(new PropertySegment(token.ToString()));
        token.Clear();
    }

    private static string ReadBracketValue(string canonical, ref int index)
    {
        var token = new StringBuilder();
        for (index++; index < canonical.Length; index++)
        {
            var current = canonical[index];
            if (current == ']')
            {
                return token.ToString();
            }

            if (current == ESCAPE_CHAR && index + 1 < canonical.Length)
            {
                token.Append(canonical[++index]);
                continue;
            }

            token.Append(current);
        }

        throw new ConfigurationPathFormatException($"Path '{canonical}' contains an unterminated bracket segment.");
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(".", "\\.", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }
}

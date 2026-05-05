using Monica.AI.AgentCapabilities.Models;

namespace Monica.AI.UI.UIChat.Support;

internal static class AgentCapabilityReferenceTokenSyntax
{
    private const string SKILL_PREFIX = "[$skills:";
    private const string MCP_PREFIX = "[$mcp:";
    private const char TOKEN_END = ']';

    public static string Create(AgentCapabilityKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains(TOKEN_END, StringComparison.Ordinal))
        {
            throw new ArgumentException("Capability reference names cannot contain ']'.", nameof(name));
        }

        var prefix = kind == AgentCapabilityKind.Skill ? SKILL_PREFIX : MCP_PREFIX;
        return $"{prefix}{name.Trim()}{TOKEN_END}";
    }

    public static IReadOnlyList<AgentCapabilityReferenceTextSegment> Parse(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        var segments = new List<AgentCapabilityReferenceTextSegment>();
        var index = 0;

        while (index < value.Length)
        {
            var tokenStart = value.IndexOf("[$", index, StringComparison.Ordinal);
            if (tokenStart < 0)
            {
                AddTextSegment(segments, value[index..]);
                break;
            }

            if (tokenStart > index)
            {
                AddTextSegment(segments, value[index..tokenStart]);
            }

            var tokenEnd = value.IndexOf(TOKEN_END, tokenStart + 2);
            if (tokenEnd < 0)
            {
                AddTextSegment(segments, value[tokenStart..]);
                break;
            }

            var tokenText = value[tokenStart..(tokenEnd + 1)];
            if (TryParseToken(tokenText, out var kind, out var name))
            {
                segments.Add(AgentCapabilityReferenceTextSegment.Reference(kind, name));
            }
            else
            {
                AddTextSegment(segments, tokenText);
            }

            index = tokenEnd + 1;
        }

        return segments;
    }

    private static bool TryParseToken(
        string tokenText,
        out AgentCapabilityKind kind,
        out string name)
    {
        if (tokenText.StartsWith(SKILL_PREFIX, StringComparison.Ordinal)
            && TryReadTokenName(tokenText, SKILL_PREFIX, out name))
        {
            kind = AgentCapabilityKind.Skill;
            return true;
        }

        if (tokenText.StartsWith(MCP_PREFIX, StringComparison.Ordinal)
            && TryReadTokenName(tokenText, MCP_PREFIX, out name))
        {
            kind = AgentCapabilityKind.Mcp;
            return true;
        }

        kind = default;
        name = string.Empty;
        return false;
    }

    private static bool TryReadTokenName(string tokenText, string prefix, out string name)
    {
        name = tokenText[prefix.Length..^1].Trim();
        return !string.IsNullOrWhiteSpace(name);
    }

    private static void AddTextSegment(List<AgentCapabilityReferenceTextSegment> segments, string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        segments.Add(AgentCapabilityReferenceTextSegment.PlainText(text));
    }
}

internal sealed record AgentCapabilityReferenceTextSegment(
    bool IsReference,
    string Text,
    AgentCapabilityKind? Kind)
{
    public static AgentCapabilityReferenceTextSegment PlainText(string text)
    {
        return new AgentCapabilityReferenceTextSegment(false, text, null);
    }

    public static AgentCapabilityReferenceTextSegment Reference(AgentCapabilityKind kind, string name)
    {
        return new AgentCapabilityReferenceTextSegment(true, name, kind);
    }
}

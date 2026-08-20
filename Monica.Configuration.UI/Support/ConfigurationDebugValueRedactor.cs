namespace Monica.Configuration.UI.Support;

/// <summary>
/// Parses Microsoft configuration debug-view lines and prevents credential-like values from being rendered by default.
/// </summary>
internal static class ConfigurationDebugValueRedactor
{
    private const string REDACTION_MARKER = "••••••••";

    private static readonly HashSet<string> SensitiveSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "pwd",
        "passphrase",
        "secret",
        "clientsecret",
        "apisecret",
        "apikey",
        "accesskey",
        "secretkey",
        "privatekey",
        "signingkey",
        "encryptionkey",
        "connectionstring",
        "connectionstrings",
        "token",
        "accesstoken",
        "refreshtoken",
        "idtoken",
        "jwttoken",
        "bearertoken",
        "authorization",
        "credential",
        "credentials",
        "sharedaccesssignature",
        "sas"
    };

    private static readonly string[] SensitiveSuffixes =
    [
        "password",
        "passphrase",
        "secret",
        "apikey",
        "accesstoken",
        "refreshtoken",
        "idtoken",
        "jwttoken",
        "bearertoken",
        "credential",
        "credentials",
        "connectionstring"
    ];

    private static readonly string[] SensitiveValueMarkers =
    [
        "password=",
        "pwd=",
        "clientsecret=",
        "accountkey=",
        "sharedaccesssignature=",
        "apikey=",
        "access_token=",
        "refresh_token=",
        "bearer "
    ];

    /// <summary>
    /// Converts raw debug-view output into indexed, display-safe lines.
    /// </summary>
    internal static IReadOnlyList<ConfigurationDebugLine> Parse(string? debugView)
    {
        if (string.IsNullOrEmpty(debugView))
        {
            return [];
        }

        var rawLines = debugView
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        var sectionPath = new List<(int Indent, string Segment)>();
        var lines = new List<ConfigurationDebugLine>(rawLines.Length);

        for (var index = 0; index < rawLines.Length; index++)
        {
            var rawText = rawLines[index];
            var indent = CountIndent(rawText);
            var trimmed = rawText.Trim();
            while (sectionPath.Count > 0 && sectionPath[^1].Indent >= indent)
            {
                sectionPath.RemoveAt(sectionPath.Count - 1);
            }

            if (!trimmed.Contains('=') && trimmed.EndsWith(':'))
            {
                sectionPath.Add((indent, trimmed[..^1]));
                lines.Add(new ConfigurationDebugLine(index, rawText, BuildPath(sectionPath, null), false, rawText));
                continue;
            }

            lines.Add(CreateLine(rawText, index, sectionPath));
        }

        return lines;
    }

    /// <summary>
    /// Determines whether a configuration path conventionally carries a credential or secret.
    /// </summary>
    internal static bool IsSensitiveKey(string key)
    {
        var segments = key
            .Split([':', '.', '/', '\\', '[', ']', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(static segment => segment.Length > 0)
            .ToArray();

        if (segments.Any(segment =>
                SensitiveSegments.Contains(segment)
                || SensitiveSuffixes.Any(suffix => segment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return segments.Length >= 2
               && segments[^1] == "key"
               && segments[..^1].Any(static segment =>
                   segment is "jwt" or "oauth" or "auth" or "authentication" or "encryption" or "signing" or "api");
    }

    /// <summary>
    /// Identifies credential material embedded in an otherwise ordinary configuration value.
    /// </summary>
    internal static bool IsSensitiveValue(string value)
    {
        var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        if (SensitiveValueMarkers.Any(marker =>
                normalized.Contains(marker.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal)))
        {
            return true;
        }

        return value.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase)
               || value.Contains("-----BEGIN RSA PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase);
    }

    private static ConfigurationDebugLine CreateLine(
        string rawText,
        int index,
        IReadOnlyList<(int Indent, string Segment)> sectionPath)
    {
        var separatorIndex = rawText.IndexOf('=');
        if (separatorIndex < 0)
        {
            return new ConfigurationDebugLine(index, rawText, rawText.Trim(), false, rawText);
        }

        var localKey = rawText[..separatorIndex].Trim();
        var key = BuildPath(sectionPath, localKey);
        var isSensitive = IsSensitiveKey(key) || IsSensitiveValue(rawText[(separatorIndex + 1)..]);
        var maskedText = isSensitive
            ? $"{rawText[..(separatorIndex + 1)]} {REDACTION_MARKER}"
            : rawText;
        return new ConfigurationDebugLine(index, rawText, key, isSensitive, maskedText);
    }

    private static string Normalize(string value)
    {
        return string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    private static string BuildPath(
        IReadOnlyList<(int Indent, string Segment)> sectionPath,
        string? localKey)
    {
        return string.Join(
            ":",
            sectionPath.Select(static section => section.Segment)
                .Append(localKey)
                .Where(static segment => !string.IsNullOrWhiteSpace(segment))
                .Select(static segment => segment!));
    }

    private static int CountIndent(string text)
    {
        var width = 0;
        foreach (var character in text)
        {
            if (character == ' ')
            {
                width++;
                continue;
            }

            if (character == '\t')
            {
                width += 4;
                continue;
            }

            break;
        }

        return width;
    }
}

/// <summary>
/// Represents one indexed configuration debug-view line with its safe display classification.
/// </summary>
internal sealed record ConfigurationDebugLine(
    int Index,
    string RawText,
    string Key,
    bool IsSensitive,
    string MaskedText)
{
    /// <summary>
    /// Gets text that can be searched without using a hidden sensitive value as an oracle.
    /// </summary>
    internal string SearchText => IsSensitive ? Key : RawText;

    /// <summary>
    /// Gets the line displayed for the current deliberate reveal state.
    /// </summary>
    internal string GetDisplayText(bool revealed) => IsSensitive && !revealed ? MaskedText : RawText;
}

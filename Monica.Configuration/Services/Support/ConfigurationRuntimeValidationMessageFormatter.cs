using System.Text;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Formats source-aware runtime validation diagnostics for startup exceptions and options validation.
/// </summary>
internal static class ConfigurationRuntimeValidationMessageFormatter
{
    /// <summary>
    /// Formats a full report as a concise startup failure message.
    /// </summary>
    /// <param name="report">The validation report.</param>
    /// <returns>The formatted message.</returns>
    public static string FormatReport(ConfigurationValidationReport report)
    {
        var builder = new StringBuilder();
        builder.Append("Monica configuration validation failed: ")
            .Append(report.IssueCount)
            .Append(report.IssueCount == 1 ? " issue." : " issues.");

        foreach (var group in report.Issues
                     .GroupBy(issue => issue.DefinitionKey, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var issue in group.OrderBy(issue => issue.LogicalPath.ToCanonicalString(), StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine()
                    .AppendLine(FormatIssueHeader(issue))
                    .AppendLine($"Path: {DisplayLogicalPath(issue.LogicalPath)}")
                    .AppendLine($"Configuration key: {issue.ConfigurationPath}")
                    .AppendLine($"Problem: {issue.Problem}")
                    .AppendLine($"Effective value: {DisplayEffectiveValue(issue)}")
                    .AppendLine($"Effective source: {DisplayEffectiveSource(issue)}")
                    .Append($"Source chain: {DisplaySourceChain(issue)}");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Formats one issue for Microsoft.Extensions.Options validation.
    /// </summary>
    /// <param name="issue">The issue to format.</param>
    /// <returns>The formatted issue.</returns>
    public static string FormatOptionsIssue(ConfigurationRuntimeValidationIssue issue)
    {
        return $"{FormatIssueHeader(issue)} {DisplayLogicalPath(issue.LogicalPath)} ({issue.ConfigurationPath}): {issue.Problem} " +
               $"Effective value: {DisplayEffectiveValue(issue)}; Effective source: {DisplayEffectiveSource(issue)}; " +
               $"Source chain: {DisplaySourceChain(issue)}";
    }

    private static string FormatIssueHeader(ConfigurationRuntimeValidationIssue issue)
    {
        var displayParts = new[] { issue.DefinitionDisplayName, issue.DefinitionCategory, issue.NodeDisplayName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var display = displayParts.Length == 0 ? null : string.Join(" / ", displayParts);
        return string.IsNullOrWhiteSpace(display)
            ? $"[{DisplayDefinitionKey(issue.DefinitionKey)}]"
            : $"[{DisplayDefinitionKey(issue.DefinitionKey)}] {display}";
    }

    private static string DisplayDefinitionKey(string definitionKey)
    {
        var lastSeparator = Math.Max(definitionKey.LastIndexOf('.'), definitionKey.LastIndexOf('+'));
        return lastSeparator >= 0 ? definitionKey[(lastSeparator + 1)..] : definitionKey;
    }

    private static string DisplayLogicalPath(LogicalPath logicalPath)
    {
        return logicalPath.Depth == 0
            ? "$"
            : $"$.{logicalPath.ToCanonicalString()}";
    }

    private static string DisplayEffectiveValue(ConfigurationRuntimeValidationIssue issue)
    {
        if (issue.IsMissing)
        {
            return "<missing>";
        }

        if (issue.IsSensitive)
        {
            return "<redacted>";
        }

        return string.IsNullOrWhiteSpace(issue.EffectiveDisplayValue)
            ? "<empty>"
            : issue.EffectiveDisplayValue;
    }

    private static string DisplayEffectiveSource(ConfigurationRuntimeValidationIssue issue)
    {
        return issue.EffectiveSource is null
            ? "none"
            : $"{issue.EffectiveSource.DisplayName} ({issue.EffectiveSource.ProviderType})";
    }

    private static string DisplaySourceChain(ConfigurationRuntimeValidationIssue issue)
    {
        if (issue.SourceChain.Values.Count == 0)
        {
            return "no provider supplies this key.";
        }

        return string.Join(
            " -> ",
            issue.SourceChain.Values.Select(value =>
                value.IsEffective
                    ? $"{value.Source.DisplayName} (effective)"
                    : value.Source.DisplayName));
    }
}

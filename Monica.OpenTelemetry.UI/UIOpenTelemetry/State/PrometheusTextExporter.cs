using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Monica.OpenTelemetry.InProcessCollector.Models;

namespace Monica.OpenTelemetry.UI.UIOpenTelemetry.State;

/// <summary>
/// Converts in-process snapshots into a lightweight Prometheus exposition text view for clipboard export.
/// </summary>
internal static partial class PrometheusTextExporter
{
    public static string Export(OpenTelemetrySnapshot snapshot)
    {
        var builder = new StringBuilder();

        foreach (var instrument in snapshot.Instruments.OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            var metricName = NormalizeMetricName(instrument.Name);
            if (!string.IsNullOrWhiteSpace(instrument.Description))
            {
                builder.Append("# HELP ")
                    .Append(metricName)
                    .Append(' ')
                    .AppendLine(EscapeHelp(instrument.Description));
            }

            builder.Append("# TYPE ")
                .Append(metricName)
                .Append(' ')
                .AppendLine(GetPrometheusType(instrument.Kind));

            foreach (var tagSet in instrument.TagSets)
            {
                if (tagSet.Histogram is not null)
                {
                    AppendHistogram(builder, metricName, tagSet);
                    continue;
                }

                AppendSample(builder, metricName, tagSet.Tags, tagSet.CurrentValue);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GetPrometheusType(string instrumentKind)
    {
        return instrumentKind switch
        {
            "Counter" or "ObservableCounter" => "counter",
            "Histogram" => "histogram",
            _ => "gauge"
        };
    }

    private static void AppendHistogram(StringBuilder builder, string metricName, TagSetSamples tagSet)
    {
        var histogram = tagSet.Histogram!;
        long cumulativeCount = 0;

        foreach (var bucket in histogram.Buckets)
        {
            cumulativeCount += bucket.Count;
            AppendSample(
                builder,
                metricName + "_bucket",
                AddTag(tagSet.Tags, "le", bucket.UpperBound.ToString("G17", CultureInfo.InvariantCulture)),
                cumulativeCount);
        }

        AppendSample(
            builder,
            metricName + "_bucket",
            AddTag(tagSet.Tags, "le", "+Inf"),
            histogram.Count);
        AppendSample(builder, metricName + "_count", tagSet.Tags, histogram.Count);
        AppendSample(builder, metricName + "_sum", tagSet.Tags, histogram.Average * histogram.Count);
    }

    private static void AppendSample(
        StringBuilder builder,
        string metricName,
        IReadOnlyDictionary<string, string> tags,
        double value)
    {
        builder.Append(metricName)
            .Append(FormatTags(tags))
            .Append(' ')
            .AppendLine(value.ToString("G17", CultureInfo.InvariantCulture));
    }

    private static IReadOnlyDictionary<string, string> AddTag(
        IReadOnlyDictionary<string, string> tags,
        string key,
        string value)
    {
        var merged = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            merged[tag.Key] = tag.Value;
        }

        merged[key] = value;
        return new Dictionary<string, string>(merged, StringComparer.Ordinal);
    }

    private static string FormatTags(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.Count == 0)
        {
            return string.Empty;
        }

        return "{" + string.Join(
            ",",
            tags.Select(static tag =>
                $"{NormalizeMetricName(tag.Key)}=\"{EscapeTagValue(tag.Value)}\"")) + "}";
    }

    private static string EscapeHelp(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string EscapeTagValue(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string NormalizeMetricName(string value)
    {
        var normalized = MetricNameRegex().Replace(value, "_");
        return char.IsDigit(normalized[0])
            ? "_" + normalized
            : normalized;
    }

    [GeneratedRegex("[^a-zA-Z0-9_:]")]
    private static partial Regex MetricNameRegex();
}

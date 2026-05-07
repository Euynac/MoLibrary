using System.Text.Json;
using Monica.OpenTelemetry.InProcessCollector.Models;

namespace Monica.OpenTelemetry.UI.UIOpenTelemetry.State;

/// <summary>
/// Exports compact dashboard JSON that keeps the current metric state and omits retained sample history.
/// </summary>
internal static class OpenTelemetryJsonSummaryExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Export(OpenTelemetrySnapshot snapshot)
    {
        return JsonSerializer.Serialize(CreateSummary(snapshot), SerializerOptions);
    }

    private static SnapshotSummary CreateSummary(OpenTelemetrySnapshot snapshot)
    {
        return new SnapshotSummary(
            snapshot.ServiceName,
            snapshot.ServiceVersion,
            snapshot.DeploymentEnvironment,
            snapshot.TimestampUtc,
            snapshot.MeterPatterns,
            snapshot.SamplesPerSeries,
            snapshot.MaxTagSetsPerInstrument,
            snapshot.HasOverflow,
            snapshot.Instruments.Select(CreateInstrumentSummary).ToList());
    }

    private static InstrumentSummary CreateInstrumentSummary(InstrumentSnapshot instrument)
    {
        return new InstrumentSummary(
            instrument.MeterName,
            instrument.Name,
            instrument.Kind,
            instrument.Unit,
            instrument.Description,
            instrument.IsObservable,
            instrument.CurrentValue,
            instrument.Delta,
            instrument.Overflow,
            instrument.DroppedSamples,
            instrument.TagSets.Count,
            instrument.TagSets.Select(CreateTagSetSummary).ToList());
    }

    private static TagSetSummary CreateTagSetSummary(TagSetSamples tagSet)
    {
        return new TagSetSummary(
            tagSet.Key,
            tagSet.Tags,
            tagSet.CurrentValue,
            tagSet.Delta,
            tagSet.LastUpdatedUtc,
            tagSet.Samples.Count,
            tagSet.Histogram);
    }

    private sealed record SnapshotSummary(
        string ServiceName,
        string? ServiceVersion,
        string? DeploymentEnvironment,
        DateTimeOffset TimestampUtc,
        IReadOnlyList<string> MeterPatterns,
        int SamplesPerSeries,
        int MaxTagSetsPerInstrument,
        bool HasOverflow,
        IReadOnlyList<InstrumentSummary> Instruments);

    private sealed record InstrumentSummary(
        string MeterName,
        string Name,
        string Kind,
        string? Unit,
        string? Description,
        bool IsObservable,
        double CurrentValue,
        double Delta,
        bool Overflow,
        long DroppedSamples,
        int TagSetCount,
        IReadOnlyList<TagSetSummary> TagSets);

    private sealed record TagSetSummary(
        string Key,
        IReadOnlyDictionary<string, string> Tags,
        double CurrentValue,
        double Delta,
        DateTimeOffset LastUpdatedUtc,
        int RetainedSampleCount,
        HistogramSnapshot? Histogram);
}

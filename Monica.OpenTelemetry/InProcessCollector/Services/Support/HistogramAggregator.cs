using Monica.OpenTelemetry.InProcessCollector.Models;

namespace Monica.OpenTelemetry.InProcessCollector.Services.Support;

/// <summary>
/// Calculates approximate histogram statistics from the bounded retained sample window.
/// </summary>
internal static class HistogramAggregator
{
    private const int BUCKET_COUNT = 10;

    public static HistogramSnapshot? Build(IReadOnlyList<MetricPointSnapshot> samples)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        var values = samples
            .Select(static sample => sample.Value)
            .Order()
            .ToArray();

        var min = values[0];
        var max = values[^1];

        return new HistogramSnapshot
        {
            Count = values.Length,
            Min = min,
            Max = max,
            Average = values.Average(),
            P50 = Percentile(values, 0.50),
            P95 = Percentile(values, 0.95),
            P99 = Percentile(values, 0.99),
            Buckets = BuildBuckets(values, min, max)
        };
    }

    private static double Percentile(double[] orderedValues, double percentile)
    {
        if (orderedValues.Length == 1)
        {
            return orderedValues[0];
        }

        var index = (orderedValues.Length - 1) * percentile;
        var lowerIndex = (int)Math.Floor(index);
        var upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
        {
            return orderedValues[lowerIndex];
        }

        var weight = index - lowerIndex;
        return orderedValues[lowerIndex] * (1 - weight) + orderedValues[upperIndex] * weight;
    }

    private static IReadOnlyList<HistogramBucketSnapshot> BuildBuckets(double[] orderedValues, double min, double max)
    {
        if (min.Equals(max))
        {
            return
            [
                new HistogramBucketSnapshot
                {
                    LowerBound = min,
                    UpperBound = max,
                    Count = orderedValues.Length
                }
            ];
        }

        var bucketWidth = (max - min) / BUCKET_COUNT;
        var counts = new long[BUCKET_COUNT];

        foreach (var value in orderedValues)
        {
            var bucketIndex = Math.Min(BUCKET_COUNT - 1, (int)((value - min) / bucketWidth));
            counts[bucketIndex]++;
        }

        var buckets = new List<HistogramBucketSnapshot>(BUCKET_COUNT);
        for (var i = 0; i < BUCKET_COUNT; i++)
        {
            buckets.Add(new HistogramBucketSnapshot
            {
                LowerBound = min + i * bucketWidth,
                UpperBound = i == BUCKET_COUNT - 1 ? max : min + (i + 1) * bucketWidth,
                Count = counts[i]
            });
        }

        return buckets;
    }
}

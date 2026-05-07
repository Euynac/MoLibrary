using System.Diagnostics.Metrics;

namespace Monica.OpenTelemetry.InProcessCollector.Services.Support;

/// <summary>
/// Infers a stable instrument kind label from the concrete .NET instrument type.
/// </summary>
internal static class InstrumentKindResolver
{
    public static string Resolve(Instrument instrument)
    {
        var genericTypeName = GetGenericTypeName(instrument.GetType());
        return genericTypeName switch
        {
            "Counter`1" => "Counter",
            "UpDownCounter`1" => "UpDownCounter",
            "Histogram`1" => "Histogram",
            "ObservableCounter`1" => "ObservableCounter",
            "ObservableUpDownCounter`1" => "ObservableUpDownCounter",
            "ObservableGauge`1" => "ObservableGauge",
            _ when instrument.IsObservable => "Observable",
            _ => "Instrument"
        };
    }

    private static string GetGenericTypeName(Type type)
    {
        while (type != typeof(object))
        {
            if (type.IsGenericType)
            {
                return type.GetGenericTypeDefinition().Name;
            }

            type = type.BaseType ?? typeof(object);
        }

        return string.Empty;
    }
}

using System.Diagnostics.Metrics;

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Determines the <see cref="InstrumentKind"/> of an instrument
/// </summary>
internal static class InstrumentClassifier
{
    // Synchronous Gauge<T> ships with System.Diagnostics.DiagnosticSource 9+, not with the net8 BCL,
    // so it is recognized by name instead of by type.
    private const string GaugeTypeName = "System.Diagnostics.Metrics.Gauge`1";

    public static InstrumentKind Classify(Instrument instrument)
    {
        var type = instrument.GetType();

        if (!type.IsGenericType)
        {
            return InstrumentKind.Unknown;
        }

        var definition = type.GetGenericTypeDefinition();

        if (definition == typeof(Counter<>))
        {
            return InstrumentKind.Counter;
        }

        if (definition == typeof(UpDownCounter<>))
        {
            return InstrumentKind.UpDownCounter;
        }

        if (definition == typeof(Histogram<>))
        {
            return InstrumentKind.Histogram;
        }

        if (definition == typeof(ObservableCounter<>))
        {
            return InstrumentKind.ObservableCounter;
        }

        if (definition == typeof(ObservableUpDownCounter<>))
        {
            return InstrumentKind.ObservableUpDownCounter;
        }

        if (definition == typeof(ObservableGauge<>))
        {
            return InstrumentKind.ObservableGauge;
        }

        if (definition.FullName == GaugeTypeName)
        {
            return InstrumentKind.Gauge;
        }

        return InstrumentKind.Unknown;
    }
}

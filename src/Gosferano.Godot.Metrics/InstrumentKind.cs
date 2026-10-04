namespace Gosferano.Godot.Metrics;

/// <summary>
/// Recognized instrument types
/// </summary>
internal enum InstrumentKind
{
    Unknown,
    Counter,
    UpDownCounter,
    Gauge,
    Histogram,
    ObservableCounter,
    ObservableUpDownCounter,
    ObservableGauge,
}

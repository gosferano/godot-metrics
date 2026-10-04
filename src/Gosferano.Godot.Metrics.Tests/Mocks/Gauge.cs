// Declared in the BCL namespace on purpose: synchronous Gauge<T> comes from System.Diagnostics.DiagnosticSource 9+,
// which the library must not depend on, so detection is by full type name. This stand-in has that exact name.
namespace System.Diagnostics.Metrics;

/// <summary>
/// Stand-in for <c>System.Diagnostics.Metrics.Gauge&lt;T&gt;</c> from DiagnosticSource 9+
/// </summary>
public sealed class Gauge<T> : Instrument<T>
    where T : struct
{
    public Gauge(Meter meter, string name, string? unit = null)
        : base(meter, name, unit, null)
    {
        Publish();
    }

    public void Record(T value)
    {
        RecordMeasurement(value);
    }

    public void Record(T value, KeyValuePair<string, object?> tag)
    {
        RecordMeasurement(value, tag);
    }
}

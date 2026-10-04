using System.Diagnostics.Metrics;

namespace Gosferano.Godot.Metrics.Tests.Mocks;

/// <summary>
/// Custom instrument type the library does not recognize
/// </summary>
public sealed class UnknownInstrument<T> : Instrument<T>
    where T : struct
{
    public UnknownInstrument(Meter meter, string name)
        : base(meter, name, null, null)
    {
        Publish();
    }

    public void Record(T value)
    {
        RecordMeasurement(value);
    }
}

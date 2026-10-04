namespace Gosferano.Godot.Metrics;

/// <summary>
/// Keeps the most recent measurement (gauges, observable gauges and unrecognized instruments)
/// </summary>
internal sealed class LastValueSeries : MetricSeries
{
    private static readonly SeriesStat[] StatsArray = [new("", true)];

    private double _value;

    public override IReadOnlyList<SeriesStat> Stats => StatsArray;

    public override void Record(double value)
    {
        Volatile.Write(ref _value, value);
    }

    public override void Collect(double elapsedSeconds, Span<double> values)
    {
        values[0] = Volatile.Read(ref _value);
    }
}

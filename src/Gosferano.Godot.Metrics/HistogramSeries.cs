namespace Gosferano.Godot.Metrics;

/// <summary>
/// Sliding window over the most recent histogram measurements: last, avg, p95, max and the lifetime count
/// </summary>
internal sealed class HistogramSeries : MetricSeries
{
    internal const int WindowSize = 256;

    private static readonly SeriesStat[] StatsArray =
    [
        new(" last", true),
        new(" avg", true),
        new(" p95", true),
        new(" max", true),
        new(" count", false),
    ];

    private readonly double[] _window = new double[WindowSize];
    private readonly double[] _sorted = new double[WindowSize];
    private readonly object _gate = new();

    private int _filled;
    private int _next;
    private long _count;
    private double _last;

    public override IReadOnlyList<SeriesStat> Stats => StatsArray;

    public override void Record(double value)
    {
        lock (_gate)
        {
            _window[_next] = value;
            _next = (_next + 1) % WindowSize;
            _filled = Math.Min(_filled + 1, WindowSize);
            _count++;
            _last = value;
        }
    }

    public override void Collect(double elapsedSeconds, Span<double> values)
    {
        int filled;
        long count;
        double last;

        lock (_gate)
        {
            filled = _filled;
            count = _count;
            last = _last;
            Array.Copy(_window, _sorted, filled);
        }

        values[0] = last;
        values[4] = count;

        if (filled == 0)
        {
            values[1] = 0;
            values[2] = 0;
            values[3] = 0;
            return;
        }

        var window = _sorted.AsSpan(0, filled);
        window.Sort();

        double sum = 0;
        foreach (double value in window)
        {
            sum += value;
        }

        // Nearest-rank percentile
        int p95Index = (int)Math.Ceiling(0.95 * filled) - 1;

        values[1] = sum / filled;
        values[2] = window[p95Index];
        values[3] = window[^1];
    }
}

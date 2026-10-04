namespace Gosferano.Godot.Metrics;

/// <summary>
/// Running sum for counters and up-down counters, with an optional per-second rate.
/// Synchronous instruments report deltas; observable instruments report cumulative values,
/// which are summed across the observations of one poll.
/// </summary>
internal sealed class SumSeries : MetricSeries
{
    private readonly bool _observable;
    private readonly bool _reportRate;
    private readonly SeriesStat[] _stats;

    private double _total;
    private double _previousTotal;
    private bool _hasPrevious;
    private double _observedSum;
    private bool _observed;

    public SumSeries(bool observable, bool reportRate, bool reportTotal)
    {
        if (!reportRate && !reportTotal)
        {
            throw new ArgumentException("A sum series must report a rate, a total, or both");
        }

        _observable = observable;
        _reportRate = reportRate;

        // A synchronous counter starts at zero, so the first rate is exact. An observable one may start at
        // any cumulative value, so its first poll only establishes the baseline.
        _hasPrevious = !observable;

        var stats = new List<SeriesStat>();

        if (reportRate)
        {
            stats.Add(new SeriesStat(" rate", true));
        }

        if (reportTotal)
        {
            stats.Add(new SeriesStat(reportRate ? " total" : "", true));
        }

        _stats = stats.ToArray();
    }

    public override IReadOnlyList<SeriesStat> Stats => _stats;

    public override void Record(double value)
    {
        if (_observable)
        {
            _observedSum += value;
            _observed = true;
            return;
        }

        double current = Volatile.Read(ref _total);

        while (true)
        {
            double original = Interlocked.CompareExchange(ref _total, current + value, current);

            // Compare bit patterns so NaN totals cannot spin forever
            if (BitConverter.DoubleToInt64Bits(original) == BitConverter.DoubleToInt64Bits(current))
            {
                return;
            }

            current = original;
        }
    }

    public override void BeginObservation()
    {
        _observedSum = 0;
        _observed = false;
    }

    public override void EndObservation()
    {
        if (_observed)
        {
            Volatile.Write(ref _total, _observedSum);
        }
    }

    public override void Collect(double elapsedSeconds, Span<double> values)
    {
        double total = Volatile.Read(ref _total);
        var index = 0;

        if (_reportRate)
        {
            values[index++] = _hasPrevious && elapsedSeconds > 0 ? (total - _previousTotal) / elapsedSeconds : 0;
        }

        if (index < _stats.Length)
        {
            values[index] = total;
        }

        if (!_observable || _observed)
        {
            _previousTotal = total;
            _hasPrevious = true;
        }
    }
}

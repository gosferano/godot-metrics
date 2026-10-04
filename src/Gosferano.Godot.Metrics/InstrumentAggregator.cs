using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Routes an instrument's measurements to its series and registers one monitor per series stat.
/// All tags are merged into a single series unless split keys are configured. Split series are capped;
/// combinations beyond the cap are merged into an <c>{other}</c> series.
/// </summary>
internal sealed class InstrumentAggregator
{
    internal const int MaxSeries = 100;
    internal const string OtherSeriesKey = "{other}";

    private readonly Instrument _instrument;
    private readonly InstrumentKind _kind;
    private readonly string[] _splitKeys;
    private readonly bool _counterTotals;
    private readonly IMonitorAdapter _adapter;
    private readonly Func<string, double> _readValue;
    private readonly MonitorFormat _unitFormat;
    private readonly double _unitScale;
    private readonly ConcurrentDictionary<string, SeriesEntry> _series = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    private int _distinctSeries;
    private bool _capWarned;
    private bool _closed;

    /// <param name="instrument">Instrument to aggregate</param>
    /// <param name="kind">Instrument kind, deciding the series type</param>
    /// <param name="splitKeys">Tag keys to split by; empty merges all tags</param>
    /// <param name="counterTotals">Whether counters also report their running total</param>
    /// <param name="adapter">Receives monitor registrations</param>
    /// <param name="readValue">Reads a monitor's current value (in instrument units) by id</param>
    public InstrumentAggregator(
        Instrument instrument,
        InstrumentKind kind,
        string[] splitKeys,
        bool counterTotals,
        IMonitorAdapter adapter,
        Func<string, double> readValue
    )
    {
        _instrument = instrument;
        _kind = kind;
        _splitKeys = splitKeys;
        _counterTotals = counterTotals;
        _adapter = adapter;
        _readValue = readValue;
        (_unitFormat, _unitScale) = MonitorUnits.Resolve(instrument.Unit);
        BaseId = Sanitize(instrument.Meter.Name) + "/" + Sanitize(instrument.Name);
    }

    /// <summary>
    /// <c>"&lt;MeterName&gt;/&lt;instrument&gt;"</c>, the prefix of every monitor id of this instrument
    /// </summary>
    public string BaseId { get; }

    public bool IsObservable => _instrument.IsObservable;

    /// <summary>
    /// Whether series are created per tag value, so none exist before the first measurement
    /// </summary>
    public bool IsSplit => _splitKeys.Length > 0;

    /// <summary>
    /// Registers the monitors of a merged instrument up front, so they show before the first measurement.
    /// This also gives Godot something to poll, which in turn polls observable instruments.
    /// </summary>
    public void Initialize()
    {
        if (!IsSplit)
        {
            GetOrCreateSeries("");
        }
    }

    public void Record(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        string key = IsSplit ? BuildSeriesKey(tags) : "";

        if (_series.TryGetValue(key, out var entry))
        {
            entry.Series.Record(value);
            return;
        }

        GetOrCreateSeries(key).Record(value);
    }

    public void BeginObservation()
    {
        foreach (var entry in _series.Values)
        {
            entry.Series.BeginObservation();
        }
    }

    public void EndObservation()
    {
        foreach (var entry in _series.Values)
        {
            entry.Series.EndObservation();
        }
    }

    /// <summary>
    /// Adds the current value of every monitor of this instrument to <paramref name="values"/>
    /// </summary>
    public void Collect(double elapsedSeconds, Dictionary<string, double> values)
    {
        Span<double> buffer = stackalloc double[8];

        foreach (var entry in _series.Values)
        {
            var slice = buffer[..entry.Ids.Length];
            entry.Series.Collect(elapsedSeconds, slice);

            for (var i = 0; i < entry.Ids.Length; i++)
            {
                values[entry.Ids[i]] = slice[i];
            }
        }
    }

    /// <summary>
    /// Stops creating series and returns the ids of every registered monitor
    /// </summary>
    public IReadOnlyList<string> Close()
    {
        lock (_gate)
        {
            _closed = true;
            return _series.Values.SelectMany(entry => entry.Ids).ToArray();
        }
    }

    private MetricSeries GetOrCreateSeries(string key)
    {
        lock (_gate)
        {
            if (_closed)
            {
                // Late measurement racing with removal; record into a series nobody reads
                return CreateSeries();
            }

            if (_series.TryGetValue(key, out var existing))
            {
                return existing.Series;
            }

            if (_distinctSeries >= MaxSeries)
            {
                if (!_capWarned)
                {
                    _capWarned = true;
                    _adapter.LogWarning(
                        $"'{BaseId}' exceeded {MaxSeries} tag combinations; "
                            + $"further combinations are merged into {OtherSeriesKey}."
                    );
                }

                key = OtherSeriesKey;

                if (_series.TryGetValue(key, out existing))
                {
                    return existing.Series;
                }
            }
            else
            {
                _distinctSeries++;
            }

            var series = CreateSeries();
            var ids = series.Stats.Select(stat => BaseId + key + stat.Suffix).ToArray();
            _series[key] = new SeriesEntry(series, ids);

            for (var i = 0; i < ids.Length; i++)
            {
                RegisterMonitor(ids[i], series.Stats[i]);
            }

            return series;
        }
    }

    private void RegisterMonitor(string id, SeriesStat stat)
    {
        var format = stat.UsesInstrumentUnit ? _unitFormat : MonitorFormat.Quantity;
        double scale = stat.UsesInstrumentUnit ? _unitScale : 1d;

        _adapter.AddMonitor(new MonitorDefinition(id, format, () => _readValue(id) * scale));
    }

    private MetricSeries CreateSeries()
    {
        return _kind switch
        {
            InstrumentKind.Counter => new SumSeries(observable: false, reportRate: true, _counterTotals),
            InstrumentKind.ObservableCounter => new SumSeries(observable: true, reportRate: true, _counterTotals),
            InstrumentKind.UpDownCounter => new SumSeries(observable: false, reportRate: false, reportTotal: true),
            InstrumentKind.ObservableUpDownCounter => new SumSeries(
                observable: true,
                reportRate: false,
                reportTotal: true
            ),
            InstrumentKind.Histogram => new HistogramSeries(),
            _ => new LastValueSeries(),
        };
    }

    private string BuildSeriesKey(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        StringBuilder? builder = null;

        foreach (string splitKey in _splitKeys)
        {
            foreach (var tag in tags)
            {
                if (tag.Key != splitKey)
                {
                    continue;
                }

                builder = builder is null ? new StringBuilder("{") : builder.Append(',');
                builder
                    .Append(Sanitize(splitKey))
                    .Append('=')
                    .Append(Sanitize(Convert.ToString(tag.Value, CultureInfo.InvariantCulture) ?? ""));
                break;
            }
        }

        // No split key present: the measurement belongs to the untagged series
        return builder?.Append('}').ToString() ?? "";
    }

    // Godot puts ids with more than one '/' into the default category, so only the meter separator may use it
    private static string Sanitize(string value)
    {
        return value.Replace('/', '_');
    }

    private sealed record SeriesEntry(MetricSeries Series, string[] Ids);
}

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

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

    // Room for every distinct series plus the same values arriving as different types (e.g. int and long ids)
    internal const int MaxCachedSplitKeys = MaxSeries * 2;

    [ThreadStatic]
    private static object?[]? _splitValuesScratch;

    private readonly Instrument _instrument;
    private readonly InstrumentKind _kind;
    private readonly string[] _splitKeys;
    private readonly bool _counterTotals;
    private readonly IMonitorAdapter _adapter;
    private readonly ILogger _logger;
    private readonly Func<string, double> _readValue;
    private readonly MonitorFormat _unitFormat;
    private readonly double _unitScale;
    private readonly ConcurrentDictionary<string, SeriesEntry> _series = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<SplitKey, SeriesEntry> _splitCache = new();
    private readonly object _gate = new();

    private int _distinctSeries;
    private bool _capWarned;
    private bool _closed;

    /// <param name="instrument">Instrument to aggregate</param>
    /// <param name="kind">Instrument kind, deciding the series type</param>
    /// <param name="splitKeys">Tag keys to split by; empty merges all tags</param>
    /// <param name="counterTotals">Whether counters also report their running total</param>
    /// <param name="adapter">Receives monitor registrations</param>
    /// <param name="logger">Receives the series cap warning</param>
    /// <param name="readValue">Reads a monitor's current value (in instrument units) by id</param>
    public InstrumentAggregator(
        Instrument instrument,
        InstrumentKind kind,
        string[] splitKeys,
        bool counterTotals,
        IMonitorAdapter adapter,
        ILogger logger,
        Func<string, double> readValue
    )
    {
        _instrument = instrument;
        _kind = kind;
        _splitKeys = splitKeys;
        _counterTotals = counterTotals;
        _adapter = adapter;
        _logger = logger;
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
    /// Number of tag value combinations that resolve to a series without allocating
    /// </summary>
    internal int CachedSplitKeyCount => _splitCache.Count;

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
        if (!IsSplit)
        {
            if (!_series.TryGetValue("", out var merged))
            {
                merged = GetOrCreateSeries("");
            }

            merged.Series.Record(value);
            return;
        }

        // Hot path: look the series up by tag values, without building its id
        object?[] values = CollectSplitValues(tags);

        if (_splitCache.TryGetValue(new SplitKey(values, _splitKeys.Length), out var cached))
        {
            cached.Series.Record(value);
            return;
        }

        RecordUncached(value, values.AsSpan(0, _splitKeys.Length).ToArray());
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
            _splitCache.Clear();
            return _series.Values.SelectMany(entry => entry.Ids).ToArray();
        }
    }

    private void RecordUncached(double value, object?[] values)
    {
        string key = BuildSeriesKey(values);
        var entry = GetOrCreateSeries(key);
        entry.Series.Record(value);

        // Cache only series these values own. Overflow into {other} stays uncached, so unbounded tag values
        // (the case the cap exists for) cannot grow the cache either.
        if (entry.Key == key && _splitCache.Count < MaxCachedSplitKeys)
        {
            _splitCache.TryAdd(new SplitKey(values, values.Length), entry);
        }
    }

    private object?[] CollectSplitValues(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        int length = _splitKeys.Length;
        object?[] values = _splitValuesScratch is { } scratch && scratch.Length >= length
            ? scratch
            : _splitValuesScratch = new object?[Math.Max(length, 4)];

        values.AsSpan(0, length).Fill(SplitKey.Missing);

        foreach (var tag in tags)
        {
            int index = Array.IndexOf(_splitKeys, tag.Key);

            // First occurrence of a key wins
            if (index >= 0 && ReferenceEquals(values[index], SplitKey.Missing))
            {
                values[index] = tag.Value;
            }
        }

        return values;
    }

    private SeriesEntry GetOrCreateSeries(string key)
    {
        lock (_gate)
        {
            if (_closed)
            {
                // Late measurement racing with removal; record into a series nobody reads
                return new SeriesEntry(null, CreateSeries(), []);
            }

            if (_series.TryGetValue(key, out var existing))
            {
                return existing;
            }

            if (_distinctSeries >= MaxSeries)
            {
                if (!_capWarned)
                {
                    _capWarned = true;
                    MetricsLog.SeriesCapExceeded(_logger, BaseId, MaxSeries, OtherSeriesKey);
                }

                key = OtherSeriesKey;

                if (_series.TryGetValue(key, out existing))
                {
                    return existing;
                }
            }
            else
            {
                _distinctSeries++;
            }

            var series = CreateSeries();
            var ids = series.Stats.Select(stat => BaseId + key + stat.Suffix).ToArray();
            var entry = new SeriesEntry(key, series, ids);
            _series[key] = entry;

            for (var i = 0; i < ids.Length; i++)
            {
                RegisterMonitor(ids[i], series.Stats[i]);
            }

            return entry;
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

    private string BuildSeriesKey(object?[] values)
    {
        StringBuilder? builder = null;

        for (var i = 0; i < _splitKeys.Length; i++)
        {
            if (ReferenceEquals(values[i], SplitKey.Missing))
            {
                continue;
            }

            builder = builder is null ? new StringBuilder("{") : builder.Append(',');
            builder
                .Append(Sanitize(_splitKeys[i]))
                .Append('=')
                .Append(Sanitize(Convert.ToString(values[i], CultureInfo.InvariantCulture) ?? ""));
        }

        // No split key present: the measurement belongs to the untagged series
        return builder?.Append('}').ToString() ?? "";
    }

    // Godot puts ids with more than one '/' into the default category, so only the meter separator may use it
    private static string Sanitize(string value)
    {
        return value.Replace('/', '_');
    }

    /// <param name="Key">Series key, or null for a detached series created after <see cref="Close"/></param>
    private sealed record SeriesEntry(string? Key, MetricSeries Series, string[] Ids);
}

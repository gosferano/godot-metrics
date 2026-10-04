using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Listens to the included meters and turns their measurements into monitor values.
/// Values are computed in collect cycles that are shared by all monitors and snapshots, so observable
/// instruments are polled once per cycle rather than once per monitor.
/// </summary>
internal sealed class MetricsCollector : IDisposable
{
    /// <summary>
    /// Reads within this interval reuse the previous cycle. Godot polls every monitor back to back.
    /// </summary>
    internal static readonly TimeSpan MinCollectInterval = TimeSpan.FromMilliseconds(100);

    private static readonly IReadOnlyDictionary<string, double> EmptySnapshot = new Dictionary<string, double>();

    private readonly GodotMetricsOptions _options;
    private readonly IMonitorAdapter _adapter;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly MeterFilter _filter;
    private readonly MeterListener _listener;
    private readonly ConcurrentDictionary<Instrument, InstrumentAggregator> _instruments = new();
    private readonly ConcurrentDictionary<Type, byte> _unknownTypesLogged = new();
    private readonly object _collectGate = new();

    private IReadOnlyDictionary<string, double> _snapshot = EmptySnapshot;
    private long _lastCollectTimestamp;
    private int _disposed;

    public MetricsCollector(
        GodotMetricsOptions options,
        IMonitorAdapter adapter,
        ILogger logger,
        TimeProvider timeProvider
    )
    {
        _options = options;
        _adapter = adapter;
        _logger = logger;
        _timeProvider = timeProvider;
        _filter = new MeterFilter(options.MeterPatterns);
        _lastCollectTimestamp = timeProvider.GetTimestamp();

        _listener = new MeterListener
        {
            InstrumentPublished = OnInstrumentPublished,
            MeasurementsCompleted = OnMeasurementsCompleted,
        };

        _listener.SetMeasurementEventCallback<byte>((_, value, tags, state) => Record(value, tags, state));
        _listener.SetMeasurementEventCallback<short>((_, value, tags, state) => Record(value, tags, state));
        _listener.SetMeasurementEventCallback<int>((_, value, tags, state) => Record(value, tags, state));
        _listener.SetMeasurementEventCallback<long>((_, value, tags, state) => Record(value, tags, state));
        _listener.SetMeasurementEventCallback<float>((_, value, tags, state) => Record(value, tags, state));
        _listener.SetMeasurementEventCallback<double>((_, value, tags, state) => Record(value, tags, state));
        _listener.SetMeasurementEventCallback<decimal>((_, value, tags, state) => Record((double)value, tags, state));

        _listener.Start();
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Current value of every monitor in instrument units
    /// </summary>
    public IReadOnlyDictionary<string, double> GetSnapshot()
    {
        Collect(force: false);
        return Volatile.Read(ref _snapshot);
    }

    /// <summary>
    /// Current value of one monitor in instrument units, or 0 if it has not been collected yet
    /// </summary>
    public double ReadValue(string id)
    {
        Collect(force: false);
        return Volatile.Read(ref _snapshot).TryGetValue(id, out double value) ? value : 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_collectGate)
        {
            // Raises MeasurementsCompleted for every enabled instrument, which removes its monitors
            _listener.Dispose();

            foreach (var instrument in _instruments.Keys)
            {
                RemoveInstrument(instrument);
            }

            Volatile.Write(ref _snapshot, EmptySnapshot);
        }
    }

    private void OnInstrumentPublished(Instrument instrument, MeterListener listener)
    {
        if (IsDisposed || !_filter.Matches(instrument.Meter.Name))
        {
            return;
        }

        var kind = InstrumentClassifier.Classify(instrument);

        if (kind == InstrumentKind.Unknown && _unknownTypesLogged.TryAdd(instrument.GetType(), 0))
        {
            MetricsLog.UnrecognizedInstrumentType(
                _logger,
                instrument.GetType().FullName ?? instrument.GetType().Name,
                $"{instrument.Meter.Name}/{instrument.Name}"
            );
        }

        string[] splitKeys = _options.Splits.TryGetValue(instrument.Name, out string[]? keys) ? keys : [];
        var aggregator = new InstrumentAggregator(
            instrument,
            kind,
            splitKeys,
            _options.CounterTotals,
            _adapter,
            _logger,
            ReadValue
        );

        if (!_instruments.TryAdd(instrument, aggregator))
        {
            return;
        }

        aggregator.Initialize();
        listener.EnableMeasurementEvents(instrument, aggregator);

        if (aggregator.IsObservable && aggregator.IsSplit)
        {
            // Split series only exist once observed, and Godot only polls existing monitors
            _adapter.Post(() => Collect(force: true));
        }
    }

    private void OnMeasurementsCompleted(Instrument instrument, object? state)
    {
        RemoveInstrument(instrument);
    }

    private void RemoveInstrument(Instrument instrument)
    {
        if (!_instruments.TryRemove(instrument, out var aggregator))
        {
            return;
        }

        foreach (string id in aggregator.Close())
        {
            _adapter.RemoveMonitor(id);
        }
    }

    private static void Record(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        ((InstrumentAggregator)state!).Record(value, tags);
    }

    /// <param name="force">Collect even if the previous cycle is more recent than <see cref="MinCollectInterval"/></param>
    private void Collect(bool force)
    {
        lock (_collectGate)
        {
            if (IsDisposed)
            {
                return;
            }

            long now = _timeProvider.GetTimestamp();
            var elapsed = _timeProvider.GetElapsedTime(_lastCollectTimestamp, now);

            if (!force && !ReferenceEquals(_snapshot, EmptySnapshot) && elapsed < MinCollectInterval)
            {
                return;
            }

            var observables = _instruments.Values.Where(aggregator => aggregator.IsObservable).ToArray();

            foreach (var aggregator in observables)
            {
                aggregator.BeginObservation();
            }

            _listener.RecordObservableInstruments();

            var values = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var aggregator in _instruments.Values)
            {
                if (aggregator.IsObservable)
                {
                    aggregator.EndObservation();
                }

                aggregator.Collect(elapsed.TotalSeconds, values);
            }

            _lastCollectTimestamp = now;
            Volatile.Write(ref _snapshot, values);
        }
    }
}

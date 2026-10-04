namespace Gosferano.Godot.Metrics;

/// <summary>
/// Configures which meters are shown and how their instruments map to monitors
/// </summary>
public sealed class GodotMetricsOptions
{
    private readonly List<string> _meterPatterns = [];
    private readonly Dictionary<string, string[]> _splits = new(StringComparer.Ordinal);

    internal IReadOnlyList<string> MeterPatterns => _meterPatterns;

    internal IReadOnlyDictionary<string, string[]> Splits => _splits;

    internal bool CounterTotals { get; private set; }

    /// <summary>
    /// Includes every meter whose name matches the pattern
    /// </summary>
    /// <param name="pattern">Meter name; <c>*</c> matches any sequence of characters (case-insensitive)</param>
    public GodotMetricsOptions IncludeMeter(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        _meterPatterns.Add(pattern);
        return this;
    }

    /// <summary>
    /// Shows one monitor set per distinct value of the given tag keys instead of merging all tags.
    /// Tags not listed are still merged.
    /// </summary>
    /// <param name="instrument">Instrument name (in any included meter)</param>
    /// <param name="tagKeys">Tag keys to split by</param>
    public GodotMetricsOptions SplitBy(string instrument, params string[] tagKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instrument);
        ArgumentNullException.ThrowIfNull(tagKeys);

        if (tagKeys.Length == 0 || tagKeys.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty tag key is required", nameof(tagKeys));
        }

        _splits[instrument] = tagKeys.Distinct(StringComparer.Ordinal).ToArray();
        return this;
    }

    /// <summary>
    /// Adds a running total monitor next to the per-second rate for counters
    /// </summary>
    public GodotMetricsOptions WithCounterTotals()
    {
        CounterTotals = true;
        return this;
    }
}

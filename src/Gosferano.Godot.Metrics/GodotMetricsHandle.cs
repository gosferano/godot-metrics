namespace Gosferano.Godot.Metrics;

/// <summary>
/// Handle for enabled metrics. Disposing it removes every registered monitor and stops listening.
/// </summary>
public sealed class GodotMetricsHandle : IDisposable
{
    private readonly MetricsCollector _collector;

    internal GodotMetricsHandle(MetricsCollector collector)
    {
        _collector = collector;
    }

    /// <summary>
    /// Returns the current value of every monitor, keyed by monitor id.
    /// Values are in the instrument's own unit (e.g. milliseconds stay milliseconds).
    /// Works in exported builds, without the editor debugger attached.
    /// </summary>
    public IReadOnlyDictionary<string, double> GetSnapshot()
    {
        return _collector.GetSnapshot();
    }

    /// <summary>
    /// Removes every registered monitor and disposes the underlying meter listener
    /// </summary>
    public void Dispose()
    {
        _collector.Dispose();
    }
}

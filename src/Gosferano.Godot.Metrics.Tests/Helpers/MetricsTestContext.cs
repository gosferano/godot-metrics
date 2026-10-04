using System.Diagnostics.Metrics;
using Gosferano.Godot.Metrics.Tests.Mocks;

namespace Gosferano.Godot.Metrics.Tests.Helpers;

/// <summary>
/// A meter with a unique name plus a collector listening to it through fakes.
/// Unique names keep tests isolated, since meter listeners see every meter in the process.
/// </summary>
internal sealed class MetricsTestContext : IDisposable
{
    public MetricsTestContext(Action<GodotMetricsOptions>? configure = null)
    {
        MeterName = UniqueMeterName();
        Meter = new Meter(MeterName);

        var options = new GodotMetricsOptions().IncludeMeter(MeterName);
        configure?.Invoke(options);

        Collector = new MetricsCollector(options, Adapter, Logger, Time);
    }

    public string MeterName { get; }

    public Meter Meter { get; }

    public FakeMonitorAdapter Adapter { get; } = new();

    public FakeTimeProvider Time { get; } = new();

    public FakeLogger Logger { get; } = new();

    public MetricsCollector Collector { get; }

    public static string UniqueMeterName()
    {
        return $"Tests.{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Monitor id of an instrument in this context's meter
    /// </summary>
    public string Id(string instrument, string suffix = "")
    {
        return $"{MeterName}/{instrument}{suffix}";
    }

    /// <summary>
    /// Advances the clock by one second and collects
    /// </summary>
    public IReadOnlyDictionary<string, double> NextSnapshot()
    {
        return NextSnapshot(TimeSpan.FromSeconds(1));
    }

    public IReadOnlyDictionary<string, double> NextSnapshot(TimeSpan elapsed)
    {
        Time.Advance(elapsed);
        return Collector.GetSnapshot();
    }

    public void Dispose()
    {
        Collector.Dispose();
        Meter.Dispose();
    }
}

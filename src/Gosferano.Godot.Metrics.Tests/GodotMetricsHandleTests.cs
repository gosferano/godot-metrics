using System.Diagnostics.Metrics;
using Gosferano.Godot.Metrics.Tests.Helpers;
using Gosferano.Godot.Metrics.Tests.Mocks;
using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class GodotMetricsHandleTests : IDisposable
{
    private readonly Meter _meter = new(MetricsTestContext.UniqueMeterName());
    private readonly FakeMonitorAdapter _adapter = new();
    private readonly FakeTimeProvider _time = new();

    public void Dispose()
    {
        _meter.Dispose();
    }

    [Fact]
    public void GetSnapshot_ReturnsCurrentValuesByMonitorId()
    {
        // Arrange
        using var handle = Enable();
        _meter.CreateUpDownCounter<int>("queue.length").Add(3);
        _meter.CreateHistogram<double>("tick.duration", "ms").Record(16);

        // Act
        var snapshot = handle.GetSnapshot();

        // Assert
        Assert.Equal(3, snapshot[$"{_meter.Name}/queue.length"]);
        Assert.Equal(16, snapshot[$"{_meter.Name}/tick.duration max"]);
    }

    [Fact]
    public void Dispose_RemovesEveryMonitor()
    {
        // Arrange
        var handle = Enable(o => o.SplitBy("system.duration", "system"));
        _meter.CreateCounter<int>("ticks.processed").Add(1);
        _meter.CreateObservableGauge("ecs.entities", () => 5);
        var histogram = _meter.CreateHistogram<double>("system.duration");
        histogram.Record(1, new KeyValuePair<string, object?>("system", "Movement"));
        histogram.Record(1, new KeyValuePair<string, object?>("system", "Combat"));
        var registered = _adapter.Monitors.Keys.ToArray();

        // Act
        handle.Dispose();

        // Assert
        Assert.Equal(1 + 1 + 10, registered.Length);
        Assert.Empty(_adapter.Monitors);
        Assert.Equal(registered.Order(), _adapter.Removed.Order());
    }

    [Fact]
    public void Dispose_StopsListening()
    {
        // Arrange
        var handle = Enable();
        var counter = _meter.CreateCounter<int>("before");

        // Act
        handle.Dispose();
        counter.Add(1);
        _meter.CreateCounter<int>("after").Add(1);

        // Assert
        Assert.Empty(_adapter.Monitors);
        Assert.Empty(handle.GetSnapshot());
    }

    [Fact]
    public void Dispose_Twice_DoesNotRemoveTwice()
    {
        // Arrange
        var handle = Enable();
        _meter.CreateCounter<int>("ticks").Add(1);

        // Act
        handle.Dispose();
        handle.Dispose();

        // Assert
        Assert.Single(_adapter.Removed);
    }

    private GodotMetricsHandle Enable(Action<GodotMetricsOptions>? configure = null)
    {
        return GodotMetrics.Enable(
            o =>
            {
                o.IncludeMeter(_meter.Name);
                configure?.Invoke(o);
            },
            _adapter,
            _time
        );
    }
}

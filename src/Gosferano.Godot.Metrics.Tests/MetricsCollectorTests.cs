using System.Diagnostics.Metrics;
using Gosferano.Godot.Metrics.Tests.Helpers;
using Gosferano.Godot.Metrics.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class MetricsCollectorTests
{
    [Fact]
    public void Counter_ReportsRatePerSecond()
    {
        // Arrange
        using var context = new MetricsTestContext();
        var counter = context.Meter.CreateCounter<long>("ticks.processed");

        // Act
        counter.Add(10);
        counter.Add(20);
        var snapshot = context.NextSnapshot(TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(15, snapshot[context.Id("ticks.processed", " rate")]);
        Assert.Equal([context.Id("ticks.processed", " rate")], context.Adapter.Monitors.Keys);
    }

    [Fact]
    public void Counter_WithCounterTotals_AlsoReportsRunningTotal()
    {
        // Arrange
        using var context = new MetricsTestContext(o => o.WithCounterTotals());
        var counter = context.Meter.CreateCounter<int>("ticks.processed");

        // Act
        counter.Add(3);
        context.NextSnapshot();
        counter.Add(4);
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal(4, snapshot[context.Id("ticks.processed", " rate")]);
        Assert.Equal(7, snapshot[context.Id("ticks.processed", " total")]);
    }

    [Fact]
    public void UpDownCounter_ReportsCurrentSum()
    {
        // Arrange
        using var context = new MetricsTestContext();
        var counter = context.Meter.CreateUpDownCounter<int>("queue.length");

        // Act
        counter.Add(5);
        counter.Add(-2);
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal(3, snapshot[context.Id("queue.length")]);
    }

    [Fact]
    public void Gauge_RecognizedByName_ReportsLastValue()
    {
        // Arrange
        using var context = new MetricsTestContext();
        var gauge = new Gauge<double>(context.Meter, "zone.temperature");

        // Act
        gauge.Record(12.5);
        gauge.Record(9);
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal(9, snapshot[context.Id("zone.temperature")]);
        Assert.Empty(context.Logger.Entries);
    }

    [Fact]
    public void UnknownInstrument_FallsBackToLastValueAndWarnsOncePerType()
    {
        // Arrange
        using var context = new MetricsTestContext();
        var first = new UnknownInstrument<int>(context.Meter, "custom.a");
        _ = new UnknownInstrument<int>(context.Meter, "custom.b");

        // Act
        first.Record(4);
        first.Record(2);
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal(2, snapshot[context.Id("custom.a")]);
        Assert.Equal(0, snapshot[context.Id("custom.b")]);
        var entry = Assert.Single(context.Logger.Entries);
        Assert.Equal(typeof(UnknownInstrument<int>).FullName, entry.Properties["InstrumentType"]);
        Assert.Equal(context.Id("custom.a"), entry.Properties["Instrument"]);
    }

    [Fact]
    public void Histogram_ReportsWindowStatsAndScalesMillisecondsForGodot()
    {
        // Arrange
        using var context = new MetricsTestContext();
        var histogram = context.Meter.CreateHistogram<double>("tick.duration", "ms");

        // Act
        histogram.Record(250);
        var snapshot = context.NextSnapshot();
        var monitor = context.Adapter.Monitors[context.Id("tick.duration", " p95")];

        // Assert
        Assert.Equal(250, snapshot[context.Id("tick.duration", " p95")]);
        Assert.Equal(MonitorFormat.Time, monitor.Format);
        Assert.Equal(0.25, monitor.Read(), 1e-12);
    }

    [Fact]
    public void AllNumericTypes_AreRecorded()
    {
        // Arrange
        using var context = new MetricsTestContext();

        // Act
        context.Meter.CreateUpDownCounter<byte>("byte").Add(1);
        context.Meter.CreateUpDownCounter<short>("short").Add(2);
        context.Meter.CreateUpDownCounter<int>("int").Add(3);
        context.Meter.CreateUpDownCounter<long>("long").Add(4);
        context.Meter.CreateUpDownCounter<float>("float").Add(5.5f);
        context.Meter.CreateUpDownCounter<double>("double").Add(6.5);
        context.Meter.CreateUpDownCounter<decimal>("decimal").Add(7.5m);
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal(1, snapshot[context.Id("byte")]);
        Assert.Equal(2, snapshot[context.Id("short")]);
        Assert.Equal(3, snapshot[context.Id("int")]);
        Assert.Equal(4, snapshot[context.Id("long")]);
        Assert.Equal(5.5, snapshot[context.Id("float")]);
        Assert.Equal(6.5, snapshot[context.Id("double")]);
        Assert.Equal(7.5, snapshot[context.Id("decimal")]);
    }

    [Fact]
    public void ObservableGauge_IsPolledOncePerCycleAcrossMonitorsAndSnapshots()
    {
        // Arrange
        using var context = new MetricsTestContext();
        var calls = 0;
        context.Meter.CreateObservableGauge("ecs.entities", () => ++calls * 100);
        context.Meter.CreateObservableGauge("ecs.archetypes", () => 7);

        // Act: Godot polls every monitor back to back, and an overlay reads a snapshot in the same frame
        context.Time.Advance(TimeSpan.FromSeconds(1));
        double entities = context.Adapter.Read(context.Id("ecs.entities"));
        double archetypes = context.Adapter.Read(context.Id("ecs.archetypes"));
        var snapshot = context.Collector.GetSnapshot();

        // Assert
        Assert.Equal(1, calls);
        Assert.Equal(100, entities);
        Assert.Equal(7, archetypes);
        Assert.Equal(100, snapshot[context.Id("ecs.entities")]);

        // Act: next cycle
        context.Time.Advance(TimeSpan.FromSeconds(1));
        entities = context.Adapter.Read(context.Id("ecs.entities"));

        // Assert
        Assert.Equal(2, calls);
        Assert.Equal(200, entities);
    }

    [Fact]
    public void ObservableCounter_ReportsRateFromCumulativeValues()
    {
        // Arrange
        using var context = new MetricsTestContext(o => o.WithCounterTotals());
        long cumulative = 1000;
        context.Meter.CreateObservableCounter("gc.collections", () => cumulative);

        // Act & Assert: first poll is the baseline
        var snapshot = context.NextSnapshot();
        Assert.Equal(0, snapshot[context.Id("gc.collections", " rate")]);
        Assert.Equal(1000, snapshot[context.Id("gc.collections", " total")]);

        cumulative = 1060;
        snapshot = context.NextSnapshot(TimeSpan.FromSeconds(3));
        Assert.Equal(20, snapshot[context.Id("gc.collections", " rate")]);
        Assert.Equal(1060, snapshot[context.Id("gc.collections", " total")]);
    }

    [Fact]
    public void ObservableUpDownCounter_WhenMerged_SumsAcrossTags()
    {
        // Arrange
        using var context = new MetricsTestContext();
        context.Meter.CreateObservableUpDownCounter(
            "ecs.entities",
            () => new[]
            {
                new Measurement<int>(3, new KeyValuePair<string, object?>("archetype", "Tree")),
                new Measurement<int>(4, new KeyValuePair<string, object?>("archetype", "Rock")),
            }
        );

        // Act
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal(7, snapshot[context.Id("ecs.entities")]);
    }

    [Fact]
    public void SplitObservable_IsPolledOnMainThreadAfterPublishSoItsSeriesAppear()
    {
        // Arrange
        using var context = new MetricsTestContext(o => o.SplitBy("ecs.entities", "archetype"));
        context.Meter.CreateObservableGauge(
            "ecs.entities",
            () => new Measurement<int>(3, new KeyValuePair<string, object?>("archetype", "Tree"))
        );
        Assert.Empty(context.Adapter.Monitors);

        // Act
        context.Adapter.RunPosted();

        // Assert
        Assert.Equal([context.Id("ecs.entities", "{archetype=Tree}")], context.Adapter.Monitors.Keys);
        Assert.Equal(3, context.Adapter.Read(context.Id("ecs.entities", "{archetype=Tree}")));
    }

    [Fact]
    public void SplitBy_AppliesToNamedInstrumentOnly()
    {
        // Arrange
        using var context = new MetricsTestContext(o => o.SplitBy("system.duration", "system"));
        var split = context.Meter.CreateHistogram<double>("system.duration", "ms");
        var merged = context.Meter.CreateHistogram<double>("tick.duration", "ms");
        var tag = new KeyValuePair<string, object?>("system", "Movement");

        // Act
        split.Record(2, tag);
        merged.Record(2, tag);
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal(2, snapshot[context.Id("system.duration", "{system=Movement} p95")]);
        Assert.Equal(2, snapshot[context.Id("tick.duration", " p95")]);
    }

    [Fact]
    public void SplitHistogram_RecordedThroughMeterListener_DoesNotAllocate()
    {
        // Arrange
        using var context = new MetricsTestContext(o => o.SplitBy("system.duration", "system"));
        var histogram = context.Meter.CreateHistogram<double>("system.duration", "ms");
        var movement = new KeyValuePair<string, object?>("system", "Movement");
        histogram.Record(1, movement);

        // Act
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
        {
            histogram.Record(i, movement);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Snapshot_WithinMinCollectInterval_ReusesPreviousCycle()
    {
        // Arrange
        using var context = new MetricsTestContext();
        var counter = context.Meter.CreateUpDownCounter<int>("queue.length");
        var first = context.NextSnapshot();

        // Act
        counter.Add(5);
        var second = context.NextSnapshot(MetricsCollector.MinCollectInterval / 2);
        var third = context.NextSnapshot(MetricsCollector.MinCollectInterval);

        // Assert
        Assert.Same(first, second);
        Assert.Equal(5, third[context.Id("queue.length")]);
    }

    [Fact]
    public void NonMatchingMeter_IsIgnored()
    {
        // Arrange
        using var context = new MetricsTestContext();
        using var other = new Meter(MetricsTestContext.UniqueMeterName());

        // Act
        other.CreateCounter<int>("ignored").Add(1);
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Empty(snapshot);
        Assert.Empty(context.Adapter.Monitors);
    }

    [Fact]
    public void InstrumentCreatedBeforeCollector_IsPickedUp()
    {
        // Arrange
        using var meter = new Meter(MetricsTestContext.UniqueMeterName());
        var counter = meter.CreateUpDownCounter<int>("early");
        var adapter = new FakeMonitorAdapter();

        // Act
        using var collector = new MetricsCollector(
            new GodotMetricsOptions().IncludeMeter(meter.Name),
            adapter,
            NullLogger.Instance,
            new FakeTimeProvider()
        );
        counter.Add(2);

        // Assert
        Assert.Equal(2, collector.GetSnapshot()[$"{meter.Name}/early"]);
    }

    [Fact]
    public void MeterDispose_RemovesItsMonitors()
    {
        // Arrange
        using var context = new MetricsTestContext();
        using var second = new Meter(context.MeterName);
        context.Meter.CreateCounter<int>("kept").Add(1);
        second.CreateCounter<int>("dropped").Add(1);

        // Act
        second.Dispose();
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal([context.Id("dropped", " rate")], context.Adapter.Removed);
        Assert.Equal([context.Id("kept", " rate")], context.Adapter.Monitors.Keys);
        Assert.False(snapshot.ContainsKey(context.Id("dropped", " rate")));
    }

    [Fact]
    public void Measurements_FromManyThreads_AreAllCountedAndSeriesRegisteredOnce()
    {
        // Arrange
        using var context = new MetricsTestContext(o => o.SplitBy("jobs", "worker"));
        var counter = context.Meter.CreateUpDownCounter<int>("jobs");

        // Act
        Parallel.For(
            0,
            40_000,
            i => counter.Add(1, new KeyValuePair<string, object?>("worker", i % 4))
        );
        var snapshot = context.NextSnapshot();

        // Assert
        Assert.Equal(4, context.Adapter.Monitors.Count);
        Assert.Empty(context.Adapter.DuplicateAdds);
        Assert.All(Enumerable.Range(0, 4), w => Assert.Equal(10_000, snapshot[context.Id("jobs", $"{{worker={w}}}")]));
    }
}

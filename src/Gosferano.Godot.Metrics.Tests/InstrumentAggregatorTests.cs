using System.Diagnostics.Metrics;
using Gosferano.Godot.Metrics.Tests.Helpers;
using Gosferano.Godot.Metrics.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class InstrumentAggregatorTests : IDisposable
{
    private readonly Meter _meter = new(MetricsTestContext.UniqueMeterName());
    private readonly FakeMonitorAdapter _adapter = new();
    private readonly FakeLogger _logger = new();

    public void Dispose()
    {
        _meter.Dispose();
    }

    [Fact]
    public void BaseId_IsMeterSlashInstrument()
    {
        // Arrange
        var aggregator = Create(_meter.CreateCounter<int>("ticks.processed"), InstrumentKind.Counter);

        // Act & Assert
        Assert.Equal($"{_meter.Name}/ticks.processed", aggregator.BaseId);
    }

    [Fact]
    public void BaseId_ReplacesSlashesSoGodotKeepsTheMeterCategory()
    {
        // Arrange
        using var meter = new Meter("Game/Sim");
        var aggregator = Create(meter.CreateCounter<int>("a/b"), InstrumentKind.Counter);

        // Act & Assert
        Assert.Equal("Game_Sim/a_b", aggregator.BaseId);
    }

    [Fact]
    public void Initialize_WhenMerged_RegistersMonitorsBeforeFirstMeasurement()
    {
        // Arrange
        var aggregator = Create(_meter.CreateHistogram<double>("tick.duration", "ms"), InstrumentKind.Histogram);

        // Act
        aggregator.Initialize();

        // Assert
        string[] suffixes = [" last", " avg", " p95", " max", " count"];
        var expected = suffixes.Select(suffix => $"{_meter.Name}/tick.duration{suffix}").Order();
        Assert.Equal(expected, _adapter.Monitors.Keys.Order());
    }

    [Fact]
    public void Initialize_WhenSplit_RegistersNothingUntilMeasured()
    {
        // Arrange
        var aggregator = Create(_meter.CreateCounter<int>("hits"), InstrumentKind.Counter, ["system"]);

        // Act
        aggregator.Initialize();

        // Assert
        Assert.Empty(_adapter.Monitors);
    }

    [Fact]
    public void Record_ByDefault_MergesAllTagsIntoOneSeries()
    {
        // Arrange
        var aggregator = Create(_meter.CreateHistogram<double>("system.duration"), InstrumentKind.Histogram);

        // Act
        aggregator.Record(1, [Tag("system", "Movement")]);
        aggregator.Record(3, [Tag("system", "Combat")]);
        var values = Collect(aggregator);

        // Assert
        Assert.Equal(5, _adapter.Monitors.Count);
        Assert.Equal(2, values[$"{aggregator.BaseId} count"]);
        Assert.Equal(2, values[$"{aggregator.BaseId} avg"]);
    }

    [Fact]
    public void Record_WithSplitKey_CreatesOneSeriesPerTagValue()
    {
        // Arrange
        var aggregator = Create(
            _meter.CreateHistogram<double>("system.duration"),
            InstrumentKind.Histogram,
            ["system"]
        );

        // Act
        aggregator.Record(1, [Tag("system", "Movement")]);
        aggregator.Record(5, [Tag("system", "Combat")]);
        aggregator.Record(3, [Tag("system", "Movement")]);
        var values = Collect(aggregator);

        // Assert
        Assert.Equal(10, _adapter.Monitors.Count);
        Assert.Equal(3, values[$"{aggregator.BaseId}{{system=Movement}} max"]);
        Assert.Equal(2, values[$"{aggregator.BaseId}{{system=Movement}} count"]);
        Assert.Equal(5, values[$"{aggregator.BaseId}{{system=Combat}} p95"]);
    }

    [Fact]
    public void Record_WithSplitKey_StillMergesOtherTags()
    {
        // Arrange
        var aggregator = Create(_meter.CreateUpDownCounter<int>("queue"), InstrumentKind.UpDownCounter, ["zone"]);

        // Act
        aggregator.Record(2, [Tag("zone", "A"), Tag("priority", "high")]);
        aggregator.Record(3, [Tag("priority", "low"), Tag("zone", "A")]);
        var values = Collect(aggregator);

        // Assert
        Assert.Single(_adapter.Monitors);
        Assert.Equal(5, values[$"{aggregator.BaseId}{{zone=A}}"]);
    }

    [Fact]
    public void Record_WithMultipleSplitKeys_UsesConfiguredKeyOrder()
    {
        // Arrange
        var aggregator = Create(_meter.CreateCounter<int>("hits"), InstrumentKind.Counter, ["system", "phase"]);

        // Act
        aggregator.Record(1, [Tag("phase", 2), Tag("system", "AI")]);

        // Assert
        Assert.Equal([$"{aggregator.BaseId}{{system=AI,phase=2}} rate"], _adapter.Monitors.Keys);
    }

    [Fact]
    public void Record_WithoutAnySplitKeyPresent_UsesUntaggedSeries()
    {
        // Arrange
        var aggregator = Create(_meter.CreateUpDownCounter<int>("queue"), InstrumentKind.UpDownCounter, ["zone"]);

        // Act
        aggregator.Record(4, [Tag("priority", "low")]);
        aggregator.Record(1, []);
        var values = Collect(aggregator);

        // Assert
        Assert.Equal(5, values[aggregator.BaseId]);
    }

    [Fact]
    public void Record_WithSlashInTagValue_SanitizesId()
    {
        // Arrange
        var aggregator = Create(_meter.CreateUpDownCounter<int>("loaded"), InstrumentKind.UpDownCounter, ["path"]);

        // Act
        aggregator.Record(1, [Tag("path", "res://a/b")]);

        // Assert
        Assert.Equal([$"{aggregator.BaseId}{{path=res:__a_b}}"], _adapter.Monitors.Keys);
    }

    [Fact]
    public void Record_PastSeriesCap_MergesIntoOtherAndWarnsOnce()
    {
        // Arrange
        var aggregator = Create(_meter.CreateUpDownCounter<int>("per.entity"), InstrumentKind.UpDownCounter, ["id"]);
        const int extra = 50;

        // Act
        for (var i = 0; i < InstrumentAggregator.MaxSeries + extra; i++)
        {
            aggregator.Record(1, [Tag("id", i)]);
        }

        aggregator.Record(1, [Tag("id", 0)]);
        var values = Collect(aggregator);

        // Assert
        Assert.Equal(InstrumentAggregator.MaxSeries + 1, _adapter.Monitors.Count);
        Assert.Equal(extra, values[$"{aggregator.BaseId}{{other}}"]);
        Assert.Equal(2, values[$"{aggregator.BaseId}{{id=0}}"]);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(aggregator.BaseId, entry.Properties["Instrument"]);
        Assert.Equal(InstrumentAggregator.MaxSeries, entry.Properties["MaxSeries"]);
        Assert.Equal("{other}", entry.Properties["OtherSeries"]);
        Assert.Contains("{other}", entry.Message);
    }

    [Fact]
    public void Close_ReturnsAllIdsAndStopsRegisteringSeries()
    {
        // Arrange
        var aggregator = Create(_meter.CreateCounter<int>("hits"), InstrumentKind.Counter, ["system"]);
        aggregator.Record(1, [Tag("system", "A")]);
        aggregator.Record(1, [Tag("system", "B")]);

        // Act
        var ids = aggregator.Close();
        aggregator.Record(1, [Tag("system", "C")]);

        // Assert
        Assert.Equal(2, ids.Count);
        Assert.Equal(2, _adapter.Monitors.Count);
        Assert.DoesNotContain(_adapter.Monitors.Keys, id => id.Contains("system=C"));
    }

    [Fact]
    public void Monitors_UseUnitFormatAndScaleExceptForCounts()
    {
        // Arrange
        var aggregator = Create(
            _meter.CreateHistogram<double>("tick.duration", "ms"),
            InstrumentKind.Histogram,
            readValue: _ => 250
        );

        // Act
        aggregator.Initialize();
        var p95 = _adapter.Monitors[$"{aggregator.BaseId} p95"];
        var count = _adapter.Monitors[$"{aggregator.BaseId} count"];

        // Assert
        Assert.Equal(MonitorFormat.Time, p95.Format);
        Assert.Equal(0.25, p95.Read(), 1e-12);
        Assert.Equal(MonitorFormat.Quantity, count.Format);
        Assert.Equal(250, count.Read());
    }

    private InstrumentAggregator Create(
        Instrument instrument,
        InstrumentKind kind,
        string[]? splitKeys = null,
        Func<string, double>? readValue = null
    )
    {
        return new InstrumentAggregator(
            instrument,
            kind,
            splitKeys ?? [],
            counterTotals: false,
            _adapter,
            _logger,
            readValue ?? (_ => 0)
        );
    }

    private static Dictionary<string, double> Collect(InstrumentAggregator aggregator)
    {
        var values = new Dictionary<string, double>();
        aggregator.Collect(1, values);
        return values;
    }

    private static KeyValuePair<string, object?> Tag(string key, object? value)
    {
        return new KeyValuePair<string, object?>(key, value);
    }
}

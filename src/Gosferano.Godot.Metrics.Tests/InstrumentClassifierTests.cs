using System.Diagnostics.Metrics;
using Gosferano.Godot.Metrics.Tests.Helpers;
using Gosferano.Godot.Metrics.Tests.Mocks;
using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class InstrumentClassifierTests
{
    [Fact]
    public void Classify_WithBuiltInInstruments_ReturnsMatchingKind()
    {
        // Arrange
        using var meter = new Meter(MetricsTestContext.UniqueMeterName());

        // Act & Assert
        Assert.Equal(InstrumentKind.Counter, InstrumentClassifier.Classify(meter.CreateCounter<long>("a")));
        Assert.Equal(InstrumentKind.UpDownCounter, InstrumentClassifier.Classify(meter.CreateUpDownCounter<int>("b")));
        Assert.Equal(InstrumentKind.Histogram, InstrumentClassifier.Classify(meter.CreateHistogram<double>("c")));
        Assert.Equal(
            InstrumentKind.ObservableCounter,
            InstrumentClassifier.Classify(meter.CreateObservableCounter("d", () => 1L))
        );
        Assert.Equal(
            InstrumentKind.ObservableUpDownCounter,
            InstrumentClassifier.Classify(meter.CreateObservableUpDownCounter("e", () => 1))
        );
        Assert.Equal(
            InstrumentKind.ObservableGauge,
            InstrumentClassifier.Classify(meter.CreateObservableGauge("f", () => 1d))
        );
    }

    [Fact]
    public void Classify_WithGaugeFromNewerDiagnosticSource_IsRecognizedByName()
    {
        // Arrange
        using var meter = new Meter(MetricsTestContext.UniqueMeterName());
        var gauge = new Gauge<int>(meter, "gauge");

        // Act
        var kind = InstrumentClassifier.Classify(gauge);

        // Assert
        Assert.Equal(InstrumentKind.Gauge, kind);
    }

    [Fact]
    public void Classify_WithCustomInstrument_ReturnsUnknown()
    {
        // Arrange
        using var meter = new Meter(MetricsTestContext.UniqueMeterName());
        var instrument = new UnknownInstrument<int>(meter, "custom");

        // Act
        var kind = InstrumentClassifier.Classify(instrument);

        // Assert
        Assert.Equal(InstrumentKind.Unknown, kind);
    }
}

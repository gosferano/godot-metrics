using System.Diagnostics.Metrics;
using Gosferano.Godot.Metrics.Tests.Helpers;
using Gosferano.Godot.Metrics.Tests.Mocks;
using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class GodotMetricsTests
{
    [Fact]
    public void Enable_WithNullConfigure_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            GodotMetrics.Enable(null!, new FakeMonitorAdapter(), new FakeTimeProvider())
        );
    }

    [Fact]
    public void Enable_WithoutIncludeMeter_Throws()
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GodotMetrics.Enable(_ => { }, new FakeMonitorAdapter(), new FakeTimeProvider())
        );
        Assert.Contains("IncludeMeter", exception.Message);
    }

    [Fact]
    public void Enable_WithWildcard_RegistersMonitorsForMatchingMeters()
    {
        // Arrange
        string prefix = MetricsTestContext.UniqueMeterName();
        using var simulation = new Meter($"{prefix}.Simulation");
        using var ecs = new Meter($"{prefix}.Ecs");
        var adapter = new FakeMonitorAdapter();

        // Act
        using var handle = GodotMetrics.Enable(
            o => o.IncludeMeter($"{prefix}.*"),
            adapter,
            new FakeTimeProvider()
        );
        simulation.CreateCounter<int>("ticks.processed");
        ecs.CreateObservableGauge("ecs.entities", () => 1);

        // Assert
        Assert.Equal(
            [$"{prefix}.Ecs/ecs.entities", $"{prefix}.Simulation/ticks.processed rate"],
            adapter.Monitors.Keys.Order()
        );
    }
}

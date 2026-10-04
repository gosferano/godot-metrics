using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class LastValueSeriesTests
{
    [Fact]
    public void Collect_WithoutMeasurements_ReportsZero()
    {
        // Arrange
        var series = new LastValueSeries();
        var values = new double[1];

        // Act
        series.Collect(1, values);

        // Assert
        Assert.Equal(0, values[0]);
    }

    [Fact]
    public void Collect_AfterMeasurements_ReportsMostRecent()
    {
        // Arrange
        var series = new LastValueSeries();
        var values = new double[1];

        // Act
        series.Record(10);
        series.Record(-2.5);
        series.Collect(1, values);

        // Assert
        Assert.Equal(-2.5, values[0]);
        Assert.Equal([""], series.Stats.Select(stat => stat.Suffix));
    }
}

using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class HistogramSeriesTests
{
    [Fact]
    public void Stats_AreLastAvgP95MaxCount()
    {
        // Arrange
        var series = new HistogramSeries();

        // Act
        var stats = series.Stats;

        // Assert
        Assert.Equal([" last", " avg", " p95", " max", " count"], stats.Select(stat => stat.Suffix));
        Assert.False(stats[4].UsesInstrumentUnit);
    }

    [Fact]
    public void Collect_WithoutMeasurements_ReportsZeros()
    {
        // Arrange
        var series = new HistogramSeries();
        var values = new double[5];

        // Act
        series.Collect(1, values);

        // Assert
        Assert.Equal([0d, 0d, 0d, 0d, 0d], values);
    }

    [Fact]
    public void Collect_WithMeasurements_ReportsWindowStats()
    {
        // Arrange
        var series = new HistogramSeries();
        var values = new double[5];

        // Record 1..100 out of order, so "last" differs from "max"
        foreach (int value in Enumerable.Range(1, 100).Reverse())
        {
            series.Record(value);
        }

        // Act
        series.Collect(1, values);

        // Assert
        Assert.Equal(1, values[0]);
        Assert.Equal(50.5, values[1], 1e-9);
        Assert.Equal(95, values[2]);
        Assert.Equal(100, values[3]);
        Assert.Equal(100, values[4]);
    }

    [Fact]
    public void Collect_PastWindowSize_UsesMostRecentMeasurementsButLifetimeCount()
    {
        // Arrange
        var series = new HistogramSeries();
        var values = new double[5];

        for (var i = 0; i < 1000; i++)
        {
            series.Record(i);
        }

        // Act
        series.Collect(1, values);

        // Assert: window holds 744..999
        int first = 1000 - HistogramSeries.WindowSize;
        Assert.Equal(999, values[0]);
        Assert.Equal((first + 999) / 2d, values[1], 1e-9);
        Assert.Equal(first + 243, values[2]);
        Assert.Equal(999, values[3]);
        Assert.Equal(1000, values[4]);
    }

    [Fact]
    public void Record_FromManyThreads_CountsEveryMeasurement()
    {
        // Arrange
        var series = new HistogramSeries();
        var values = new double[5];

        // Act
        Parallel.For(0, 50_000, i => series.Record(i % 10));
        series.Collect(1, values);

        // Assert
        Assert.Equal(50_000, values[4]);
        Assert.Equal(9, values[3]);
    }
}

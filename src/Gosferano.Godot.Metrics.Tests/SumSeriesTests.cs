using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class SumSeriesTests
{
    [Fact]
    public void Constructor_WithNoOutputs_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SumSeries(observable: false, reportRate: false, reportTotal: false));
    }

    [Fact]
    public void Stats_ForCounterWithTotals_AreRateThenTotal()
    {
        // Arrange
        var series = new SumSeries(observable: false, reportRate: true, reportTotal: true);

        // Act
        var suffixes = series.Stats.Select(stat => stat.Suffix);

        // Assert
        Assert.Equal([" rate", " total"], suffixes);
    }

    [Fact]
    public void Stats_ForUpDownCounter_IsSingleUnsuffixedValue()
    {
        // Arrange
        var series = new SumSeries(observable: false, reportRate: false, reportTotal: true);

        // Act
        var suffixes = series.Stats.Select(stat => stat.Suffix);

        // Assert
        Assert.Equal([""], suffixes);
    }

    [Fact]
    public void Collect_Synchronous_ReportsRatePerSecondAndRunningTotal()
    {
        // Arrange
        var series = new SumSeries(observable: false, reportRate: true, reportTotal: true);
        var values = new double[2];

        // Act & Assert
        series.Record(4);
        series.Record(6);
        series.Collect(2, values);
        Assert.Equal([5d, 10d], values);

        series.Record(3);
        series.Collect(0.5, values);
        Assert.Equal([6d, 13d], values);
    }

    [Fact]
    public void Collect_WithZeroElapsed_ReportsZeroRate()
    {
        // Arrange
        var series = new SumSeries(observable: false, reportRate: true, reportTotal: false);
        var values = new double[1];
        series.Record(5);

        // Act
        series.Collect(0, values);

        // Assert
        Assert.Equal(0, values[0]);
    }

    [Fact]
    public void Collect_UpDown_ReportsCurrentSumIncludingNegatives()
    {
        // Arrange
        var series = new SumSeries(observable: false, reportRate: false, reportTotal: true);
        var values = new double[1];

        // Act
        series.Record(5);
        series.Record(-8);
        series.Collect(1, values);

        // Assert
        Assert.Equal(-3, values[0]);
    }

    [Fact]
    public void Collect_Observable_FirstPollEstablishesBaselineThenReportsDelta()
    {
        // Arrange
        var series = new SumSeries(observable: true, reportRate: true, reportTotal: true);
        var values = new double[2];

        // Act & Assert
        Observe(series, 100);
        series.Collect(1, values);
        Assert.Equal([0d, 100d], values);

        Observe(series, 130);
        series.Collect(2, values);
        Assert.Equal([15d, 130d], values);
    }

    [Fact]
    public void Collect_Observable_SumsObservationsWithinOnePoll()
    {
        // Arrange
        var series = new SumSeries(observable: true, reportRate: false, reportTotal: true);
        var values = new double[1];

        // Act
        Observe(series, 3, 4);
        series.Collect(1, values);

        // Assert
        Assert.Equal(7, values[0]);
    }

    [Fact]
    public void Collect_ObservableWithoutObservation_KeepsPreviousTotal()
    {
        // Arrange
        var series = new SumSeries(observable: true, reportRate: false, reportTotal: true);
        var values = new double[1];
        Observe(series, 9);
        series.Collect(1, values);

        // Act
        Observe(series);
        series.Collect(1, values);

        // Assert
        Assert.Equal(9, values[0]);
    }

    [Fact]
    public void Record_FromManyThreads_CountsEveryMeasurement()
    {
        // Arrange
        var series = new SumSeries(observable: false, reportRate: false, reportTotal: true);
        var values = new double[1];

        // Act
        Parallel.For(0, 100_000, _ => series.Record(1));
        series.Collect(1, values);

        // Assert
        Assert.Equal(100_000, values[0]);
    }

    private static void Observe(SumSeries series, params double[] observations)
    {
        series.BeginObservation();

        foreach (double observation in observations)
        {
            series.Record(observation);
        }

        series.EndObservation();
    }
}

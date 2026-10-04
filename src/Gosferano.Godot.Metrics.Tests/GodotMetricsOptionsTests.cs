using Gosferano.Godot.Metrics.Tests.Mocks;
using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class GodotMetricsOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void IncludeMeter_WithBlankPattern_Throws(string pattern)
    {
        // Arrange
        var options = new GodotMetricsOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.IncludeMeter(pattern));
    }

    [Fact]
    public void IncludeMeter_AddsPatternsInOrder()
    {
        // Arrange
        var options = new GodotMetricsOptions();

        // Act
        options.IncludeMeter("Chronoscape.*").IncludeMeter("Audio");

        // Assert
        Assert.Equal(["Chronoscape.*", "Audio"], options.MeterPatterns);
    }

    [Fact]
    public void SplitBy_WithoutKeys_Throws()
    {
        // Arrange
        var options = new GodotMetricsOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.SplitBy("system.duration"));
    }

    [Fact]
    public void SplitBy_WithBlankKey_Throws()
    {
        // Arrange
        var options = new GodotMetricsOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.SplitBy("system.duration", "system", " "));
    }

    [Fact]
    public void SplitBy_RemovesDuplicateKeysAndLastCallWins()
    {
        // Arrange
        var options = new GodotMetricsOptions();

        // Act
        options.SplitBy("system.duration", "phase");
        options.SplitBy("system.duration", "system", "system", "phase");

        // Assert
        Assert.Equal(["system", "phase"], options.Splits["system.duration"]);
    }

    [Fact]
    public void WithCounterTotals_EnablesTotals()
    {
        // Arrange
        var options = new GodotMetricsOptions();

        // Act
        options.WithCounterTotals();

        // Assert
        Assert.True(options.CounterTotals);
    }

    [Fact]
    public void UseLoggerFactory_WithNull_Throws()
    {
        // Arrange
        var options = new GodotMetricsOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => options.UseLoggerFactory(null!));
    }

    [Fact]
    public void UseLoggerFactory_StoresFactory()
    {
        // Arrange
        var options = new GodotMetricsOptions();
        var factory = new FakeLoggerFactory();

        // Act
        options.UseLoggerFactory(factory);

        // Assert
        Assert.Same(factory, options.LoggerFactory);
    }

    [Fact]
    public void FluentMethods_ReturnSameInstance()
    {
        // Arrange
        var options = new GodotMetricsOptions();

        // Act & Assert
        Assert.Same(options, options.IncludeMeter("A"));
        Assert.Same(options, options.SplitBy("b", "c"));
        Assert.Same(options, options.WithCounterTotals());
        Assert.Same(options, options.UseLoggerFactory(new FakeLoggerFactory()));
    }
}

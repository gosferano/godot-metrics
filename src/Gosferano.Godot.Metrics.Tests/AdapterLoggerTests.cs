using Gosferano.Godot.Metrics.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class AdapterLoggerTests
{
    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, true)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    [InlineData(LogLevel.None, false)]
    public void IsEnabled_OnlyForWarningsAndAbove(LogLevel level, bool expected)
    {
        // Arrange
        var logger = new AdapterLogger(new FakeMonitorAdapter());

        // Act & Assert
        Assert.Equal(expected, logger.IsEnabled(level));
    }

    [Fact]
    public void Log_ForwardsFormattedMessageWithLevel()
    {
        // Arrange
        var adapter = new FakeMonitorAdapter();
        var logger = new AdapterLogger(adapter);

        // Act
        logger.LogWarning("{Instrument} exceeded {MaxSeries}", "Game/hits", 100);
        logger.LogError("Failed");

        // Assert
        Assert.Equal([(LogLevel.Warning, "Game/hits exceeded 100"), (LogLevel.Error, "Failed")], adapter.Logs);
    }

    [Fact]
    public void Log_BelowWarning_IsDropped()
    {
        // Arrange
        var adapter = new FakeMonitorAdapter();
        var logger = new AdapterLogger(adapter);

        // Act
        logger.LogInformation("Ignored");

        // Assert
        Assert.Empty(adapter.Logs);
    }

    [Fact]
    public void Log_WithException_AppendsIt()
    {
        // Arrange
        var adapter = new FakeMonitorAdapter();
        var logger = new AdapterLogger(adapter);

        // Act
        logger.LogError(new InvalidOperationException("boom"), "Failed");

        // Assert
        var (_, message) = Assert.Single(adapter.Logs);
        Assert.StartsWith("Failed", message);
        Assert.Contains("boom", message);
    }
}

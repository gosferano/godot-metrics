using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class MonitorUnitsTests
{
    [Theory]
    [InlineData("s", "Time", 1d)]
    [InlineData("ms", "Time", 1e-3)]
    [InlineData("us", "Time", 1e-6)]
    [InlineData("μs", "Time", 1e-6)]
    [InlineData("ns", "Time", 1e-9)]
    [InlineData("By", "Memory", 1d)]
    [InlineData("KiBy", "Memory", 1024d)]
    [InlineData("MiBy", "Memory", 1048576d)]
    [InlineData("%", "Percentage", 0.01)]
    [InlineData("{entity}", "Quantity", 1d)]
    [InlineData("", "Quantity", 1d)]
    [InlineData(null, "Quantity", 1d)]
    public void Resolve_WithUnit_ReturnsFormatAndScale(string? unit, string format, double scale)
    {
        // Act
        var resolved = MonitorUnits.Resolve(unit);

        // Assert
        Assert.Equal(Enum.Parse<MonitorFormat>(format), resolved.Format);
        Assert.Equal(scale, resolved.Scale, 1e-15);
    }
}

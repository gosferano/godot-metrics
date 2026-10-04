using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class MeterFilterTests
{
    [Theory]
    [InlineData("Chronoscape.Simulation", "Chronoscape.Simulation", true)]
    [InlineData("Chronoscape.Simulation", "Chronoscape.Simulation.Ecs", false)]
    [InlineData("Chronoscape.*", "Chronoscape.Simulation", true)]
    [InlineData("Chronoscape.*", "chronoscape.simulation", true)]
    [InlineData("Chronoscape.*", "Chronoscape", false)]
    [InlineData("*.Ecs", "Chronoscape.Simulation.Ecs", true)]
    [InlineData("Chronoscape.*.Ecs", "Chronoscape.Simulation.Ecs", true)]
    [InlineData("*", "System.Runtime", true)]
    [InlineData("Game+Mod", "Game+Mod", true)]
    [InlineData("Game+Mod", "GameeMod", false)]
    public void Matches_WithPattern_ReturnsExpected(string pattern, string meterName, bool expected)
    {
        // Arrange
        var filter = new MeterFilter([pattern]);

        // Act
        bool matches = filter.Matches(meterName);

        // Assert
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void Matches_WithMultiplePatterns_MatchesAny()
    {
        // Arrange
        var filter = new MeterFilter(["Chronoscape.*", "Audio"]);

        // Act & Assert
        Assert.True(filter.Matches("Chronoscape.Simulation"));
        Assert.True(filter.Matches("Audio"));
        Assert.False(filter.Matches("Network"));
    }

    [Fact]
    public void Matches_WithNoPatterns_MatchesNothing()
    {
        // Arrange
        var filter = new MeterFilter([]);

        // Act & Assert
        Assert.False(filter.Matches("Chronoscape.Simulation"));
    }
}

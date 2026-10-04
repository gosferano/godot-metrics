using Xunit;

namespace Gosferano.Godot.Metrics.Tests;

public class SplitKeyTests
{
    [Fact]
    public void Equals_WithSameValuesInDifferentArrays_IsTrue()
    {
        // Arrange
        var first = new SplitKey(["Movement", 2], 2);
        var second = new SplitKey(["Movement", 2], 2);

        // Act & Assert
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_ComparesOnlyTheGivenLength()
    {
        // Arrange: a reused scratch array may hold leftovers past the length
        var scratch = new SplitKey(["Movement", "leftover"], 1);
        var exact = new SplitKey(["Movement"], 1);

        // Act & Assert
        Assert.True(scratch.Equals(exact));
        Assert.Equal(scratch.GetHashCode(), exact.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentValues_IsFalse()
    {
        // Arrange
        var movement = new SplitKey(["Movement"], 1);
        var combat = new SplitKey(["Combat"], 1);

        // Act & Assert
        Assert.False(movement.Equals(combat));
    }

    [Fact]
    public void Equals_DistinguishesMissingFromNull()
    {
        // Arrange
        var missing = new SplitKey([SplitKey.Missing], 1);
        var nullValue = new SplitKey([null], 1);

        // Act & Assert
        Assert.False(missing.Equals(nullValue));
    }
}

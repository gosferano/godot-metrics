namespace Gosferano.Godot.Metrics;

/// <summary>
/// The values of an instrument's split tag keys, in configured order. Compares by value, so a lookup can wrap
/// a reused scratch array without allocating.
/// </summary>
internal readonly struct SplitKey : IEquatable<SplitKey>
{
    /// <summary>
    /// Marks a split key that the measurement did not carry (distinct from a tag whose value is null)
    /// </summary>
    public static readonly object Missing = new();

    private readonly object?[] _values;
    private readonly int _length;

    public SplitKey(object?[] values, int length)
    {
        _values = values;
        _length = length;
    }

    public bool Equals(SplitKey other)
    {
        if (_length != other._length)
        {
            return false;
        }

        for (var i = 0; i < _length; i++)
        {
            if (!Equals(_values[i], other._values[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is SplitKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        for (var i = 0; i < _length; i++)
        {
            hash.Add(_values[i]);
        }

        return hash.ToHashCode();
    }
}

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Aggregates the measurements of one instrument series (one tag combination, or all tags merged).
/// <see cref="Record"/> may be called from any thread; the other members are called from the collect cycle.
/// </summary>
internal abstract class MetricSeries
{
    /// <summary>
    /// Values produced by <see cref="Collect"/>, in order
    /// </summary>
    public abstract IReadOnlyList<SeriesStat> Stats { get; }

    public abstract void Record(double value);

    /// <summary>
    /// Called before observable instruments are polled
    /// </summary>
    public virtual void BeginObservation()
    {
    }

    /// <summary>
    /// Called after observable instruments are polled
    /// </summary>
    public virtual void EndObservation()
    {
    }

    /// <summary>
    /// Writes the current value of every stat into <paramref name="values"/>
    /// </summary>
    /// <param name="elapsedSeconds">Time since the previous collect cycle</param>
    /// <param name="values">Destination, one slot per entry in <see cref="Stats"/></param>
    public abstract void Collect(double elapsedSeconds, Span<double> values);
}

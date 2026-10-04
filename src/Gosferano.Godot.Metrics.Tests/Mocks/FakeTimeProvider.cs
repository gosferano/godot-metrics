namespace Gosferano.Godot.Metrics.Tests.Mocks;

/// <summary>
/// Clock that only moves when advanced
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp()
    {
        return Interlocked.Read(ref _ticks);
    }

    public void Advance(TimeSpan duration)
    {
        Interlocked.Add(ref _ticks, duration.Ticks);
    }
}

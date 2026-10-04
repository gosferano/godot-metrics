namespace Gosferano.Godot.Metrics;

/// <summary>
/// Display format of a monitor. Names match Godot's <c>Performance.MonitorType</c>.
/// </summary>
internal enum MonitorFormat
{
    /// <summary>Plain number</summary>
    Quantity,

    /// <summary>Bytes</summary>
    Memory,

    /// <summary>Seconds (shown as milliseconds)</summary>
    Time,

    /// <summary>Fraction (0.5 is shown as 50%)</summary>
    Percentage,
}

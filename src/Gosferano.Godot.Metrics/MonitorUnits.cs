namespace Gosferano.Godot.Metrics;

/// <summary>
/// Maps an instrument's UCUM unit to a monitor format and the factor that converts values into it
/// </summary>
internal static class MonitorUnits
{
    public static (MonitorFormat Format, double Scale) Resolve(string? unit)
    {
        return unit switch
        {
            "s" => (MonitorFormat.Time, 1d),
            "ms" => (MonitorFormat.Time, 1e-3),
            "us" or "μs" => (MonitorFormat.Time, 1e-6),
            "ns" => (MonitorFormat.Time, 1e-9),
            "By" => (MonitorFormat.Memory, 1d),
            "KiBy" => (MonitorFormat.Memory, 1024d),
            "MiBy" => (MonitorFormat.Memory, 1024d * 1024d),
            "%" => (MonitorFormat.Percentage, 0.01),
            _ => (MonitorFormat.Quantity, 1d),
        };
    }
}

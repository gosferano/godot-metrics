using Microsoft.Extensions.Logging;

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Entry point for showing System.Diagnostics.Metrics instruments as Godot custom monitors
/// </summary>
public static class GodotMetrics
{
    /// <summary>
    /// Logger category of every message the library writes
    /// </summary>
    public const string LogCategory = "Gosferano.Godot.Metrics";

    /// <summary>
    /// Starts listening to the configured meters and registers a Godot custom monitor for every instrument
    /// </summary>
    /// <param name="configure">Configures meter filters, tag splits and counter output</param>
    /// <returns>Handle that exposes snapshots and removes every monitor when disposed</returns>
    /// <exception cref="InvalidOperationException">No meter was included</exception>
    public static GodotMetricsHandle Enable(Action<GodotMetricsOptions> configure)
    {
        return Enable(configure, new GodotMonitorAdapter(), TimeProvider.System);
    }

    internal static GodotMetricsHandle Enable(
        Action<GodotMetricsOptions> configure,
        IMonitorAdapter adapter,
        TimeProvider timeProvider
    )
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new GodotMetricsOptions();
        configure(options);

        if (options.MeterPatterns.Count == 0)
        {
            throw new InvalidOperationException(
                "No meters included. Call IncludeMeter at least once, e.g. IncludeMeter(\"MyGame.*\")."
            );
        }

        ILogger logger = options.LoggerFactory?.CreateLogger(LogCategory) ?? new AdapterLogger(adapter);

        return new GodotMetricsHandle(new MetricsCollector(options, adapter, logger, timeProvider));
    }
}

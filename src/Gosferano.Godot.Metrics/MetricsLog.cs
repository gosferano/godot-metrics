using Microsoft.Extensions.Logging;

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Log messages written by the library, as structured message templates
/// </summary>
internal static partial class MetricsLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Unrecognized instrument type {InstrumentType} (first seen on {Instrument}); showing its last value"
    )]
    public static partial void UnrecognizedInstrumentType(ILogger logger, string instrumentType, string instrument);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "{Instrument} exceeded {MaxSeries} tag combinations; further combinations are merged into {OtherSeries}"
    )]
    public static partial void SeriesCapExceeded(ILogger logger, string instrument, int maxSeries, string otherSeries);
}

namespace Gosferano.Godot.Metrics;

/// <summary>
/// One value produced by a series, shown as one monitor
/// </summary>
/// <param name="Suffix">Appended to the series id (e.g. <c>" p95"</c>); empty for single-value series</param>
/// <param name="UsesInstrumentUnit">Whether the value is in the instrument's unit (false for counts)</param>
internal readonly record struct SeriesStat(string Suffix, bool UsesInstrumentUnit);

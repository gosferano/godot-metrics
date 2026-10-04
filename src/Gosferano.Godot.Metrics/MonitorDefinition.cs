namespace Gosferano.Godot.Metrics;

/// <summary>
/// A single Godot monitor: its id, display format and a reader returning the value already scaled for Godot
/// </summary>
internal readonly record struct MonitorDefinition(string Id, MonitorFormat Format, Func<double> Read);

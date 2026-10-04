namespace Gosferano.Godot.Metrics;

/// <summary>
/// The only boundary to Godot. Implementations must accept calls from any thread.
/// </summary>
internal interface IMonitorAdapter
{
    /// <summary>
    /// Registers a custom monitor (marshalled to the main thread)
    /// </summary>
    void AddMonitor(MonitorDefinition monitor);

    /// <summary>
    /// Removes a custom monitor (marshalled to the main thread)
    /// </summary>
    void RemoveMonitor(string id);

    /// <summary>
    /// Runs an action on the main thread during a later frame
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// Reports a warning to the engine log
    /// </summary>
    void LogWarning(string message);
}

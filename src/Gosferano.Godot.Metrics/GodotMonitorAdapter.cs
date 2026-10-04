using System.Reflection;
using Godot;
using Microsoft.Extensions.Logging;
using GodotArray = Godot.Collections.Array;

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Registers monitors through <see cref="Performance"/>. Uses typed monitors (Godot 4.7+) when available.
/// </summary>
internal sealed class GodotMonitorAdapter : IMonitorAdapter
{
    private const string LogPrefix = "[Gosferano.Godot.Metrics] ";

    private static readonly Type? MonitorTypeEnum = typeof(Performance).GetNestedType("MonitorType");

    private static readonly MethodInfo? TypedAddCustomMonitor = MonitorTypeEnum is null
        ? null
        : typeof(Performance).GetMethod(
            nameof(Performance.AddCustomMonitor),
            [typeof(StringName), typeof(Callable), typeof(GodotArray), MonitorTypeEnum]
        );

    public void AddMonitor(MonitorDefinition monitor)
    {
        Func<double> read = monitor.Read;

        CallDeferred(() =>
        {
            if (Performance.HasCustomMonitor(monitor.Id))
            {
                return;
            }

            var callable = Callable.From(read);

            if (TypedAddCustomMonitor is not null && MonitorTypeEnum is not null)
            {
                object type = Enum.Parse(MonitorTypeEnum, monitor.Format.ToString());
                TypedAddCustomMonitor.Invoke(null, [new StringName(monitor.Id), callable, null, type]);
            }
            else
            {
                Performance.AddCustomMonitor(monitor.Id, callable);
            }
        });
    }

    public void RemoveMonitor(string id)
    {
        CallDeferred(() =>
        {
            if (Performance.HasCustomMonitor(id))
            {
                Performance.RemoveCustomMonitor(id);
            }
        });
    }

    public void Post(Action action)
    {
        CallDeferred(action);
    }

    public void Log(LogLevel level, string message)
    {
        if (level >= LogLevel.Error)
        {
            GD.PushError(LogPrefix + message);
        }
        else if (level == LogLevel.Warning)
        {
            GD.PushWarning(LogPrefix + message);
        }
        else
        {
            GD.Print(LogPrefix + message);
        }
    }

    private static void CallDeferred(Action action)
    {
        Callable.From(action).CallDeferred();
    }
}

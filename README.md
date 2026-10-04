# Metrics for Godot

[![NuGet](https://img.shields.io/nuget/v/Gosferano.Godot.Metrics)](https://www.nuget.org/packages/Gosferano.Godot.Metrics)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Gosferano.Godot.Metrics)](https://www.nuget.org/packages/Gosferano.Godot.Metrics)
[![GitHub](https://img.shields.io/github/license/gosferano/godot-metrics.svg)](https://github.com/gosferano/godot-metrics/blob/main/LICENSE)

Shows .NET `System.Diagnostics.Metrics` instruments as Godot 4 custom monitors. Your simulation and domain code publishes standard .NET metrics without referencing Godot, and they appear in the editor's **Debugger → Monitors** tab.

## Features

- ✅ **No Godot in Your Domain Code** - Publish with plain `Meter`, `Counter<T>`, `Histogram<T>` and friends
- ✅ **Every Instrument Type** - Counters, up-down counters, gauges, histograms and their observable variants
- ✅ **All Numeric Types** - `byte`, `short`, `int`, `long`, `float`, `double`, `decimal`
- ✅ **Grouped by Meter** - Monitor ids are `<MeterName>/<instrument>`, so each meter gets its own category
- ✅ **Bounded Cardinality** - Tags are merged by default; split by named tag keys only where you opt in
- ✅ **Typed Monitors** - Durations show as time, bytes as memory and `%` as a percentage (Godot 4.7+)
- ✅ **Thread-Safe** - Record from any thread; Godot calls are marshalled to the main thread
- ✅ **Snapshot API** - Read the same numbers from an in-game overlay in exported builds
- ✅ **Standard Logging** - Logs through `Microsoft.Extensions.Logging`, so Serilog, NLog and others plug in; falls back to the Godot output
- ✅ **Minimal Dependencies** - Only GodotSharp and `Microsoft.Extensions.Logging.Abstractions`

## Installation

### Via NuGet Package Manager
```bash
dotnet add package Gosferano.Godot.Metrics
```

### Via Package Reference
```xml
<PackageReference Include="Gosferano.Godot.Metrics" Version="0.2.0" />
```

Godot loads NuGet dependencies only when the game project has `<EnableDynamicLoading>true</EnableDynamicLoading>`, which the default Godot project template includes.

## Quick Start

### 1. Publish Metrics from Your Domain Code
No Godot reference needed. This can live in a plain class library:
```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

public sealed class SimulationMetrics : IDisposable
{
    public const string MeterName = "MyGame.Simulation";

    private readonly Meter _meter = new(MeterName);

    public SimulationMetrics(Func<int> entityCount)
    {
        TickDuration = _meter.CreateHistogram<double>("tick.duration", unit: "ms");
        TicksProcessed = _meter.CreateCounter<long>("ticks.processed");
        _meter.CreateObservableGauge("ecs.entities", entityCount);
    }

    public Histogram<double> TickDuration { get; }

    public Counter<long> TicksProcessed { get; }

    public void Dispose() => _meter.Dispose();
}

// In the game loop
long start = Stopwatch.GetTimestamp();
RunTick();
metrics.TickDuration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
metrics.TicksProcessed.Add(1);
```

### 2. Enable Monitors on the Godot Side
```csharp
using Godot;
using Gosferano.Godot.Metrics;

public partial class Diagnostics : Node
{
    private GodotMetricsHandle? _metrics;

    public override void _EnterTree()
    {
        _metrics = GodotMetrics.Enable(o => o.IncludeMeter("MyGame.*"));
    }

    public override void _ExitTree()
    {
        _metrics?.Dispose();
    }
}
```

### 3. Open the Monitors Tab
Run the game from the editor and open **Debugger → Monitors**. You will find a `MyGame.Simulation` category with:
```
tick.duration last / avg / p95 / max / count
ticks.processed rate
ecs.entities
```

## Usage Examples

### Instrument Mapping

Godot polls each monitor about once per second, and each monitor shows one number:

| Instrument | Monitors | Value |
|---|---|---|
| `Counter<T>` | `<name> rate` (+ `<name> total` with `WithCounterTotals()`) | Increase per second / running total |
| `UpDownCounter<T>` | `<name>` | Current sum |
| `Gauge<T>` | `<name>` | Last recorded value |
| `Histogram<T>` | `<name> last`, `avg`, `p95`, `max`, `count` | Sliding window over the last 256 measurements; `count` is the lifetime total |
| `ObservableCounter<T>` | same as `Counter<T>` | Rate computed from the cumulative values it reports |
| `ObservableUpDownCounter<T>` | `<name>` | Current value |
| `ObservableGauge<T>` | `<name>` | Current value |
| Anything else | `<name>` | Last recorded value (a warning is logged once per type) |

Observable callbacks run once per collect cycle, no matter how many monitors read them. A cycle runs when a monitor or snapshot is read and the previous one is older than 100 ms.

The monitor format follows the instrument's unit:

| Unit | Godot format |
|---|---|
| `s`, `ms`, `us`, `ns` | Time (converted to seconds, shown as ms) |
| `By`, `KiBy`, `MiBy` | Memory (converted to bytes) |
| `%` | Percentage |
| anything else | Quantity |

`count` monitors are always Quantity. Typed monitors require Godot 4.7+; older versions fall back to plain numbers.

> **Gauge&lt;T&gt; on .NET 8:** synchronous `Gauge<T>` ships with `System.Diagnostics.DiagnosticSource` 9+, not with the .NET 8 runtime. This package doesn't depend on it, but if your project references it, its gauges are picked up and shown as last values.

### Tags and SplitBy

By default all tags of an instrument are **merged**. One instrument gets one monitor set, so tags can never create an unbounded number of monitors:
```csharp
// Both measurements land in "MyGame.Simulation/system.duration ..."
systemDuration.Record(1.2, new KeyValuePair<string, object?>("system", "Movement"));
systemDuration.Record(0.4, new KeyValuePair<string, object?>("system", "Combat"));
```

Opt in to one monitor set per distinct tag value with `SplitBy`. Tag keys you don't name are still merged:
```csharp
GodotMetrics.Enable(o => o
    .IncludeMeter("MyGame.*")
    .SplitBy("system.duration", "system"));
```
```
MyGame.Simulation/system.duration{system=Movement} p95
MyGame.Simulation/system.duration{system=Combat} p95
```

Splitting by several keys produces ids like `{system=Movement,phase=Late}`, with keys in the order you passed them. Measurements that carry none of the split keys go to the plain `system.duration` series.

When tags are merged, an observable up-down counter sums its observations, while a gauge shows the last observation. Use `SplitBy` for gauges that report several tagged values.

### Series Cap

Split instruments are capped at **100 series**. Past that, a warning is logged once and further tag combinations are merged into an `{other}` series:
```
MyGame.Simulation/per.entity{other}
```
The cap is a safety net against unbounded memory growth in long-running sessions (say, an idle game left open overnight with an entity id as a tag). It is not configurable: if you hit it, the tag is a poor fit for `SplitBy`.

### Snapshot API for In-Game Overlays

The Monitors tab only exists while the editor debugger is attached. To show the same numbers in an exported build, read a snapshot:
```csharp
public partial class MetricsOverlay : Label
{
    private GodotMetricsHandle _metrics = null!;

    public void Initialize(GodotMetricsHandle metrics) => _metrics = metrics;

    public override void _Process(double delta)
    {
        var snapshot = _metrics.GetSnapshot();

        Text = $"tick p95: {snapshot.GetValueOrDefault("MyGame.Simulation/tick.duration p95"):F2} ms\n"
             + $"entities: {snapshot.GetValueOrDefault("MyGame.Simulation/ecs.entities")}";
    }
}
```
Snapshot values are in the instrument's own unit (milliseconds stay milliseconds). Reading every frame is cheap: reads within 100 ms of the last collect cycle reuse its result.

### Logging

The library logs warnings when a split instrument hits the series cap, and when it meets an instrument type it doesn't recognize. By default these go to the Godot output via `GD.PushWarning`.

To route them through your own logging, pass an `ILoggerFactory`. Messages are written under the `GodotMetrics.LogCategory` category (`"Gosferano.Godot.Metrics"`) as structured templates. With Serilog, add `Serilog.Extensions.Logging` to the game project:
```csharp
using Serilog;
using Serilog.Extensions.Logging;

var loggerFactory = new SerilogLoggerFactory(Log.Logger);

_metrics = GodotMetrics.Enable(o => o
    .IncludeMeter("MyGame.*")
    .UseLoggerFactory(loggerFactory));
```
Serilog then receives properties such as `Instrument`, `MaxSeries` and `InstrumentType`, with `SourceContext` set to `Gosferano.Godot.Metrics`.

> `Serilog.Extensions.Logging` 9+ brings in `System.Diagnostics.DiagnosticSource` 9+, which replaces the .NET 8 copy of the metrics APIs in your game. This package works with both, and it also makes synchronous `Gauge<T>` available.

### Threading Notes

- **Recording** is safe from any thread. Aggregation uses `Interlocked` operations and small locks.
- **Meters and instruments** can be created on any thread. `AddCustomMonitor` and `RemoveCustomMonitor` are always deferred to the main thread with `CallDeferred`, and so are series discovered later by `SplitBy` on background threads.
- **Observable callbacks** run on the thread that triggers the collect cycle: the main thread for Godot monitors, or the caller's thread for `GetSnapshot()`. Make them safe to call from there.
- **Lifetime:** when an instrument's meter is disposed, its monitors are removed. Disposing the handle removes every monitor and disposes the `MeterListener`.

## API Reference

### GodotMetrics

#### Constants

**LogCategory**
```csharp
const string LogCategory = "Gosferano.Godot.Metrics"
```
Logger category of every message the library writes. Use it to filter or set levels in your logging configuration.

#### Methods

**Enable**
```csharp
static GodotMetricsHandle Enable(Action<GodotMetricsOptions> configure)
```
Starts listening to the included meters and registers monitors for their instruments. Throws `InvalidOperationException` if no meter is included. Call it once, from the main thread.

### GodotMetricsOptions

#### Methods

**IncludeMeter**
```csharp
GodotMetricsOptions IncludeMeter(string pattern)
```
Includes meters whose name matches `pattern`. `*` matches any sequence of characters; matching is case-insensitive. Call it at least once; call it several times to include several patterns.

**SplitBy**
```csharp
GodotMetricsOptions SplitBy(string instrument, params string[] tagKeys)
```
Shows one monitor set per distinct value of `tagKeys` for every instrument named `instrument`. Calling it again for the same instrument replaces the keys.

**WithCounterTotals**
```csharp
GodotMetricsOptions WithCounterTotals()
```
Adds a `total` monitor next to each counter's `rate` monitor.

**UseLoggerFactory**
```csharp
GodotMetricsOptions UseLoggerFactory(ILoggerFactory loggerFactory)
```
Writes the library's log messages through `loggerFactory` under `GodotMetrics.LogCategory`. Without it, warnings go to the Godot output.

### GodotMetricsHandle

Implements `IDisposable`.

#### Methods

**GetSnapshot**
```csharp
IReadOnlyDictionary<string, double> GetSnapshot()
```
Returns the current value of every monitor, keyed by monitor id, in instrument units. Returns an empty dictionary after disposal.

**Dispose**
```csharp
void Dispose()
```
Removes every registered monitor and disposes the meter listener. Safe to call more than once.

### Monitor Ids
```
<MeterName>/<instrument>[{key=value,...}][ suffix]
```
A `/` inside a meter name, instrument name or tag is replaced with `_`, because Godot only groups ids that contain exactly one `/`.

## Best Practices

### Keep Godot Out of the Simulation

Define meters in your domain projects and call `GodotMetrics.Enable` only in the Godot project. Unit tests and headless tools can run the simulation, and its metrics, without the engine.

### Name Meters by Layer

Use one meter per layer (`MyGame.Simulation`, `MyGame.Ecs`, `MyGame.Rendering`). Each meter becomes its own Monitors category, and `IncludeMeter("MyGame.*")` picks up all of them.

### Use Units

Set `unit: "ms"` on durations and `unit: "By"` on sizes. Godot then formats them as time and memory instead of bare numbers.

### Prefer Rates and Gauges over Small Fractions

Godot shows Quantity monitors as integers, so a counter rate of `0.3` per second shows as `0`. For slow events, enable `WithCounterTotals()` or measure over a larger unit. Godot also expects monitor values to be zero or positive, so up-down counters that go negative may not graph correctly.

### Split Sparingly

Split only by tags with a small, known set of values (system names, zones, phases). Never split by ids, names or anything user-generated.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

Built with ❤️ for the Godot community.

## Support

- 🐛 [Report Issues](https://github.com/gosferano/godot-metrics/issues)
- 💬 [Discussions](https://github.com/gosferano/godot-metrics/discussions)
- ☕ [Sponsor the Project](https://ko-fi.com/gosferano)
- ⭐ Star the Repository

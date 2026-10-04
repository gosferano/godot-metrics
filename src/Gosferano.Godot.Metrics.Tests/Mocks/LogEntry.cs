using Microsoft.Extensions.Logging;

namespace Gosferano.Godot.Metrics.Tests.Mocks;

/// <summary>
/// One message captured by <see cref="FakeLogger"/>
/// </summary>
internal sealed record LogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties
);

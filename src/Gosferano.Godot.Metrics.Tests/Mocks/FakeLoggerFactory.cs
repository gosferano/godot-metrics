using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Gosferano.Godot.Metrics.Tests.Mocks;

/// <summary>
/// Hands out one shared <see cref="FakeLogger"/> and records the requested categories
/// </summary>
internal sealed class FakeLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentQueue<string> _categories = new();

    public FakeLogger Logger { get; } = new();

    public IReadOnlyCollection<string> Categories => _categories;

    public ILogger CreateLogger(string categoryName)
    {
        _categories.Enqueue(categoryName);
        return Logger;
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}

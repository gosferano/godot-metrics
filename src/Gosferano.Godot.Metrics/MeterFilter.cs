using System.Text.RegularExpressions;

namespace Gosferano.Godot.Metrics;

/// <summary>
/// Matches meter names against wildcard patterns (<c>*</c> = any sequence, case-insensitive)
/// </summary>
internal sealed class MeterFilter
{
    private readonly Regex[] _patterns;

    public MeterFilter(IEnumerable<string> patterns)
    {
        _patterns = patterns
            .Select(pattern => new Regex(
                "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            ))
            .ToArray();
    }

    public bool Matches(string meterName)
    {
        return _patterns.Any(pattern => pattern.IsMatch(meterName));
    }
}

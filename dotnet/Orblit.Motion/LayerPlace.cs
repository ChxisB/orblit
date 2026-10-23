using Orblit.Json;

namespace Orblit.Motion;

/// <summary>Seconds through a one-shot. Saved beside the graph's place.</summary>
public sealed class LayerPlace
{
    /// <exception cref="ArgumentException">When <paramref name="at"/> is not a finite time at least zero.</exception>
    public LayerPlace(double at = 0)
    {
        if (!double.IsFinite(at) || at < 0)
        {
            throw new ArgumentException("A finite time at least zero.", nameof(at));
        }

        At = at;
    }

    public double At { get; }

    public OrderedDictionary<string, object?> ToJson() => new() { ["at"] = At };

    /// <summary>A place out of decoded JSON, or null when it is not one.</summary>
    public static LayerPlace? FromJson(object? raw) =>
        raw is JsonMap map && JsonNumbers.Finite(map.GetValueOrDefault("at")) is { } at && at >= 0 ? new LayerPlace(at) : null;
}

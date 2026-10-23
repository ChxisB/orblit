using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>A stretch of a cutscene seen through one camera.</summary>
public sealed class CutsceneShot
{
    public CutsceneShot(string camera, double start, double duration)
    {
        if (camera.Length == 0)
        {
            throw new ArgumentException("A shot needs a camera.", nameof(camera));
        }

        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "A shot starts at 0 or on.");
        }

        if (!(duration > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A shot needs a length.");
        }

        Camera = camera;
        Start = start;
        Duration = duration;
    }

    /// <summary>The camera's id in the scene, which is its path: <c>street1/lamp3/eye</c> for one inside a placed prefab.</summary>
    public string Camera { get; }

    /// <summary>Seconds into the cutscene.</summary>
    public double Start { get; }

    public double Duration { get; }

    public double End => Start + Duration;

    public CutsceneShot CopyWith(string? camera = null, double? start = null, double? duration = null) =>
        new(camera ?? Camera, start ?? Start, duration ?? Duration);

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["camera"] = Camera,
        ["start"] = Start,
        ["duration"] = Duration,
    };

    /// <summary>A shot out of decoded JSON, or null with a note in <paramref name="problems"/>.</summary>
    public static CutsceneShot? FromJson(object? raw, List<string> problems)
    {
        var json = Values.Object(raw);
        var camera = Values.Text(json, "camera");
        var start = Values.MaybeNumber(json, "start") ?? 0;
        if (!string.IsNullOrEmpty(camera) && !(start < 0) && Values.MaybeNumber(json, "duration") is double duration and > 0)
        {
            return new CutsceneShot(camera, start, duration);
        }

        problems.Add("A shot that could not be read was left out. It needs a camera, a start and a length.");
        return null;
    }
}

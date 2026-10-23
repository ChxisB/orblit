using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>A sound a cutscene plays.</summary>
public sealed class CutsceneSound
{
    public CutsceneSound(string sound, double start, double duration)
    {
        if (sound.Length == 0)
        {
            throw new ArgumentException("A sound needs a file.", nameof(sound));
        }

        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "A sound starts at 0 or on.");
        }

        if (!(duration > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A sound needs a length.");
        }

        Sound = sound;
        Start = start;
        Duration = duration;
    }

    /// <summary>The sound's file, relative to the project.</summary>
    public string Sound { get; }

    /// <summary>Seconds into the cutscene.</summary>
    public double Start { get; }

    /// <summary>
    /// How much of it plays. A cutscene cannot know how long a file is without opening it, and a sound
    /// cut short is often what was meant.
    /// </summary>
    public double Duration { get; }

    public double End => Start + Duration;

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["sound"] = Sound,
        ["start"] = Start,
        ["duration"] = Duration,
    };

    /// <summary>A sound out of decoded JSON, or null with a note in <paramref name="problems"/>.</summary>
    public static CutsceneSound? FromJson(object? raw, List<string> problems)
    {
        var json = Values.Object(raw);
        var sound = Values.Text(json, "sound");
        var start = Values.MaybeNumber(json, "start") ?? 0;
        if (!string.IsNullOrEmpty(sound) && !(start < 0) && Values.MaybeNumber(json, "duration") is double duration and > 0)
        {
            return new CutsceneSound(sound, start, duration);
        }

        problems.Add("A sound that could not be read was left out. It needs a file, a start and a length.");
        return null;
    }
}

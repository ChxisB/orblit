using System.Globalization;
using Orblit.Json;
using Orblit.Mesh;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>The animations of a glTF document, each as a clip.</summary>
internal sealed class GltfClipReader
{
    private static readonly Carrying<Quaternion> Turns = new(ChannelKind.Rotation, 4, Rotation, Unit);
    private static readonly Carrying<Vector3> Moves = new(ChannelKind.Vector, 3, static n => new Vector3(n[0], n[1], n[2]), static value => value);
    private static readonly double[] Rates = [30, 24, 25, 60, 50, 48];
    private static readonly string[] LoopWords = ["loop", "cycle"];
    private static readonly string[] NodeProperties = ["translation", "rotation", "scale", "weights"];

    private readonly GltfNodes _nodes;
    private readonly GltfAccessors _accessors;
    private readonly Dictionary<long, string> _bones = [];

    public GltfClipReader(GltfNodes nodes, GltfAccessors accessors)
    {
        _nodes = nodes;
        _accessors = accessors;
        foreach (var skin in GltfNodes.Maps(nodes.Json.GetValueOrDefault("skins")))
        {
            var joints = GltfNodes.Ints(skin.GetValueOrDefault("joints"));
            var names = nodes.JointNames(joints);
            for (var i = 0; i < joints.Count; i++)
            {
                _bones.TryAdd(joints[i], names[i]);
            }
        }
    }

    public ClipsImported Read()
    {
        var animations = GltfNodes.Maps(_nodes.Json.GetValueOrDefault("animations"));
        var clips = animations.Select(Clip).ToList();
        return new ClipsImported(clips, [.. _accessors.Problems]);
    }

    private ClipDocument Clip(JsonMap animation, int index)
    {
        var name = animation.GetValueOrDefault("name") is string text && Trimmed(text) is { Length: > 0 } trimmed
            ? trimmed
            : $"Animation {index + 1}";
        var samplers = GltfNodes.Maps(animation.GetValueOrDefault("samplers"));
        var channels = new List<ClipChannel>();
        foreach (var channel in GltfNodes.Maps(animation.GetValueOrDefault("channels")))
        {
            if (ChannelOf(channel, samplers, name) is not { } made)
            {
                continue;
            }

            if (channels.Any(made.SameAddress))
            {
                Note($"Two channels of \"{name}\" move {Spoken(PathOf(channel))} on one node; the second was left out.");
            }
            else
            {
                channels.Add(made);
            }
        }

        var times = channels.SelectMany(static channel => channel.BoxedKeys).Select(static key => key.At).ToList();
        var lower = name.ToLowerInvariant();
        var loops = LoopWords.Any(word => lower.StartsWith(word, StringComparison.Ordinal) || lower.EndsWith(word, StringComparison.Ordinal));
        return new ClipDocument(
            name,
            times.Prepend(0.0).Max(),
            whenDone: loops ? WhenDone.Loop : WhenDone.Hold,
            rate: RateOf(times),
            channels: channels);
    }

    private ClipChannel? ChannelOf(JsonMap channel, IReadOnlyList<JsonMap> samplers, string clip)
    {
        if (MovedBy(channel, samplers, clip) is not { } moved)
        {
            return null;
        }

        var bone = _bones.GetValueOrDefault(moved.Node);
        var address = new ChannelAddress(
            bone is null ? _nodes.EntityOf(moved.Node) : string.Empty,
            bone,
            bone is null ? $"transform.{moved.Property}" : moved.Property);
        return moved.Property == "rotation"
            ? Channel(address, Turns, moved.Sampler, clip)
            : Channel(address, Moves, moved.Sampler, clip);
    }

    /// <summary>Which node a channel moves, by which sampler, and how. Notes why when it cannot say.</summary>
    private Moved? MovedBy(JsonMap channel, IReadOnlyList<JsonMap> samplers, string clip)
    {
        if (channel.GetValueOrDefault("target") is not JsonMap target)
        {
            return null;
        }

        var (aimed, path) = Aimed(target);
        if (IsPointer(path))
        {
            Note($"A channel of \"{clip}\" moves \"{path}\", which is not read.");
            return null;
        }

        if (Within(aimed, _nodes.Nodes.Count) is not { } node)
        {
            Note($"A channel of \"{clip}\" moves something that is not a node.");
            return null;
        }

        if (Within(channel.GetValueOrDefault("sampler"), samplers.Count) is not { } at)
        {
            Note($"A channel of \"{clip}\" has no sampler.");
            return null;
        }

        if (path is "weights")
        {
            Note($"Morph target weights in \"{clip}\" were not carried across; clips do not move them yet.");
            return null;
        }

        if (PropertyOf(path) is not { } property)
        {
            Note($"A channel of \"{clip}\" moves \"{Spoken(path)}\", which is not read.");
            return null;
        }

        return new Moved(node, samplers[(int)at], property);
    }

    /// <summary>
    /// The node a channel is aimed at and what it moves there. A pointer into a node's own transform names
    /// the same two things the plain form does, so it reads the same. A pointer anywhere else has no node,
    /// and its text stands as the path.
    /// </summary>
    private static (object? Node, object? Path) Aimed(JsonMap target)
    {
        var path = target.GetValueOrDefault("path");
        if (path is not "pointer" || PointerOf(target) is not { } pointer)
        {
            return (target.GetValueOrDefault("node"), path);
        }

        if (pointer.Split('/') is not ["", "nodes", var index, var property] || !NodeProperties.Contains(property))
        {
            return (null, pointer);
        }

        // A number too long to hold is past the end of any list too, so it stays out of range.
        return (long.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out var at) ? at : long.MaxValue, property);
    }

    private static string? PointerOf(JsonMap target) =>
        target.GetValueOrDefault("extensions") is JsonMap extensions
        && extensions.GetValueOrDefault("KHR_animation_pointer") is JsonMap held
            ? held.GetValueOrDefault("pointer") as string
            : null;

    /// <summary>A path that is a pointer's text, which always begins with a slash, rather than a name.</summary>
    private static bool IsPointer(object? path) => path is string text && text.StartsWith('/');

    private static string? PropertyOf(object? path) => path switch
    {
        "translation" => "position",
        "rotation" => "rotation",
        "scale" => "scale",
        _ => null,
    };

    private ClipChannel<T>? Channel<T>(ChannelAddress address, Carrying<T> carrying, JsonMap sampler, string clip)
        where T : notnull
    {
        var times = _accessors.Floats(AccessorOf(sampler.GetValueOrDefault("input")), 1);
        var values = _accessors.Floats(AccessorOf(sampler.GetValueOrDefault("output")), carrying.Width);
        var hold = sampler.GetValueOrDefault("interpolation") switch
        {
            "STEP" => Hold.Step,
            "CUBICSPLINE" => Hold.Curve,
            _ => Hold.Linear,
        };

        // A cubic spline stores three things a key: the slope arriving, the value, and the slope leaving.
        var stride = hold == Hold.Curve ? 3 : 1;
        if (times.Count == 0 || values.Count < (long)times.Count * stride * carrying.Width)
        {
            Note($"A channel of \"{clip}\" has fewer values than times, so it was left out.");
            return null;
        }

        var keys = KeysOf(carrying, times, values, hold);
        return keys.Count == 0 ? null : new ClipChannel<T>(address.Target, address.Property, carrying.Kind, keys, address.Bone);
    }

    private static List<Key<T>> KeysOf<T>(Carrying<T> carrying, List<double> times, List<double> values, Hold hold)
        where T : notnull
    {
        T Element(int at) => carrying.Make([.. Enumerable.Range(0, carrying.Width).Select(c => Short(values[(at * carrying.Width) + c]))]);

        var keys = new List<Key<T>>();
        for (var k = 0; k < times.Count; k++)
        {
            if (!double.IsFinite(times[k]))
            {
                continue;
            }

            keys.Add(hold == Hold.Curve
                ? new Key<T>(Short(times[k]), carrying.Unit(Element((k * 3) + 1)), hold, slopeIn: Element(k * 3), slopeOut: Element((k * 3) + 2))
                : new Key<T>(Short(times[k]), carrying.Unit(Element(k)), hold));
        }

        return keys;
    }

    private void Note(string problem) => _accessors.Problems.Add(problem);

    private static object? PathOf(JsonMap channel) =>
        channel.GetValueOrDefault("target") is JsonMap target ? Aimed(target).Path : null;

    /// <summary>A whole number that is at least nought and below <paramref name="count"/>, or null.</summary>
    private static long? Within(object? raw, int count) => raw is long at && at >= 0 && at < count ? at : null;

    /// <summary>An accessor index as the accessors take it. One past the int range is past the end of any list too.</summary>
    private static int? AccessorOf(object? raw) => GltfNodes.Index(raw) is { } index ? GltfNodes.Saturate(index) : null;

    /// <summary>A value as a message says it: a string as it is, anything else as the JSON that wrote it.</summary>
    private static string Spoken(object? value) => value is string text ? text : JsonText.Encode(value);

    /// <summary>
    /// Text without the white space at either end, which includes the byte order mark: a name copied out of a
    /// document editor carries one more often than it carries a visible character.
    /// </summary>
    private static string Trimmed(string text)
    {
        static bool Blank(char c) => char.IsWhiteSpace(c) || c == (char)0xFEFF;

        var start = 0;
        var end = text.Length;
        while (start < end && Blank(text[start]))
        {
            start++;
        }

        while (end > start && Blank(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }

    /// <summary>
    /// The frame rate every one of <paramref name="times"/> sits on, or thirty. Thirty first, then the film and
    /// video rates, then their doubles: a clip keyed on whole seconds fits them all, and thirty is the likeliest
    /// guess.
    /// </summary>
    private static double RateOf(IReadOnlyList<double> times) =>
        Rates.FirstOrDefault(rate => times.All(at => Math.Abs((Math.Round(at * rate, MidpointRounding.AwayFromZero) / rate) - at) < 1e-3), 30);

    /// <summary>
    /// The shortest decimal that is still the same float. The file's numbers are floats, and a double printed in
    /// full carries seventeen digits of which nine mean anything.
    /// </summary>
    private static double Short(double value)
    {
        if (!double.IsFinite(value))
        {
            return value;
        }

        for (var digits = 6; digits <= 9; digits++)
        {
            var shorter = double.Parse(value.ToString("G" + digits, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            if ((float)shorter == (float)value)
            {
                return shorter;
            }
        }

        return value;
    }

    private static Quaternion Rotation(double[] n) => new(n[0], n[1], n[2], n[3]);

    /// <summary>A rotation made to turn without scaling, when it is not nearly one already or nothing at all.</summary>
    private static Quaternion Unit(Quaternion value)
    {
        var length = value.Length;
        return length < 1e-9 || Math.Abs(length - 1) < 1e-6 ? value : value.Normalized();
    }

    private sealed record Moved(long Node, JsonMap Sampler, string Property);

    /// <summary>What a channel of one kind needs to be read: how many numbers a value takes and how they make one.</summary>
    private sealed record Carrying<T>(ChannelKind<T> Kind, int Width, Func<double[], T> Make, Func<T, T> Unit)
        where T : notnull;
}

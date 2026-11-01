using Orblit.Json;

namespace Orblit.Motion;

/// <summary>A frozen pose, held as copies of every value so nothing it came from can change it.</summary>
public sealed class PoseSnapshot : IEquatable<PoseSnapshot>
{
    private readonly ClipFrame _frame;

    public PoseSnapshot(ClipFrame frame)
    {
        // Written and read back, which copies every vector and refuses a value that cannot be carried.
        _frame = Read(Write(frame))
            ?? throw new ArgumentException("A snapshot needs finite, supported pose values.", nameof(frame));
    }

    /// <summary>The pose as a frame of its own, that nothing else holds.</summary>
    public ClipFrame Sample() => ClipFrame.Mix([_frame], [1], _frame.At);

    public OrderedDictionary<string, object?> ToJson() => Write(_frame);

    /// <summary>A snapshot out of decoded JSON, or null when it is not one.</summary>
    public static PoseSnapshot? FromJson(object? raw) =>
        raw is JsonMap map && Read(map) is { } frame ? new PoseSnapshot(frame) : null;

    public bool Equals(PoseSnapshot? other) => other is not null && Text() == other.Text();

    public override bool Equals(object? obj) => Equals(obj as PoseSnapshot);

    public override int GetHashCode() => Text().GetHashCode(StringComparison.Ordinal);

    private static OrderedDictionary<string, object?> Write(ClipFrame frame)
    {
        var values = new OrderedDictionary<string, object?>();
        foreach (var (target, properties) in frame.Values)
        {
            var written = new OrderedDictionary<string, object?>();
            foreach (var (property, value) in properties)
            {
                written[property] = WriteValue(value);
            }

            values[target] = written;
        }

        var bones = new OrderedDictionary<string, object?>();
        foreach (var (target, locals) in frame.Bones)
        {
            var written = new OrderedDictionary<string, object?>();
            foreach (var (bone, local) in locals)
            {
                written[bone] = WriteLocal(local);
            }

            bones[target] = written;
        }

        return new() { ["at"] = frame.At, ["values"] = values, ["bones"] = bones };
    }

    private static OrderedDictionary<string, object?> WriteLocal(BoneLocal local)
    {
        var written = new OrderedDictionary<string, object?>();
        if (local.Position is { } position)
        {
            written["position"] = WriteValue(position);
        }

        if (local.Rotation is { } rotation)
        {
            written["rotation"] = WriteValue(rotation);
        }

        if (local.Scale is { } scale)
        {
            written["scale"] = WriteValue(scale);
        }

        return written;
    }

    private static object WriteValue(object value) => value switch
    {
        Vector3 vector => new List<object?> { vector.X, vector.Y, vector.Z },
        Quaternion rotation => new List<object?> { rotation.X, rotation.Y, rotation.Z, rotation.W },
        _ => value,
    };

    private static ClipFrame? Read(JsonMap raw)
    {
        if (JsonNumbers.Finite(raw.GetValueOrDefault("at")) is not { } at || raw.GetValueOrDefault("values") is not JsonMap targets)
        {
            return null;
        }

        var frame = new ClipFrame(at);
        foreach (var (target, properties) in targets)
        {
            if (properties is not JsonMap map)
            {
                return null;
            }

            var values = new OrderedDictionary<string, object>();
            foreach (var (property, value) in map)
            {
                if (ReadValue(value) is not { } decoded)
                {
                    return null;
                }

                values[property] = decoded;
            }

            frame.Values[target] = values;
        }

        return ReadBones(raw.GetValueOrDefault("bones"), frame) ? frame : null;
    }

    private static bool ReadBones(object? raw, ClipFrame frame)
    {
        if (raw is not JsonMap targets)
        {
            return false;
        }

        foreach (var (target, bones) in targets)
        {
            if (bones is not JsonMap map)
            {
                return false;
            }

            var locals = new OrderedDictionary<string, BoneLocal>();
            foreach (var (bone, local) in map)
            {
                if (ReadLocal(local) is not { } read)
                {
                    return false;
                }

                locals[bone] = read;
            }

            frame.Bones[target] = locals;
        }

        return true;
    }

    // A part that is written must be a vector or a rotation as its name says; one not written is not there.
    private static BoneLocal? ReadLocal(object? raw)
    {
        if (raw is not JsonMap map)
        {
            return null;
        }

        var position = ReadValue(map.GetValueOrDefault("position"));
        var rotation = ReadValue(map.GetValueOrDefault("rotation"));
        var scale = ReadValue(map.GetValueOrDefault("scale"));
        if ((map.ContainsKey("position") && position is not Vector3)
            || (map.ContainsKey("rotation") && rotation is not Quaternion)
            || (map.ContainsKey("scale") && scale is not Vector3))
        {
            return null;
        }

        return new BoneLocal(position as Vector3, rotation as Quaternion, scale as Vector3);
    }

    private static object? ReadValue(object? raw)
    {
        switch (raw)
        {
            case bool flag:
                return flag;
            case long or double:
                return JsonNumbers.Finite(raw);
            case JsonList list when list.Count == 3:
                return ChannelKind.Vector.TryRead(raw, out var vector) ? vector : null;
            case JsonList:
                return ChannelKind.Rotation.TryRead(raw, out var rotation) ? rotation : null;
            default:
                return null;
        }
    }

    private string Text() => JsonText.Encode(Write(_frame));
}

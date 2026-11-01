using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// Where a bone is relative to its parent, as far as a clip says.
/// </summary>
/// <remarks>
/// Each part is null where the clip does not move it, which is not the same as moving it nowhere: a clip
/// that only turns an arm leaves the arm's length to whatever else has a say, the rest pose or another clip.
/// </remarks>
public sealed class BoneLocal(Vector3? position = null, Quaternion? rotation = null, Vector3? scale = null)
{
    public Vector3? Position { get; set; } = position;

    public Quaternion? Rotation { get; set; } = rotation;

    public Vector3? Scale { get; set; } = scale;
}

/// <summary>
/// What a clip says at one moment.
/// </summary>
/// <remarks>
/// Values rather than effects, like a sequence's frame: nothing here has touched a scene or a skeleton, so
/// a frame can be shown for a scrub, compared with another, or thrown away without anything to undo.
/// </remarks>
public sealed class ClipFrame(double at)
{
    /// <summary>Seconds into the clip.</summary>
    public double At { get; } = at;

    /// <summary>Target, then property, then the value, for everything on an entity.</summary>
    public OrderedDictionary<string, OrderedDictionary<string, object>> Values { get; } = [];

    /// <summary>Target, then bone, then where the clip has the bone.</summary>
    public OrderedDictionary<string, OrderedDictionary<string, BoneLocal>> Bones { get; } = [];

    public object? ValueOf(string target, string property) =>
        Values.TryGetValue(target, out var properties) && properties.TryGetValue(property, out var value) ? value : null;

    public BoneLocal? BoneOf(string target, string bone) =>
        Bones.TryGetValue(target, out var locals) && locals.TryGetValue(bone, out var local) ? local : null;

    /// <summary>Every channel of <paramref name="clip"/>, sampled at <paramref name="at"/>.</summary>
    public static ClipFrame Sample(ClipDocument clip, double at)
    {
        var frame = new ClipFrame(at);
        foreach (var channel in clip.Channels)
        {
            frame.Put(channel, channel.ValueAt(at));
        }

        return frame;
    }

    /// <summary>
    /// <paramref name="frames"/> blended, each with as much say as its weight in <paramref name="weights"/>.
    /// </summary>
    /// <remarks>
    /// A value mixes the way its channel's kind does: numbers and vectors by weighted average, rotations the
    /// short way round, and flags by whichever frame has the most say. Only the frames that say something
    /// about a value share it, so a clip that leaves an arm alone leaves the arm to the clips that move it,
    /// the way <see cref="BoneLocal"/> leaves a part to whatever else has a say. A frame with no weight has
    /// none, and a value one frame has as a rotation and another as angles goes by whichever the first frame
    /// to have it says.
    /// </remarks>
    public static ClipFrame Mix(IReadOnlyList<ClipFrame> frames, IReadOnlyList<double> weights, double at = 0)
    {
        if (frames.Count != weights.Count)
        {
            throw new ArgumentException("A weight for every frame, and a frame for every weight.", nameof(weights));
        }

        var values = new OrderedDictionary<string, OrderedDictionary<string, Say>>();
        var bones = new OrderedDictionary<string, OrderedDictionary<string, BoneSay>>();
        for (var i = 0; i < frames.Count; i++)
        {
            // Written so that a weight that is not a number has none either.
            if (weights[i] > 0)
            {
                Listen(frames[i], weights[i], values, bones);
            }
        }

        var mixed = new ClipFrame(at);
        MixValues(values, mixed);
        MixBones(bones, mixed);
        return mixed;
    }

    /// <summary>Puts <paramref name="value"/> where <paramref name="channel"/> says it goes.</summary>
    public void Put(ClipChannel channel, object value)
    {
        if (channel.Bone is not { } bone)
        {
            Slot(Values, channel.Target, static () => [])[channel.Property] = value;
            return;
        }

        var local = Slot(Slot(Bones, channel.Target, static () => []), bone, static () => new BoneLocal());
        switch (channel.Property, value)
        {
            case ("position", Vector3 position):
                local.Position = position;
                break;
            case ("rotation", Quaternion rotation):
                local.Rotation = rotation;
                break;
            case ("scale", Vector3 scale):
                local.Scale = scale;
                break;
        }
    }

    private static TValue Slot<TValue>(OrderedDictionary<string, TValue> map, string key, Func<TValue> make)
    {
        if (!map.TryGetValue(key, out var value))
        {
            value = make();
            map[key] = value;
        }

        return value;
    }

    private static void Listen(
        ClipFrame frame,
        double weight,
        OrderedDictionary<string, OrderedDictionary<string, Say>> values,
        OrderedDictionary<string, OrderedDictionary<string, BoneSay>> bones)
    {
        foreach (var (target, properties) in frame.Values)
        {
            var into = Slot(values, target, static () => []);
            foreach (var (property, value) in properties)
            {
                Slot(into, property, static () => new Say()).Add(value, weight);
            }
        }

        foreach (var (target, locals) in frame.Bones)
        {
            var into = Slot(bones, target, static () => []);
            foreach (var (bone, local) in locals)
            {
                Slot(into, bone, static () => new BoneSay()).Add(local, weight);
            }
        }
    }

    private static void MixValues(OrderedDictionary<string, OrderedDictionary<string, Say>> values, ClipFrame into)
    {
        foreach (var (target, properties) in values)
        {
            OrderedDictionary<string, object> mixed = [];
            into.Values[target] = mixed;
            foreach (var (property, say) in properties)
            {
                if (say.Mixed() is { } value)
                {
                    mixed[property] = value;
                }
            }
        }
    }

    private static void MixBones(OrderedDictionary<string, OrderedDictionary<string, BoneSay>> bones, ClipFrame into)
    {
        foreach (var (target, locals) in bones)
        {
            OrderedDictionary<string, BoneLocal> mixed = [];
            into.Bones[target] = mixed;
            foreach (var (bone, say) in locals)
            {
                mixed[bone] = say.Mixed();
            }
        }
    }

    /// <summary>Everything the frames being mixed say about one value, and how loudly.</summary>
    private sealed class Say
    {
        private readonly List<object> _values = [];
        private readonly List<double> _weights = [];

        internal void Add(object value, double weight)
        {
            _values.Add(value);
            _weights.Add(weight);
        }

        /// <summary>The mix, or null when nothing said anything.</summary>
        internal object? Mixed()
        {
            if (_values.Count == 0)
            {
                return null;
            }

            // The kind is named each time: left to itself, the switch would take every value as an object
            // and hand the mixer the wrong sort of list.
            return _values[0] switch
            {
                double => All(Mixers.OfDouble),
                Vector3 => All(Mixers.OfVector3),
                Quaternion => All(Mixers.OfQuaternion),
                bool => All(Mixers.OfBool),
                var other => other,
            };
        }

        /// <summary>The values that are <typeparamref name="T"/>, mixed.</summary>
        private T All<T>(IMixer<T> mixer)
            where T : notnull
        {
            var kept = new List<T>();
            var said = new List<double>();
            for (var i = 0; i < _values.Count; i++)
            {
                if (_values[i] is T value)
                {
                    kept.Add(value);
                    said.Add(_weights[i]);
                }
            }

            // One voice needs no mixing, and a copy keeps the mix from sharing a vector with the frame it
            // came from.
            return kept.Count == 1 ? Copy(kept[0]) : mixer.Mix(kept, said);
        }

        private static T Copy<T>(T value) => value switch
        {
            Vector3 vector => (T)(object)vector.Clone(),
            Quaternion rotation => (T)(object)rotation.Clone(),
            _ => value,
        };
    }

    private sealed class BoneSay
    {
        private readonly Say _position = new();
        private readonly Say _rotation = new();
        private readonly Say _scale = new();

        internal void Add(BoneLocal local, double weight)
        {
            if (local.Position is { } position)
            {
                _position.Add(position, weight);
            }

            if (local.Rotation is { } rotation)
            {
                _rotation.Add(rotation, weight);
            }

            if (local.Scale is { } scale)
            {
                _scale.Add(scale, weight);
            }
        }

        internal BoneLocal Mixed() => new(_position.Mixed() as Vector3, _rotation.Mixed() as Quaternion, _scale.Mixed() as Vector3);
    }
}

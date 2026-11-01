using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>How frames are laid over one another.</summary>
public static class FrameLayers
{
    /// <summary>The full local rest pose of each target's skeleton.</summary>
    public static ClipFrame RestOf(IReadOnlyDictionary<string, RestSkeleton> skeletons)
    {
        var rest = new ClipFrame(0);
        foreach (var (target, skeleton) in skeletons)
        {
            OrderedDictionary<string, BoneLocal> locals = [];
            for (var bone = 0; bone < skeleton.Length; bone++)
            {
                locals[skeleton.Names[bone]] = new BoneLocal(skeleton.PositionOf(bone), skeleton.RotationOf(bone), skeleton.ScaleOf(bone));
            }

            rest.Bones[target] = locals;
        }

        return rest;
    }

    /// <summary>
    /// <paramref name="frame"/> over <paramref name="rest"/>: everything the rest pose has, with whatever
    /// the frame says in place of it, part by part. Copies, so neither is shared with the result.
    /// </summary>
    public static ClipFrame Complete(ClipFrame frame, ClipFrame rest)
    {
        var completed = ClipFrame.Mix([rest], [1], frame.At);
        foreach (var (target, properties) in frame.Values)
        {
            var into = completed.Values.GetValueOrAdd(target);
            foreach (var (property, value) in properties)
            {
                into[property] = CloneValue(value);
            }
        }

        foreach (var (target, locals) in frame.Bones)
        {
            var into = completed.Bones.GetValueOrAdd(target);
            foreach (var (bone, local) in locals)
            {
                into[bone] = Over(local, into.GetValueOrDefault(bone));
            }
        }

        return completed;
    }

    /// <summary>
    /// <paramref name="overlay"/> in place of what <paramref name="base"/> has, in proportion to
    /// <paramref name="weight"/> and <paramref name="mask"/>. A channel the overlay does not author is left alone.
    /// </summary>
    /// <exception cref="ArgumentException">When the weight is not between zero and one.</exception>
    // F2: five parameters, the last two optional and called by name.
    public static ClipFrame Layer(ClipFrame @base, ClipFrame overlay, ClipFrame rest, BoneMask? mask = null, double weight = 1)
    {
        CheckWeight(weight, nameof(weight));
        var completed = Complete(@base, rest);
        if (mask is null)
        {
            LayerValues(completed, overlay, weight);
        }

        foreach (var (target, locals) in overlay.Bones)
        {
            var into = completed.Bones.GetValueOrAdd(target);
            foreach (var (bone, local) in locals)
            {
                var say = weight * (mask?.WeightOf(target, bone) ?? 1);
                if (say > 0)
                {
                    into[bone] = LayerLocal(into.GetValueOrDefault(bone) ?? new BoneLocal(), local, say);
                }
            }
        }

        return completed;
    }

    /// <summary>
    /// Adds the overlay's local bone difference from <paramref name="reference"/> to <paramref name="base"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="rest"/> fills missing base channels, and the reference defaults to it. Rotations
    /// compose on the right, and scales use a ratio to the reference.
    /// </remarks>
    /// <exception cref="ArgumentException">When the weight is not between zero and one.</exception>
    // F2: six parameters, the last three optional and called by name.
    public static ClipFrame Add(
        ClipFrame @base,
        ClipFrame overlay,
        ClipFrame rest,
        ClipFrame? reference = null,
        BoneMask? mask = null,
        double weight = 1)
    {
        CheckWeight(weight, nameof(weight));
        var completed = Complete(@base, rest);
        var against = Complete(reference ?? rest, rest);
        if (mask is null)
        {
            AddValues(completed, overlay, against, weight);
        }

        foreach (var (target, locals) in overlay.Bones)
        {
            var into = completed.Bones.GetValueOrAdd(target);
            foreach (var (bone, local) in locals)
            {
                var say = weight * (mask?.WeightOf(target, bone) ?? 1);
                if (say > 0 && against.BoneOf(target, bone) is { } anchor)
                {
                    into[bone] = AddLocal(into.GetValueOrDefault(bone) ?? anchor, local, anchor, say);
                }
            }
        }

        return completed;
    }

    internal static object CloneValue(object value) => value switch
    {
        Vector3 vector => vector.Clone(),
        Quaternion rotation => rotation.Clone(),
        _ => value,
    };

    internal static void CheckWeight(double weight, string paramName)
    {
        if (!double.IsFinite(weight) || weight < 0 || weight > 1)
        {
            throw new ArgumentException("Between zero and one.", paramName);
        }
    }

    // A local with whatever it leaves out taken from the one under it, copied.
    private static BoneLocal Over(BoneLocal local, BoneLocal? under) => new(
        (local.Position ?? under?.Position)?.Clone(),
        (local.Rotation ?? under?.Rotation)?.Clone(),
        (local.Scale ?? under?.Scale)?.Clone());

    private static OrderedDictionary<string, T> GetValueOrAdd<T>(this OrderedDictionary<string, OrderedDictionary<string, T>> table, string key)
    {
        if (!table.TryGetValue(key, out var found))
        {
            found = [];
            table[key] = found;
        }

        return found;
    }

    private static void LayerValues(ClipFrame completed, ClipFrame overlay, double weight)
    {
        foreach (var (target, properties) in overlay.Values)
        {
            var into = completed.Values.GetValueOrAdd(target);
            foreach (var (property, value) in properties)
            {
                if (into.TryGetValue(property, out var before))
                {
                    into[property] = MixValue(before, value, weight);
                }
                else if (weight > 0)
                {
                    into[property] = CloneValue(value);
                }
            }
        }
    }

    private static BoneLocal LayerLocal(BoneLocal under, BoneLocal over, double weight) => new(
        MixPart(under.Position, over.Position, weight),
        MixPart(under.Rotation, over.Rotation, weight),
        MixPart(under.Scale, over.Scale, weight));

    private static T? MixPart<T>(T? under, T? over, double weight)
        where T : class
    {
        if (over is null)
        {
            return under;
        }

        return under is null ? (T)CloneValue(over) : (T)MixValue(under, over, weight);
    }

    private static object MixValue(object under, object over, double weight) => (under, over) switch
    {
        (Vector3 a, Vector3 b) => Mixers.OfVector3.Mix([a, b], [1 - weight, weight]),
        (Quaternion a, Quaternion b) => Mixers.OfQuaternion.Mix([a, b], [1 - weight, weight]),
        (double a, double b) => a + ((b - a) * weight),
        _ => weight >= 0.5 ? over : under,
    };

    private static void AddValues(ClipFrame completed, ClipFrame overlay, ClipFrame reference, double weight)
    {
        foreach (var (target, properties) in overlay.Values)
        {
            if (!completed.Values.TryGetValue(target, out var into))
            {
                continue;
            }

            foreach (var (property, value) in properties)
            {
                if (into.TryGetValue(property, out var under) && reference.ValueOf(target, property) is { } anchor)
                {
                    into[property] = AddValue(under, value, anchor, weight);
                }
            }
        }
    }

    private static object AddValue(object under, object over, object reference, double weight) => (under, over, reference) switch
    {
        (double a, double b, double r) => a + ((b - r) * weight),
        (Vector3 a, Vector3 b, Vector3 r) => a + ((b - r) * weight),
        (Quaternion a, Quaternion b, Quaternion r) => AddTurn(a, b, r, weight),
        _ => under,
    };

    private static Quaternion AddTurn(Quaternion under, Quaternion over, Quaternion reference, double weight)
    {
        var added = under * Mixers.OfQuaternion.Mix([Quaternion.Identity(), reference.Conjugated() * over], [1 - weight, weight]);
        added.Normalize();
        return added;
    }

    private static BoneLocal AddLocal(BoneLocal under, BoneLocal over, BoneLocal reference, double weight) => new(
        AddPart(under.Position, over.Position, reference.Position, weight),
        AddPart(under.Rotation, over.Rotation, reference.Rotation, weight),
        AddScale(under.Scale, over.Scale, reference.Scale, weight));

    private static T? AddPart<T>(T? under, T? over, T? reference, double weight)
        where T : class
    {
        if (under is null || over is null || reference is null)
        {
            return under;
        }

        return (T)AddValue(under, over, reference, weight);
    }

    private static Vector3? AddScale(Vector3? under, Vector3? over, Vector3? reference, double weight)
    {
        var scale = under?.Clone();
        if (scale is null || over is null || reference is null)
        {
            return scale;
        }

        for (var axis = 0; axis < 3; axis++)
        {
            var divisor = reference[axis];
            if (divisor != 0)
            {
                scale[axis] *= 1 + (((over[axis] / divisor) - 1) * weight);
            }
            else
            {
                scale[axis] += over[axis] * weight;
            }
        }

        return scale;
    }
}

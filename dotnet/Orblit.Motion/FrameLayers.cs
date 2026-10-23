namespace Orblit.Motion;

/// <summary>How frames are laid over one another.</summary>
internal static class FrameLayers
{
    /// <summary>
    /// <paramref name="frame"/> over <paramref name="rest"/>: everything the rest pose has, with whatever
    /// the frame says in place of it, part by part. Copies, so neither is shared with the result.
    /// </summary>
    internal static ClipFrame Complete(ClipFrame frame, ClipFrame rest)
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

    internal static object CloneValue(object value) => value switch
    {
        Vector3 vector => vector.Clone(),
        Quaternion rotation => rotation.Clone(),
        _ => value,
    };
}

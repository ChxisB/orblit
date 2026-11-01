namespace Orblit.Motion;

/// <summary>The skins of a glTF document, each as a skeleton standing at rest.</summary>
internal static class GltfSkeletons
{
    public static List<RestSkeleton> Of(GltfNodes nodes)
    {
        var skeletons = new List<RestSkeleton>();
        foreach (var skin in GltfNodes.Maps(nodes.Json.GetValueOrDefault("skins")))
        {
            var joints = GltfNodes.Ints(skin.GetValueOrDefault("joints"));
            if (joints.Count > 0)
            {
                skeletons.Add(Skeleton(nodes, joints));
            }
        }

        return skeletons;
    }

    private static RestSkeleton Skeleton(GltfNodes nodes, List<long> joints)
    {
        var slot = new Dictionary<long, int>();
        for (var i = 0; i < joints.Count; i++)
        {
            slot.TryAdd(joints[i], i);
        }

        // A parent has one joint fewer above it than its child, so sorting by that puts parents first without
        // disturbing the file's order otherwise.
        var above = joints.Select(node => JointAbove(nodes, slot, node)).ToList();
        var order = Enumerable.Range(0, joints.Count).OrderBy(joint => Depth(above, joint)).ThenBy(joint => joint).ToList();
        var place = order.Select((joint, at) => (joint, at)).ToDictionary(static pair => pair.joint, static pair => pair.at);
        var named = nodes.JointNames(joints);
        var hangsFrom = nodes.ParentOf(joints[order[0]]);

        return new RestSkeleton(
            names: [.. order.Select(joint => named[joint])],
            parents: [.. order.Select(joint => above[joint] is { } up ? place[up] : -1)],
            local: [.. order.Select(joint => nodes.LocalOf(joints[joint]))],
            above: hangsFrom is { } hung ? nodes.WorldOf(hung) : null);
    }

    /// <summary>The slot of the nearest joint above <paramref name="node"/>, or null when there is none or the parents loop.</summary>
    private static int? JointAbove(GltfNodes nodes, Dictionary<long, int> slot, long node)
    {
        var seen = new HashSet<long> { node };
        for (var up = nodes.ParentOf(node); up is { } above; up = nodes.ParentOf(above))
        {
            if (!seen.Add(above))
            {
                return null;
            }

            if (slot.TryGetValue(above, out var found))
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>How many joints are above <paramref name="joint"/>, counted no further than there are joints, so a loop ends.</summary>
    private static int Depth(List<int?> above, int joint)
    {
        var count = 0;
        for (var up = above[joint]; up is { } next; up = above[next])
        {
            if (++count > above.Count)
            {
                break;
            }
        }

        return count;
    }
}

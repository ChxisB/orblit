using Orblit.Filament;
using Orblit.Rig;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>
/// A skin as the binding reads it: every list as long as the joints, and a tree whatever the description
/// said.
/// </summary>
/// <remarks>
/// A description from the renderer is already all of that. One built by hand, or read from an older
/// renderer that sent no rests, may not be. What it gets is a skeleton in the wrong place, rather than an
/// exception partway through a frame or a walk up a loop that never ends.
/// </remarks>
internal sealed class SkinShape
{
    public SkinShape(SkinInfo skin)
    {
        var count = skin.Joints.Count;
        Names = BoneNaming.Unique(skin.Joints);
        Parents = ParentsOf(skin, count);
        Rest = [.. Enumerable.Range(0, count).Select(joint => joint < skin.Rest.Count ? skin.Rest[joint] : Matrix4.Identity())];
        Local = LocalsOf(skin, Parents, Rest);
        Order = OrderOf(Parents);
    }

    public IReadOnlyList<string> Names { get; }

    /// <summary>Each joint's parent joint, or -1, with any loop cut.</summary>
    public IReadOnlyList<int> Parents { get; }

    public IReadOnlyList<Matrix4> Rest { get; }

    public IReadOnlyList<Matrix4> Local { get; }

    /// <summary>Joint positions with every parent before its children. The skin's own order promises nothing of the kind.</summary>
    public IReadOnlyList<int> Order { get; }

    private static int[] ParentsOf(SkinInfo skin, int count)
    {
        var parents = new int[count];
        for (var joint = 0; joint < count; joint++)
        {
            var parent = joint < skin.Parents.Count ? skin.Parents[joint] : -1;
            parents[joint] = parent >= 0 && parent < count && parent != joint ? (int)parent : -1;
        }

        CutLoops(parents);
        return parents;
    }

    // With no local to go on, the likeliest shape is the plainest one: no nodes between a joint and its
    // parent, and a root hanging straight from the model.
    private static Matrix4[] LocalsOf(SkinInfo skin, IReadOnlyList<int> parents, IReadOnlyList<Matrix4> rest) =>
    [
        .. Enumerable.Range(0, rest.Count).Select(joint =>
        {
            if (joint < skin.Local.Count)
            {
                return skin.Local[joint];
            }

            return parents[joint] < 0 ? rest[joint] : Matrix4.Inverted(rest[parents[joint]]).Multiplied(rest[joint]);
        }),
    ];

    private static int[] OrderOf(IReadOnlyList<int> parents)
    {
        var order = new List<int>(parents.Count);
        var placed = new bool[parents.Count];
        for (var joint = 0; joint < parents.Count; joint++)
        {
            // Walks up to the first joint already placed, then places the chain from the top down.
            var chain = new Stack<int>();
            for (var up = joint; up >= 0 && !placed[up]; up = parents[up])
            {
                chain.Push(up);
            }

            while (chain.TryPop(out var next))
            {
                placed[next] = true;
                order.Add(next);
            }
        }

        return [.. order];
    }

    /// <summary>
    /// Cuts every loop at the first joint in it, which then hangs from nothing. A joint that only leads
    /// into a loop is left alone. The loop is cut when its own first member is reached, and cutting the
    /// joint as well would detach a limb that was fine.
    /// </summary>
    private static void CutLoops(int[] parents)
    {
        for (var joint = 0; joint < parents.Length; joint++)
        {
            var walked = new HashSet<int> { joint };
            for (var up = parents[joint]; up >= 0; up = parents[up])
            {
                if (up == joint)
                {
                    parents[joint] = -1;
                    break;
                }

                if (!walked.Add(up))
                {
                    break;
                }
            }
        }
    }
}

using System.Collections.ObjectModel;

namespace Orblit.Motion;

/// <summary>
/// A skeleton standing still: which bones it has, how they hang from one another, and where each sits in its
/// parent.
/// </summary>
/// <remarks>
/// <para>
/// What a clip's bone channels are measured against. A clip says where a bone is in its parent, in absolute
/// terms, so it means nothing until it is set beside the skeleton it was made for. Two skeletons that stand
/// the same way with different bone lengths and different rest turns are what retargeting bridges, and this
/// is each of them.
/// </para>
/// <para>
/// Bones come parents first, so a bone's parent is always an earlier index. Only rotation, position and scale
/// of each bone are kept; a matrix that shears is read as the nearest of those.
/// </para>
/// </remarks>
public sealed class RestSkeleton
{
    private readonly ReadOnlyCollection<string> _names;
    private readonly Matrix4 _above;
    private readonly List<Matrix4> _local;
    private readonly List<Parts> _parts;
    private readonly List<Matrix4> _world;
    private readonly List<Quaternion> _worldRotation;
    private readonly Quaternion _aboveRotation;

    /// <summary>
    /// <paramref name="local"/> is each bone's rest transform in its parent. <paramref name="above"/> is where
    /// whatever the root bones hang from stands in the world: an armature node that turns a Z-up rig upright,
    /// or scales a rig made in centimetres.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When the lists differ in length, a name is used twice, or a parent does not come before its child.
    /// </exception>
    public RestSkeleton(IReadOnlyList<string> names, IReadOnlyList<int> parents, IReadOnlyList<Matrix4> local, Matrix4? above = null)
    {
        _names = names.ToList().AsReadOnly();
        Parents = parents.ToList().AsReadOnly();
        _above = (above ?? Matrix4.Identity()).Clone();
        _local = [.. local.Select(matrix => matrix.Clone())];
        Validate();

        _parts = [.. _local.Select(Parts.Of)];
        _world = WorldsOf(_local);
        _worldRotation = [.. _world.Select(matrix => Parts.Of(matrix).Rotation)];
        _aboveRotation = Parts.Of(_above).Rotation;
        Reach = FarthestBone();
    }

    public IReadOnlyList<string> Names => _names;

    /// <summary>Each bone's parent, or -1 for a root.</summary>
    public IReadOnlyList<int> Parents { get; }

    public int Length => Names.Count;

    /// <summary>
    /// How far the farthest bone stands from where the roots hang, in the world, at rest.
    /// </summary>
    /// <remarks>
    /// One number for how big the skeleton is, whichever way it faces and however its bones are named. A
    /// person's is about their height.
    /// </remarks>
    public double Reach { get; }

    /// <summary>The bone called <paramref name="name"/>, or null when there is none.</summary>
    public int? IndexOf(string name)
    {
        var at = _names.IndexOf(name);
        return at < 0 ? null : at;
    }

    /// <summary>Where <paramref name="bone"/> sits in its parent, at rest.</summary>
    public Vector3 PositionOf(int bone) => _parts[bone].Position.Clone();

    /// <summary>How <paramref name="bone"/> is turned in its parent, at rest.</summary>
    public Quaternion RotationOf(int bone) => _parts[bone].Rotation.Clone();

    public Vector3 ScaleOf(int bone) => _parts[bone].Scale.Clone();

    /// <summary>Where <paramref name="bone"/> stands in the world, at rest.</summary>
    public Vector3 OriginOf(int bone) => _world[bone].GetTranslation();

    /// <summary>
    /// Where every bone stands in the world when <paramref name="pose"/> puts some of them elsewhere in their
    /// parents, parents first like the bones themselves.
    /// </summary>
    /// <remarks>
    /// A bone the pose does not name keeps its rest, and so does a part of a bone that the pose leaves null. A
    /// name the skeleton does not have is ignored. This is for drawing a skeleton, so it answers points and no
    /// turns.
    /// </remarks>
    public IReadOnlyList<Vector3> PosedOrigins(IReadOnlyDictionary<string, BoneLocal> pose) =>
        [.. PosedWorlds(pose).Select(matrix => matrix.GetTranslation())];

    /// <summary>
    /// Where every bone stands in the world, turn and scale included, when <paramref name="pose"/> puts some
    /// of them elsewhere in their parents.
    /// </summary>
    /// <remarks>
    /// Read as <see cref="PosedOrigins"/> is, so a limb can be worked out from the pose a clip gives and not
    /// from the rest.
    /// </remarks>
    public IReadOnlyList<Matrix4> PosedWorlds(IReadOnlyDictionary<string, BoneLocal> pose) =>
        WorldsOf([.. _parts.Select((parts, bone) =>
        {
            var local = pose.GetValueOrDefault(Names[bone]);
            return Matrix4.Compose(local?.Position ?? parts.Position, local?.Rotation ?? parts.Rotation, local?.Scale ?? parts.Scale);
        })]);

    /// <summary>How <paramref name="bone"/> is turned in the world, at rest.</summary>
    public Quaternion WorldRotationOf(int bone) => _worldRotation[bone].Clone();

    /// <summary>
    /// How the parent of <paramref name="bone"/> is turned in the world, at rest: for a root, whatever it
    /// hangs from.
    /// </summary>
    public Quaternion ParentWorldRotationOf(int bone) =>
        Parents[bone] < 0 ? _aboveRotation.Clone() : _worldRotation[Parents[bone]].Clone();

    /// <summary>The parent of <paramref name="bone"/> in the world at rest, as a matrix, scale and all.</summary>
    public Matrix4 ParentWorldOf(int bone) => (Parents[bone] < 0 ? _above : _world[Parents[bone]]).Clone();

    public override string ToString() => $"RestSkeleton({Length} bones)";

    private void Validate()
    {
        if (Parents.Count != Names.Count || _local.Count != Names.Count)
        {
            throw new ArgumentException("A parent and a rest transform for every bone.");
        }

        if (Names.Distinct().Count() != Names.Count)
        {
            throw new ArgumentException("Two bones share a name.", nameof(Names));
        }

        for (var bone = 0; bone < Parents.Count; bone++)
        {
            if (Parents[bone] < -1 || Parents[bone] >= bone)
            {
                throw new ArgumentException($"Bone {bone} hangs from {Parents[bone]}, which does not come before it.", nameof(Parents));
            }
        }
    }

    /// <summary>The world matrix of every bone, given each one's matrix in its parent.</summary>
    private List<Matrix4> WorldsOf(List<Matrix4> locals)
    {
        var world = new List<Matrix4>();
        for (var bone = 0; bone < locals.Count; bone++)
        {
            var parent = Parents[bone];
            world.Add((parent < 0 ? _above : world[parent]).Multiplied(locals[bone]));
        }

        return world;
    }

    private double FarthestBone()
    {
        var origin = _above.GetTranslation();
        var farthest = 0.0;
        foreach (var world in _world)
        {
            var away = world.GetTranslation().DistanceTo(origin);
            if (away > farthest)
            {
                farthest = away;
            }
        }

        return farthest;
    }

    private sealed record Parts(Vector3 Position, Quaternion Rotation, Vector3 Scale)
    {
        public static Parts Of(Matrix4 matrix)
        {
            var position = Vector3.Zero();
            var rotation = Quaternion.Identity();
            var scale = Vector3.Zero();
            matrix.Decompose(position, rotation, scale);
            return new Parts(position, rotation, scale);
        }
    }
}

namespace Orblit.Motion;

/// <summary>
/// How the bones of one skeleton move the bones of another, worked out once so a baked clip and a live frame
/// turn a bone the same way.
/// </summary>
internal sealed class RetargetPlan
{
    private static readonly Dictionary<string, string> NoBones = [];
    private static readonly HashSet<string> NoNames = [];

    private readonly List<string> _problems;
    private readonly List<List<int>> _children;
    private readonly List<Quaternion> _stance;
    private readonly List<Quaternion> _rest;
    private readonly Dictionary<int, RetargetLink> _links;
    private readonly Dictionary<int, Matrix4> _across;

    /// <summary>
    /// <paramref name="drivers"/> is target bone to the source bone that moves it.
    /// </summary>
    /// <remarks>
    /// With <paramref name="fixRest"/> each driven bone is first turned to point the way its driver does at
    /// rest, so a target stood in an A-pose follows a source stood in a T-pose by where the limbs point and
    /// not by how far they move.
    /// </remarks>
    // F2: five parameters, all of a plan's parts.
    public RetargetPlan(
        RestSkeleton from,
        RestSkeleton to,
        IReadOnlyDictionary<int, int> drivers,
        bool fixRest,
        IReadOnlyList<string> problems)
    {
        From = from;
        To = to;
        Drivers = new OrderedDictionary<int, int>(drivers);
        FixesRest = fixRest;
        _problems = [.. problems];
        Size = from.Reach > 1e-9 && to.Reach > 1e-9 ? to.Reach / from.Reach : 1;
        _children = ChildrenOf(to);
        _stance = fixRest ? Stances() : [.. Enumerable.Repeat(Quaternion.Identity(), to.Length)];
        _rest = [.. Enumerable.Range(0, from.Length).Select(from.RotationOf)];
        _links = Drivers.Keys.ToDictionary(static bone => bone, Link);
        _across = Drivers.Keys.ToDictionary(static bone => bone, AcrossFor);
    }

    public RestSkeleton From { get; }

    public RestSkeleton To { get; }

    public IReadOnlyDictionary<int, int> Drivers { get; }

    public bool FixesRest { get; }

    /// <summary>How much larger the target is: the ratio of their <see cref="RestSkeleton.Reach"/>.</summary>
    public double Size { get; }

    public IReadOnlyList<string> Problems => _problems.ToArray();

    /// <summary>
    /// Which bone of <paramref name="from"/> drives each bone of <paramref name="to"/>, with
    /// <paramref name="bones"/> naming them by hand over the names and, for <paramref name="humanoid"/>, over
    /// the parts of a person.
    /// </summary>
    /// <remarks>
    /// A bone of <paramref name="to"/> in <paramref name="unpaired"/> is left out of whatever the names and
    /// the body paired it with, so nothing drives it. A bone named in <paramref name="bones"/> is paired by
    /// hand, and so cannot also be unpaired.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// When <paramref name="bones"/> names a bone one skeleton lacks, or <paramref name="unpaired"/> names a
    /// bone <paramref name="to"/> lacks or one that <paramref name="bones"/> pairs.
    /// </exception>
    // F2: six parameters, called by name.
    public static RetargetPlan Of(
        RestSkeleton from,
        RestSkeleton to,
        IReadOnlyDictionary<string, string>? bones,
        IReadOnlySet<string>? unpaired,
        bool humanoid,
        bool fixRest)
    {
        bones ??= NoBones;
        unpaired ??= NoNames;
        CheckUnpaired(to, bones, unpaired);
        var problems = new List<string>();
        var named = Named(from, to, bones, unpaired, humanoid, problems);
        var drivers = new OrderedDictionary<int, int>();
        foreach (var (mine, theirs) in named)
        {
            var into = to.IndexOf(mine);
            var source = from.IndexOf(theirs);
            if (into is null || source is null)
            {
                throw new ArgumentException($"\"{mine}\" or \"{theirs}\" is not a bone of the skeleton it is named in.", nameof(bones));
            }

            drivers[into.Value] = source.Value;
        }

        return new RetargetPlan(from, to, drivers, fixRest, problems);
    }

    /// <summary>How <paramref name="source"/> is turned in its parent, at rest.</summary>
    public Quaternion RestTurnOf(int source) => _rest[source].Clone();

    public RetargetLink LinkOf(int bone) => _links[bone];

    /// <summary>The turn of <paramref name="bone"/> in its parent, given how each source bone is turned in its own, by <paramref name="turn"/>.</summary>
    public Quaternion RotationOf(int bone, Func<int, Quaternion> turn)
    {
        var link = LinkOf(bone);
        return link.Left * Product(link.Down, turn).Inverted() * Product(link.Up, turn) * link.Right;
    }

    /// <summary>Whether the whole source at rest puts <paramref name="bone"/> anywhere but where it rests. Only <see cref="FixesRest"/> can make that so.</summary>
    public bool RestsElsewhere(int bone)
    {
        if (!FixesRest)
        {
            return false;
        }

        var off = RotationOf(bone, RestTurnOf) * To.RotationOf(bone).Inverted();
        return 1 - Math.Abs(off.W) > 1e-9;
    }

    /// <summary>How the target's parent of <paramref name="bone"/> is turned from its rest to point the way the source does. Identity unless <see cref="FixesRest"/>.</summary>
    public Quaternion ParentStanceOf(int bone)
    {
        var parent = To.Parents[bone];
        return (parent < 0 ? Quaternion.Identity() : _stance[parent]).Clone();
    }

    /// <summary>Where <paramref name="bone"/> goes in its parent when its driver is at <paramref name="local"/> in its.</summary>
    public Vector3 PositionOf(int bone, Vector3 local)
    {
        var moved = ChangeOf(bone, local - From.PositionOf(Drivers[bone]));
        moved.Add(To.PositionOf(bone));
        return moved;
    }

    /// <summary>
    /// A move or a slope of the driver, as the same move of <paramref name="bone"/>: carried through the
    /// world between the two parents, and made as much larger as the target is. Where the parents stand is
    /// not part of a move.
    /// </summary>
    public Vector3 ChangeOf(int bone, Vector3 change) => _across[bone].Rotated3(change.Scaled(Size));

    /// <summary>Where <paramref name="bone"/> goes in scale when its driver is at <paramref name="scale"/>.</summary>
    public Vector3 ScaleOf(int bone, Vector3 scale)
    {
        var rest = From.ScaleOf(Drivers[bone]);
        var mine = To.ScaleOf(bone);
        return new Vector3(mine.X * Per(scale.X, rest.X), mine.Y * Per(scale.Y, rest.Y), mine.Z * Per(scale.Z, rest.Z));
    }

    private static double Per(double value, double at) => Math.Abs(at) < 1e-12 ? 1 : value / at;

    private static void CheckUnpaired(RestSkeleton to, IReadOnlyDictionary<string, string> bones, IReadOnlySet<string> unpaired)
    {
        foreach (var name in unpaired)
        {
            if (to.IndexOf(name) is null)
            {
                throw new ArgumentException($"\"{name}\" is not a bone of the target skeleton.", nameof(unpaired));
            }

            if (bones.ContainsKey(name))
            {
                throw new ArgumentException($"\"{name}\" is paired by hand in bones, so it cannot be left unpaired.", nameof(unpaired));
            }
        }
    }

    // F2: six parameters, one of them the list the body's notes are added to.
    private static OrderedDictionary<string, string> Named(
        RestSkeleton from,
        RestSkeleton to,
        IReadOnlyDictionary<string, string> bones,
        IReadOnlySet<string> unpaired,
        bool humanoid,
        List<string> problems)
    {
        var auto = BoneNames.Match(from, to);
        if (humanoid)
        {
            foreach (var (key, value) in ByBody(from, to, problems))
            {
                auto[key] = value;
            }
        }

        var named = new OrderedDictionary<string, string>(auto.Where(pair => !unpaired.Contains(pair.Key)));
        foreach (var (key, value) in bones)
        {
            named[key] = value;
        }

        return named;
    }

    private static OrderedDictionary<string, string> ByBody(RestSkeleton from, RestSkeleton to, List<string> problems)
    {
        foreach (var (name, skeleton) in new[] { ("source", from), ("target", to) })
        {
            problems.AddRange(HumanoidMap.Of(skeleton).Problems.Select(problem => $"In the {name} skeleton: {problem}"));
        }

        return HumanoidMap.Match(from, to);
    }

    private static Quaternion Product(IReadOnlyList<int> chain, Func<int, Quaternion> turn)
    {
        var product = Quaternion.Identity();
        foreach (var source in chain)
        {
            product *= turn(source);
        }

        return product;
    }

    private static List<List<int>> ChildrenOf(RestSkeleton skeleton)
    {
        var children = Enumerable.Range(0, skeleton.Length).Select(static _ => new List<int>()).ToList();
        for (var bone = 0; bone < skeleton.Length; bone++)
        {
            if (skeleton.Parents[bone] >= 0)
            {
                children[skeleton.Parents[bone]].Add(bone);
            }
        }

        return children;
    }

    private Matrix4 AcrossFor(int bone)
    {
        var stance = Matrix4.Compose(Vector3.Zero(), ParentStanceOf(bone), Vector3.All(1));
        var across = stance.Multiplied(To.ParentWorldOf(bone));
        across.CopyInverse(across);
        across.Multiply(From.ParentWorldOf(Drivers[bone]));
        return across;
    }

    /// <summary>A bone of the target as it would stand with the source at rest.</summary>
    private Quaternion RestWorld(int bone) => _stance[bone] * To.WorldRotationOf(bone);

    private RetargetLink Link(int bone)
    {
        var source = Drivers[bone];
        var right = From.WorldRotationOf(source).Inverted() * RestWorld(bone);
        var pivot = Pivot(bone);
        var up = PathTo(source);
        if (pivot is null)
        {
            // Nothing above drives, so the parent stays where it rests.
            var left = To.ParentWorldRotationOf(bone).Inverted() * From.ParentWorldRotationOf(up[0]);
            return new RetargetLink(source, left, right, up, []);
        }

        var held = Drivers[pivot.Value];
        var down = PathTo(held);
        while (up.Count > 0 && down.Count > 0 && up[0] == down[0])
        {
            up.RemoveAt(0);
            down.RemoveAt(0);
        }

        var between = To.WorldRotationOf(pivot.Value).Inverted() * To.ParentWorldRotationOf(bone);
        var offset = From.WorldRotationOf(held).Inverted() * RestWorld(pivot.Value);
        return new RetargetLink(source, between.Inverted() * offset.Inverted(), right, up, down);
    }

    /// <summary>The nearest bone above <paramref name="bone"/> that something drives.</summary>
    private int? Pivot(int bone)
    {
        for (var up = To.Parents[bone]; up >= 0; up = To.Parents[up])
        {
            if (Drivers.ContainsKey(up))
            {
                return up;
            }
        }

        return null;
    }

    /// <summary>The source bones from a root down to <paramref name="source"/>.</summary>
    private List<int> PathTo(int source)
    {
        var path = new List<int>();
        for (var up = source; up >= 0; up = From.Parents[up])
        {
            path.Insert(0, up);
        }

        return path;
    }

    /// <summary>
    /// Each bone's rotation in the world, once the driven ones are turned to point the way their drivers do.
    /// Parents come first, and a bone with nothing to point at goes where its parent takes it.
    /// </summary>
    private List<Quaternion> Stances()
    {
        var stances = new List<Quaternion>();
        for (var bone = 0; bone < To.Length; bone++)
        {
            var parent = To.Parents[bone];
            var inherited = parent < 0 ? Quaternion.Identity() : stances[parent];
            stances.Add(Swing(bone, inherited) * inherited);
        }

        return stances;
    }

    /// <summary>
    /// The turn that points <paramref name="bone"/> where its driver points, from where its parent has left
    /// it. Identity where there is one limb to follow no more than one way, which is why a hip or a shoulder
    /// girdle is left alone.
    /// </summary>
    private Quaternion Swing(int bone, Quaternion inherited)
    {
        if (AimOf(bone) is not { } aim)
        {
            return Quaternion.Identity();
        }

        var mine = inherited.AsRotationMatrix().Transformed(aim.Mine);
        var theirs = aim.Theirs;
        if (mine.Length < 1e-9 || theirs.Length < 1e-9)
        {
            return Quaternion.Identity();
        }

        mine.Normalize();
        theirs.Normalize();
        if (mine.Dot(theirs) < -0.7)
        {
            _problems.Add($"\"{To.Names[bone]}\" points more than 134 degrees from the bone that drives it at rest, so its rest was left as it is.");
            return Quaternion.Identity();
        }

        return Quaternion.FromTwoVectors(mine, theirs);
    }

    /// <summary>
    /// Where <paramref name="bone"/> points at rest, and where its driver does, or null when there is no one
    /// bone below it that is driven by a bone below its driver.
    /// </summary>
    private Aim? AimOf(int bone)
    {
        if (!Drivers.TryGetValue(bone, out var source))
        {
            return null;
        }

        var below = DrivenBelow(bone);
        if (below.Count != 1)
        {
            return null;
        }

        var far = Drivers[below[0]];
        return IsBelow(far, source)
            ? new Aim(To.OriginOf(below[0]) - To.OriginOf(bone), From.OriginOf(far) - From.OriginOf(source))
            : null;
    }

    /// <summary>The nearest driven bone on each branch below <paramref name="bone"/>.</summary>
    private List<int> DrivenBelow(int bone)
    {
        var found = new List<int>();
        Visit(bone, found);
        return found;
    }

    private void Visit(int at, List<int> found)
    {
        foreach (var kid in _children[at])
        {
            if (Drivers.ContainsKey(kid))
            {
                found.Add(kid);
            }
            else
            {
                Visit(kid, found);
            }
        }
    }

    private bool IsBelow(int bone, int ancestor)
    {
        for (var up = From.Parents[bone]; up >= 0; up = From.Parents[up])
        {
            if (up == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct Aim(Vector3 Mine, Vector3 Theirs);
}

using System.Collections.ObjectModel;
using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>Thrown when a skeleton describes something that is not a tree.</summary>
public class ArmatureException(string message) : InvalidOperationException(message);

/// <summary>
/// A skeleton in its rest pose. The rest pose is the shape the mesh was bound in. Everything an
/// animator does is expressed as a departure from it, which is why it is stored separately rather
/// than being wherever the bones happen to be.
/// </summary>
public sealed class Armature
{
    private readonly OrderedDictionary<string, Bone> _bones = [];
    private List<string>? _order;

    public Armature(IEnumerable<Bone>? bones = null)
    {
        foreach (var bone in bones ?? [])
        {
            Add(bone);
        }
    }

    public IEnumerable<Bone> Bones => _bones.Values;

    public int Length => _bones.Count;

    public Bone? this[string name] => _bones.GetValueOrDefault(name);

    public bool Contains(string name) => _bones.ContainsKey(name);

    public void Add(Bone bone)
    {
        if (_bones.ContainsKey(bone.Name))
        {
            throw new ArmatureException($"There is already a bone called \"{bone.Name}\".");
        }

        _bones[bone.Name] = bone;
        _order = null;
    }

    public void Remove(string name)
    {
        if (!_bones.Remove(name))
        {
            return;
        }

        // Orphaned children become roots rather than dangling. A rig that loses a bone should keep
        // evaluating, so the mistake is visible as a limb in the wrong place rather than as an
        // exception during playback.
        foreach (var bone in _bones.Values)
        {
            if (bone.Parent == name)
            {
                bone.Parent = null;
            }
        }

        _order = null;
    }

    /// <summary>Bones with no parent.</summary>
    public IEnumerable<Bone> Roots => _bones.Values.Where(bone => bone.Parent is null);

    public IEnumerable<Bone> ChildrenOf(string name) => _bones.Values.Where(bone => bone.Parent == name);

    /// <summary>
    /// Bone names with every parent before every child. Computed once and kept, because evaluation
    /// runs every frame and the hierarchy changes when a rig is built rather than when it is played.
    /// </summary>
    public IReadOnlyList<string> EvaluationOrder => _order ??= SortByDepth();

    private List<string> SortByDepth()
    {
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var order = new List<string>();

        void Visit(Bone bone)
        {
            if (visited.Contains(bone.Name))
            {
                return;
            }

            if (!visiting.Add(bone.Name))
            {
                throw new ArmatureException(
                    $"Bone \"{bone.Name}\" is its own ancestor. A skeleton has to be a tree, and a loop here would evaluate forever.");
            }

            if (bone.Parent is { } parentName)
            {
                var parent = _bones.GetValueOrDefault(parentName)
                    ?? throw new ArmatureException(
                        $"Bone \"{bone.Name}\" names a parent \"{parentName}\" that is not in this armature.");
                Visit(parent);
            }

            visiting.Remove(bone.Name);
            visited.Add(bone.Name);
            order.Add(bone.Name);
        }

        foreach (var bone in _bones.Values)
        {
            Visit(bone);
        }

        return order;
    }

    /// <summary>Where a bone rests, in armature space.</summary>
    public Matrix4 RestOf(string name) =>
        (_bones.GetValueOrDefault(name) ?? throw new ArmatureException($"No bone called \"{name}\".")).RestMatrix;

    /// <summary>
    /// Where a bone rests relative to its parent. This is what a pose is applied on top of. Storing
    /// it relative means a bone keeps its offset from its parent when the parent moves, which is the
    /// entire point of a hierarchy.
    /// </summary>
    public Matrix4 RestLocalOf(string name)
    {
        var bone = _bones.GetValueOrDefault(name) ?? throw new ArmatureException($"No bone called \"{name}\".");

        if (bone.Parent is not { } parentName || _bones.GetValueOrDefault(parentName) is not { } parent)
        {
            return bone.RestMatrix;
        }

        return Matrix4.Inverted(parent.RestMatrix).Multiplied(bone.RestMatrix);
    }

    /// <summary>Every bone the mesh is actually bound to.</summary>
    public IEnumerable<Bone> DeformingBones => _bones.Values.Where(b => b.Deform);

    public Armature Copy() => new(_bones.Values.Select(bone => bone.Copy()).ToList());
}

/// <summary>
/// A bone's departure from its rest pose. Local to the rest, not to the armature: a rotation here
/// turns the bone about its own joint, which is what an animator means by rotating a bone.
/// </summary>
public sealed class PoseTransform(Vector3? location = null, Quaternion? rotation = null, Vector3? scale = null)
{
    public Vector3 Location { get; set; } = location ?? Vector3.Zero();

    public Quaternion Rotation { get; set; } = rotation ?? Quaternion.Identity();

    public Vector3 Scale { get; set; } = scale ?? new Vector3(1, 1, 1);

    public bool IsRest =>
        Location.Length2 < 1e-18
        && Math.Abs(Math.Abs(Rotation.W) - 1) < 1e-9
        && Math.Abs(Scale.X - 1) < 1e-9
        && Math.Abs(Scale.Y - 1) < 1e-9
        && Math.Abs(Scale.Z - 1) < 1e-9;

    public Matrix4 Matrix => Matrix4.Compose(Location, Rotation, Scale);

    public void Reset()
    {
        Location.SetZero();
        Rotation.SetValues(0, 0, 0, 1);
        Scale.SetValues(1, 1, 1);
    }

    public PoseTransform Copy() => new(Location.Clone(), Rotation.Clone(), Scale.Clone());
}

/// <summary>
/// A two-bone chain that reaches for something. Named rather than constrained per bone, because
/// inverse kinematics is one answer about two bones at once. Solving it as two independent
/// constraints means each is guessing at what the other will do.
/// </summary>
// F2: eight parameters, called by name.
public sealed class IkChain(
    string root,
    string mid,
    string target,
    string? pole = null,
    double influence = 1,
    string? influenceProperty = null,
    double stretch = 0,
    string? stretchProperty = null)
{
    /// <summary>The upper bone, a thigh or an upper arm.</summary>
    public string Root { get; } = root;

    /// <summary>The lower bone. Must be a child of <see cref="Root"/>.</summary>
    public string Mid { get; } = mid;

    /// <summary>The bone whose head the chain reaches for.</summary>
    public string Target { get; } = target;

    /// <summary>
    /// The bone whose head decides which way the joint bends. Without one the chain still solves, but
    /// a knee may end up bending backwards.
    /// </summary>
    public string? Pole { get; } = pole;

    public double Influence { get; } = influence;

    /// <summary>
    /// A pose property to take the influence from, which is how a limb is handed between inverse and
    /// forward kinematics.
    /// </summary>
    public string? InfluenceProperty { get; } = influenceProperty;

    /// <summary>How far the limb may stretch past full extension, from zero to one.</summary>
    public double Stretch { get; } = stretch;

    /// <summary>
    /// A pose property to take the stretch from, so an animator can turn it off for a shot where the
    /// silhouette matters more than the reach.
    /// </summary>
    public string? StretchProperty { get; } = stretchProperty;

    public double StretchIn(Pose pose)
    {
        var value = StretchProperty is null ? Stretch : pose.Property(StretchProperty) ?? Stretch;
        return Doubles.Clamp(value, 0.0, 1.0);
    }

    public double InfluenceIn(Pose pose)
    {
        var value = InfluenceProperty is null ? Influence : pose.Property(InfluenceProperty) ?? Influence;
        return Doubles.Clamp(value, 0.0, 1.0);
    }
}

/// <summary>
/// An armature, posed. Holds a transform per bone and works out where every bone ends up. Kept apart
/// from the armature itself so one skeleton can be posed many times over. A crowd is one rest pose
/// and a hundred poses, not a hundred skeletons.
/// </summary>
public sealed class Pose
{
    private readonly Dictionary<string, PoseTransform> _transforms = [];
    private readonly Dictionary<string, Matrix4> _world = [];
    private readonly Dictionary<string, List<BoneConstraint>> _constraints = [];
    private readonly OrderedDictionary<string, double> _properties = [];
    private readonly List<IkChain> _ikChains = [];
    private List<string>? _order;

    public Pose(Armature armature)
    {
        Armature = armature;
        foreach (var bone in armature.Bones)
        {
            _transforms[bone.Name] = new PoseTransform();
        }
    }

    public Armature Armature { get; }

    /// <summary>
    /// A named value constraints can read, such as an inverse kinematics blend. Lives on the pose
    /// rather than on a bone because it is animated like anything else and read by several bones at
    /// once. A limb's switch is one number that a dozen constraints consult.
    /// </summary>
    public double? Property(string name) => _properties.TryGetValue(name, out var value) ? value : null;

    public void SetProperty(string name, double value) => _properties[name] = value;

    public IReadOnlyDictionary<string, double> Properties =>
        new ReadOnlyDictionary<string, double>(new OrderedDictionary<string, double>(_properties));

    /// <summary>
    /// Adds a constraint to a bone. They apply in the order they were added, each seeing what the
    /// last one produced, the same as stacking them in any rigging tool, and the reason order is
    /// worth thinking about.
    /// </summary>
    public void Constrain(string bone, BoneConstraint constraint)
    {
        if (!_constraints.TryGetValue(bone, out var list))
        {
            _constraints[bone] = list = [];
        }

        list.Add(constraint);
        _order = null;
    }

    public IReadOnlyList<BoneConstraint> ConstraintsOn(string bone) =>
        _constraints.TryGetValue(bone, out var list) ? list.ToArray() : [];

    /// <summary>Adds a chain that reaches for a target.</summary>
    public void AddIkChain(IkChain chain)
    {
        _ikChains.Add(chain);
        _order = null;
    }

    public IReadOnlyList<IkChain> IkChains => _ikChains.ToArray();

    public void ClearConstraints(string? bone = null)
    {
        if (bone is null)
        {
            _constraints.Clear();
        }
        else
        {
            _constraints.Remove(bone);
        }

        _order = null;
    }

    /// <summary>
    /// The order bones are worked out in, accounting for both the hierarchy and what each constraint
    /// reads. A rig is a graph rather than a tree: a bone aimed at its cousin depends on that cousin
    /// as surely as it depends on its parent. Ordering by parents alone would read a matrix that has
    /// not been computed yet, which fails either loudly or, worse, quietly, one frame behind.
    /// </summary>
    public IReadOnlyList<string> EvaluationOrder => _order ??= SortWithConstraints();

    private List<string> SortWithConstraints()
    {
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();
        var order = new List<string>();

        void Visit(string name, string? because)
        {
            if (visited.Contains(name))
            {
                return;
            }

            if (!visiting.Add(name))
            {
                var by = because is null ? string.Empty : $", by way of \"{because}\"";
                throw new ArmatureException(
                    $"Evaluating \"{name}\" needs itself{by}. A constraint that closes a loop has no order that satisfies it.");
            }

            foreach (var before in Prerequisites(name))
            {
                Visit(before, name);
            }

            visiting.Remove(name);
            visited.Add(name);
            order.Add(name);
        }

        // Seeded from the armature's own order so a rig with no constraints comes out in the same
        // order it always did.
        foreach (var name in Armature.EvaluationOrder)
        {
            Visit(name, null);
        }

        return order;
    }

    // The parent first, then whatever each constraint reads. A name the armature lacks is skipped.
    private IEnumerable<string> Prerequisites(string name)
    {
        if (Armature[name] is not { } bone)
        {
            yield break;
        }

        if (bone.Parent is { } parent && Armature.Contains(parent))
        {
            yield return parent;
        }

        foreach (var constraint in _constraints.GetValueOrDefault(name) ?? [])
        {
            foreach (var dependency in constraint.Dependencies.Where(Armature.Contains))
            {
                yield return dependency;
            }
        }
    }

    /// <summary>
    /// The transform for a bone. Refuses a name the armature does not have. Returning a fresh
    /// transform instead would make a typo do nothing at all, quietly, which is exactly how a control
    /// that was renamed goes on looking connected while driving nothing.
    /// </summary>
    public PoseTransform this[string name]
    {
        get
        {
            if (_transforms.TryGetValue(name, out var existing))
            {
                return existing;
            }

            if (!Armature.Contains(name))
            {
                throw new ArmatureException($"No bone called \"{name}\" in this armature, so there is nothing to pose.");
            }

            return _transforms[name] = new PoseTransform();
        }
    }

    /// <summary>Puts every bone back at rest.</summary>
    public void Reset()
    {
        foreach (var transform in _transforms.Values)
        {
            transform.Reset();
        }
    }

    /// <summary>Works out where every bone is, parents first.</summary>
    public void Evaluate()
    {
        EvaluateOnce();
        if (_ikChains.Count == 0)
        {
            return;
        }

        // Inverse kinematics writes rotations back into the pose rather than overwriting world
        // matrices, so everything below a solved limb, a hand's fingers or a foot's toes, comes along
        // on the ordinary pass that follows.
        SolveIkChains();
        EvaluateOnce();
    }

    private void EvaluateOnce()
    {
        foreach (var name in EvaluationOrder)
        {
            EvaluateBone(name);
        }
    }

    private void EvaluateBone(string name)
    {
        var bone = Known(name);
        var local = Armature.RestLocalOf(name).Multiplied(this[name].Matrix);

        var parent = bone.Parent is { } parentName ? _world.GetValueOrDefault(parentName) : null;

        var world = parent is null ? local : parent.Multiplied(local);

        // Constraints run before children are evaluated, so a constrained bone carries everything
        // below it. That ordering is what makes a rig a mechanism rather than a list of independent
        // parts.
        foreach (var constraint in _constraints.GetValueOrDefault(name) ?? [])
        {
            world = constraint.Apply(world, this, name);
        }

        _world[name] = world;
    }

    /// <summary>
    /// Works out where bones are again after their transforms were edited. <see cref="Evaluate"/>
    /// solves every inverse kinematics chain, and a chain at part influence blends from whatever the
    /// pose holds, so a second evaluate gives a different pose. This only reads the transforms and
    /// applies the constraints, which gives the same answer however often it runs.
    /// With <paramref name="from"/>, only that bone and the bones below it are worked out. A
    /// constraint elsewhere that reads one of them is not, until the next full refresh.
    /// </summary>
    public void Refresh(string? from = null)
    {
        if (from is null)
        {
            EvaluateOnce();
            return;
        }

        if (!Armature.Contains(from))
        {
            throw new ArmatureException($"No bone called \"{from}\" in this armature.");
        }

        var below = new HashSet<string> { from };
        foreach (var name in EvaluationOrder)
        {
            var parent = Known(name).Parent;
            var isBelow = name == from || (parent is not null && below.Contains(parent));
            if (!isBelow)
            {
                continue;
            }

            below.Add(name);
            EvaluateBone(name);
        }
    }

    /// <summary>
    /// Where a bone would be with no pose applied: its parent, times its rest offset. What a local
    /// pose rotation is measured against. Public because a constraint that limits a joint has to
    /// know where the joint rests. Measuring against the identity instead would treat every bone
    /// that is not axis-aligned as already bent, and clamp the rest pose.
    /// </summary>
    public Matrix4 BaseOf(string name)
    {
        var bone = Known(name);
        var local = Armature.RestLocalOf(name);
        var parent = bone.Parent is { } parentName ? _world.GetValueOrDefault(parentName) : null;
        return parent is null ? local : parent.Multiplied(local);
    }

    private Bone Known(string name) =>
        Armature[name] ?? throw new ArmatureException($"No bone called \"{name}\" in this armature.");

    private void SolveIkChains()
    {
        foreach (var chain in _ikChains)
        {
            var amount = chain.InfluenceIn(this);
            if (amount <= 0)
            {
                continue;
            }

            if (Armature[chain.Root] is not { } rootBone || Armature[chain.Mid] is not { } midBone)
            {
                continue;
            }

            var rootHead = HeadOf(chain.Root);
            var goal = HeadOf(chain.Target);
            // Without a pole the bend plane is whatever the solver falls back to, which is stable
            // but arbitrary. Fine for a tentacle, wrong for a knee.
            var pole = chain.Pole is null
                ? rootHead + TailOf(chain.Root) - rootHead + new Vector3(0, 0, 1)
                : HeadOf(chain.Pole);

            var solution = Ik.SolveTwoBoneIk(
                rootHead,
                pole,
                goal,
                rootBone.Length,
                midBone.Length,
                chain.StretchIn(this));

            // Only the root is scaled. Scale is inherited, so stretching the upper bone carries the
            // lower one out to the right place and lengthens it by the same factor. Scaling both
            // would stretch the lower one twice.
            //
            // Non-uniform inherited scale shears a child that is rotated relative to its parent,
            // which would be a problem if the limb were bent. It cannot be: a limb only stretches
            // once it is past full extension, and a limb past full extension is straight.
            //
            // Written every solve, including back to one, because a scale left over from a frame
            // where the limb was stretched would keep it long after the target came back into reach.
            StretchAlongY(chain.Root, solution.Stretch, amount);
            Aim(chain.Root, solution.Joint - rootHead, amount);
            _world[chain.Root] = BaseOf(chain.Root).Multiplied(this[chain.Root].Matrix);

            // The lower bone hangs from the root that has just turned, so its world is a pass old.
            // Aimed from there it points at the end from a head it no longer has, and the end lands
            // off the target.
            StretchAlongY(chain.Mid, 1, amount);
            _world[chain.Mid] = BaseOf(chain.Mid).Multiplied(this[chain.Mid].Matrix);
            Aim(chain.Mid, solution.End - HeadOf(chain.Mid), amount);
            _world[chain.Mid] = BaseOf(chain.Mid).Multiplied(this[chain.Mid].Matrix);
        }
    }

    /// <summary>Stretches a bone along its own length, keeping its volume.</summary>
    private void StretchAlongY(string name, double factor, double amount)
    {
        var blended = 1 + ((factor - 1) * amount);
        this[name].Scale.SetFrom(Ik.StretchScale(blended));
    }

    /// <summary>Turns a bone so it points a given way, by writing its local rotation.</summary>
    private void Aim(string name, Vector3 direction, double amount)
    {
        var local = Ik.AimedRotation(_world[name], BaseOf(name), direction);
        if (local is null)
        {
            return;
        }

        this[name].Rotation = amount >= 1 ? local : Blend(this[name].Rotation, local, amount);
    }

    private static Quaternion Blend(Quaternion a, Quaternion b, double t)
    {
        var dot = (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W);
        var target = b;
        if (dot < 0)
        {
            target = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
        }

        var result = new Quaternion(
            a.X + ((target.X - a.X) * t),
            a.Y + ((target.Y - a.Y) * t),
            a.Z + ((target.Z - a.Z) * t),
            a.W + ((target.W - a.W) * t));
        result.Normalize();
        return result;
    }

    /// <summary>Where a bone ended up, in armature space. Call <see cref="Evaluate"/> first.</summary>
    public Matrix4 WorldOf(string name) =>
        _world.GetValueOrDefault(name) ?? throw new ArmatureException($"No bone called \"{name}\".");

    /// <summary>Where a bone's head ended up.</summary>
    public Vector3 HeadOf(string name) => WorldOf(name).GetTranslation();

    /// <summary>Where a bone's tail ended up.</summary>
    public Vector3 TailOf(string name)
    {
        var world = WorldOf(name);
        return world.Transform3(new Vector3(0, Known(name).Length, 0));
    }

    /// <summary>
    /// The matrix that moves a vertex from its bound position to its posed one. Where the bone is
    /// now, undoing where it rested. A vertex bound to a bone at rest multiplied by this lands where
    /// the pose puts it, which is the whole of skinning in one product.
    /// </summary>
    public Matrix4 SkinningOf(string name) =>
        WorldOf(name).Multiplied(Matrix4.Inverted(Armature.RestOf(name)));

    /// <summary>Skinning matrices for every deforming bone, in evaluation order.</summary>
    public OrderedDictionary<string, Matrix4> SkinningMatrices()
    {
        var result = new OrderedDictionary<string, Matrix4>();
        foreach (var name in Armature.EvaluationOrder)
        {
            if (Known(name).Deform)
            {
                result[name] = SkinningOf(name);
            }
        }

        return result;
    }
}

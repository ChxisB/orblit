using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// What a rig type writes into while it is generating. A rig type never touches the meta-rig it is
/// reading. Generation is a translation, and keeping the source untouched is what makes it
/// repeatable: a rig can be thrown away and rebuilt from the skeleton it came from, which is the
/// entire reason to generate one rather than build it by hand.
/// </summary>
public sealed class RigContext(Armature source, Armature output, Pose pose)
{
    // The keys are the set and the value is unused, to keep insertion order.
    private readonly OrderedDictionary<string, bool> _controls = [];
    private readonly OrderedDictionary<string, double> _defaults = [];
    private readonly OrderedDictionary<string, BoneWidget> _widgets = [];
    private readonly List<(string Bone, string Message)> _problems = [];

    /// <summary>The meta-rig being read.</summary>
    public Armature Source { get; } = source;

    /// <summary>The rig being written.</summary>
    public Armature Output { get; } = output;

    /// <summary>Where constraints, chains and properties land.</summary>
    public Pose Pose { get; } = pose;

    public IReadOnlyCollection<string> Controls => _controls.Keys;

    public IReadOnlyDictionary<string, double> Defaults => _defaults;

    /// <summary>The shape drawn for each control.</summary>
    public IReadOnlyDictionary<string, BoneWidget> Widgets => _widgets;

    /// <summary>Which bones an animator can show and hide together.</summary>
    public BoneCollections Collections { get; } = new();

    /// <summary>
    /// Things a rig type could not do, to be reported rather than thrown. A rig type that hits a
    /// problem carries on and says so. Throwing would stop generation at the first mistake, and an
    /// artist fixing a meta-rig wants the whole list. One problem per attempt is how a five minute
    /// fix becomes an afternoon.
    /// </summary>
    public IReadOnlyList<(string Bone, string Message)> Problems => _problems;

    public void Problem(string bone, string message) => _problems.Add((bone, message));

    internal Bone SourceBone(string name) =>
        Source[name] ?? throw new ArmatureException($"The meta-rig has no bone called \"{name}\".");

    /// <summary>
    /// Copies a bone out of the meta-rig. Same position and orientation, a new name and role.
    /// Everything a generated rig contains starts as one of these or as a bone derived from one, so a
    /// control is always somewhere an artist actually put something.
    /// </summary>
    // F2: six parameters, called by name.
    public string Copy(
        string sourceName,
        BoneRole role = BoneRole.Control,
        string? rename = null,
        string? parent = null,
        bool? deform = null,
        bool connected = false)
    {
        var bone = SourceBone(sourceName);

        var name = BoneNaming.Compose(
            rename ?? BoneNaming.BaseOf(sourceName),
            role,
            BoneNaming.SideOf(sourceName));

        Output.Add(new Bone(
            name,
            bone.Head.Clone(),
            bone.Tail.Clone(),
            bone.Roll,
            parent,
            connected,
            // Only deform bones deform. Everything else in a generated rig is machinery, and a mesh
            // bound to machinery is a mesh that breaks when the machinery is rebuilt.
            deform ?? role == BoneRole.Deform));

        if (role == BoneRole.Control)
        {
            _controls[name] = true;
        }

        return name;
    }

    /// <summary>
    /// Makes a bone once, however many times it is asked for. Rig types are generated one chain at a
    /// time and cannot see each other, but a few things are shared between them. The single target
    /// both eyes look at is one bone, not one per eye. The first caller builds it and the rest find
    /// it already there.
    /// </summary>
    public string CreateShared(string name, Vector3 head, Vector3 tail, BoneRole role = BoneRole.Control, string? parent = null)
    {
        if (Output.Contains(name))
        {
            return name;
        }

        return Create(name, head, tail, role, parent);
    }

    /// <summary>
    /// Makes a bone that has no counterpart in the meta-rig. Inverse kinematics targets, pole vectors
    /// and the hidden chain a solver runs on are all invented during generation. The artist never
    /// drew them.
    /// </summary>
    // F2: seven parameters, called by name.
    public string Create(
        string name,
        Vector3 head,
        Vector3 tail,
        BoneRole role = BoneRole.Mechanism,
        string? parent = null,
        double roll = 0,
        bool deform = false)
    {
        Output.Add(new Bone(name, head.Clone(), tail.Clone(), roll, parent, deform: deform));
        if (role == BoneRole.Control)
        {
            _controls[name] = true;
        }

        return name;
    }

    public void Constrain(string bone, BoneConstraint constraint) => Pose.Constrain(bone, constraint);

    /// <summary>
    /// Stretches a bone to reach another, at its current length. Wraps <see cref="Rig.StretchTo"/> so
    /// the rest length comes from the bone rather than from a number somebody has to look up and keep
    /// in step with the skeleton.
    /// </summary>
    public void StretchTo(string bone, string target, double volume = 1, string? influenceProperty = null)
    {
        var source = Output[bone]
            ?? throw new ArmatureException($"Cannot stretch \"{bone}\", which is not in this rig.");
        Constrain(
            bone,
            new Rig.StretchTo(target, source.Length, volume, influenceProperty: influenceProperty));
    }

    /// <summary>Gives a control the shape an animator grabs it by.</summary>
    public void Widget(string bone, BoneWidget widget) => _widgets[bone] = widget;

    /// <summary>Puts a bone in a collection, declaring the collection if it is new.</summary>
    public void Collect(string bone, string collection, bool visible = true, int? colour = null) =>
        Collections.Add(bone, collection, visible, colour);

    public void Ik(IkChain chain) => Pose.AddIkChain(chain);

    /// <summary>Declares a value an animator can change, with the value it starts at.</summary>
    public void Property(string name, double value)
    {
        _defaults[name] = value;
        Pose.SetProperty(name, value);
    }
}

/// <summary>
/// Turns one chain of the meta-rig into part of a working rig. The unit of a generated rig. An
/// artist marks a chain as an arm and gets everything an arm needs, forward and inverse controls,
/// the switch between them, and the deform bones underneath, without assembling any of it.
/// </summary>
public interface IRigType
{
    /// <summary>What this type is called where it is assigned.</summary>
    string Id { get; }

    /// <summary>
    /// How many bones it expects. A chain shorter than the minimum is an error worth reporting
    /// rather than a rig that half-works.
    /// </summary>
    int MinimumBones { get; }

    void Generate(RigContext context, IReadOnlyList<string> chain);
}

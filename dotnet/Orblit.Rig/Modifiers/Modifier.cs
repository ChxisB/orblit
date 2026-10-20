using System.Diagnostics.CodeAnalysis;
using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// What a modifier is told about the frame it runs in. A pose lives in the model's own space.
/// Everything outside it, a target the game holds or the ground the physics world has, lives in the
/// world's. The frame carries the matrix between them so a modifier never guesses.
/// </summary>
public sealed class ModifierFrame
{
    /// <summary>
    /// A frame <paramref name="dt"/> seconds long, for a model standing at <paramref name="modelToWorld"/>.
    /// Refuses a negative or non-finite <paramref name="dt"/> and a matrix that squashes space flat,
    /// because nothing downstream could make sense of either.
    /// </summary>
    public ModifierFrame(double dt = 0, Matrix4? modelToWorld = null)
    {
        WorldToModel = InverseOf(modelToWorld);
        if (!double.IsFinite(dt) || dt < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dt), dt, "A frame cannot last a negative or unbounded time.");
        }

        Dt = dt;
        ModelToWorld = modelToWorld?.Clone() ?? Matrix4.Identity();
    }

    /// <summary>
    /// Seconds since the last frame. Zero for a pose that is solved once. A modifier that smooths or
    /// simulates moves nothing while this is zero, because no time has passed for it to move in.
    /// </summary>
    public double Dt { get; }

    public Matrix4 ModelToWorld { get; }

    public Matrix4 WorldToModel { get; }

    /// <summary>How much longer a length is in the world than in the model.</summary>
    public double Scale
    {
        get
        {
            var column = ModelToWorld.GetColumn(0);
            return new Vector3(column.X, column.Y, column.Z).Length;
        }
    }

    public Vector3 ToWorld(Vector3 point) => ModelToWorld.Transformed3(point);

    public Vector3 ToModel(Vector3 point) => WorldToModel.Transformed3(point);

    /// <summary>A direction or an offset, which a move does not change.</summary>
    public Vector3 DirectionToWorld(Vector3 direction) => ModelToWorld.Rotated3(direction.Clone());

    public Vector3 DirectionToModel(Vector3 direction) => WorldToModel.Rotated3(direction.Clone());

    private static Matrix4 InverseOf(Matrix4? matrix)
    {
        if (matrix is null)
        {
            return Matrix4.Identity();
        }

        var inverse = Matrix4.Zero();
        var determinant = inverse.CopyInverse(matrix);
        if (determinant == 0 || !double.IsFinite(determinant))
        {
            throw new ArgumentException("This matrix squashes space flat, so nothing can be taken back through it.", nameof(matrix));
        }

        return inverse;
    }
}

/// <summary>
/// A point in the model's space, or null when there is nothing to reach for. A function and not a
/// value, because a target is somewhere the game knows this frame: another bone, the player, a sound.
/// The frame turns a point in the world into the model's space.
/// </summary>
public delegate Vector3? ModelPoint(Pose pose, ModifierFrame frame);

/// <summary>
/// One step of the stack: something that moves bones after playback. A modifier writes the bones it
/// wants moved, at full strength, and nothing else. It never blends. The stack takes the pose from
/// before the modifier and the pose after it, and puts back the share its slot's influence says.
/// That is why a modifier has no influence of its own: a second blend inside one would be multiplied
/// by the first.
/// A modifier gives the same pose when it runs twice on the same input. That is what lets it work on
/// a bone the clip does not rewrite, where the last frame's result is the input.
/// </summary>
public abstract class Modifier
{
    /// <summary>
    /// Throws an <see cref="ArmatureException"/> if this modifier names a bone the armature does not
    /// have, or joins bones the armature does not join. Called before the first solve, so a typo is an
    /// error naming the bone and not a limb that quietly never moves.
    /// </summary>
    public virtual void Check(Armature armature)
    {
    }

    /// <summary>
    /// Moves bones in <paramref name="pose"/>, which has been evaluated. Reads where bones are with
    /// <see cref="Pose.WorldOf"/> and writes with the pose's transforms, calling
    /// <see cref="Pose.Refresh"/> with <c>from</c> for the bones that hang below one it moved.
    /// </summary>
    public abstract void Solve(Pose pose, ModifierFrame frame);

    /// <summary>
    /// Forgets whatever this modifier remembers between frames. Called by the stack when the modifier
    /// is switched off or its influence is zero, so a spring that was asleep starts from the animation
    /// and not from where it was ten seconds ago. Also the call for a teleport.
    /// </summary>
    public virtual void Reset()
    {
    }
}

/// <summary>One place in a <see cref="ModifierStack"/>: a modifier and how much of it applies.</summary>
public sealed class ModifierSlot
{
    private double _influence;

    internal ModifierSlot(string name, Modifier modifier, double influence, string? influenceProperty)
    {
        Name = name;
        Modifier = modifier;
        InfluenceProperty = influenceProperty;
        _influence = Doubles.Clamp(influence, 0.0, 1.0);
    }

    public string Name { get; }

    public Modifier Modifier { get; }

    /// <summary>
    /// A pose property to take the influence from instead of <see cref="Influence"/>, so one number
    /// can be keyed, driven by a clip, or handed between two modifiers the way
    /// <see cref="BoneConstraint.InfluenceProperty"/> hands a limb between forward and inverse
    /// kinematics.
    /// </summary>
    public string? InfluenceProperty { get; }

    /// <summary>Switched off, the modifier is skipped and forgets what it remembered.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How much of the modifier's result is kept, from zero to one.</summary>
    public double Influence
    {
        get => _influence;
        set => _influence = Doubles.Clamp(value, 0.0, 1.0);
    }

    /// <summary>The influence to use this frame.</summary>
    public double InfluenceIn(Pose pose)
    {
        var value = InfluenceProperty is null ? _influence : pose.Property(InfluenceProperty) ?? _influence;
        return Doubles.Clamp(value, 0.0, 1.0);
    }
}

/// <summary>
/// The ordered list of modifiers that run between playback and skinning. The order is the order they
/// were added, and each one reads what the one before it left. A foot is placed before the head is
/// turned, so a head that follows a target does so from where the hips ended up. Limits come after
/// what they limit. Springs come last, so they follow the final pose.
/// The stack does the blending. For a slot below full influence it keeps the pose from before the
/// modifier ran and mixes each bone's transform back towards it: position and scale linearly,
/// rotation by the shorter arc. A modifier at influence one is not mixed at all.
/// The pose has to be evaluated first, and has to be a pose of bones the clip wrote this frame. A bone
/// the clip does not write keeps what the last frame's modifiers left.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The Dart name, and the rigging term. It is an ordered list, not a Stack<T>.")]
public sealed class ModifierStack
{
    private readonly List<ModifierSlot> _slots = [];
    private readonly Dictionary<string, PoseTransform> _before = [];
    private Armature? _checked;

    /// <summary>The slots in the order they run.</summary>
    public IReadOnlyList<ModifierSlot> Slots => _slots.ToArray();

    public int Length => _slots.Count;

    /// <summary>The slot called <paramref name="name"/>, or null.</summary>
    public ModifierSlot? this[string name] => _slots.Find(slot => slot.Name == name);

    /// <summary>
    /// Puts <paramref name="modifier"/> after every modifier already in the stack.
    /// <paramref name="name"/> is how a slot is found again, so it has to be unique.
    /// <paramref name="influence"/> is the share of its result that is kept, and
    /// <paramref name="influenceProperty"/> names a pose property to read it from instead.
    /// </summary>
    public ModifierSlot Add(string name, Modifier modifier, double influence = 1, string? influenceProperty = null)
    {
        if (this[name] is not null)
        {
            throw new ArgumentException($"The stack already has a modifier called \"{name}\".", nameof(name));
        }

        var slot = new ModifierSlot(name, modifier, influence, influenceProperty);
        _slots.Add(slot);
        _checked = null;
        return slot;
    }

    /// <summary>Takes the slot called <paramref name="name"/> out. Returns whether there was one.</summary>
    public bool Remove(string name)
    {
        var slot = this[name];
        if (slot is null)
        {
            return false;
        }

        slot.Modifier.Reset();
        _slots.Remove(slot);
        return true;
    }

    /// <summary>Clears what every modifier remembers.</summary>
    public void Reset()
    {
        foreach (var slot in _slots)
        {
            slot.Modifier.Reset();
        }
    }

    /// <summary>
    /// Runs every modifier on <paramref name="pose"/>, in order. The pose has to have been evaluated.
    /// When this returns it is up to date, so <see cref="Pose.WorldOf"/> and the skinning matrices read
    /// the modified bones. Evaluating again would solve every inverse-kinematics chain a second time,
    /// which is why this does not do it for you.
    /// </summary>
    public void Apply(Pose pose, ModifierFrame? frame = null)
    {
        var used = frame ?? new ModifierFrame();
        CheckAll(pose.Armature);

        foreach (var slot in _slots)
        {
            var amount = slot.Enabled ? slot.InfluenceIn(pose) : 0.0;
            if (amount <= 0)
            {
                slot.Modifier.Reset();
                continue;
            }

            if (amount >= 1)
            {
                slot.Modifier.Solve(pose, used);
                pose.Refresh();
                continue;
            }

            KeepPoseBefore(pose);
            slot.Modifier.Solve(pose, used);
            MixBack(pose, amount);
            pose.Refresh();
        }
    }

    private void CheckAll(Armature armature)
    {
        if (ReferenceEquals(_checked, armature))
        {
            return;
        }

        foreach (var slot in _slots)
        {
            slot.Modifier.Check(armature);
        }

        _checked = armature;
    }

    private void KeepPoseBefore(Pose pose)
    {
        foreach (var bone in pose.Armature.Bones)
        {
            if (!_before.TryGetValue(bone.Name, out var keep))
            {
                keep = _before[bone.Name] = new PoseTransform();
            }

            var now = pose[bone.Name];
            keep.Location.SetFrom(now.Location);
            keep.Rotation.SetFrom(now.Rotation);
            keep.Scale.SetFrom(now.Scale);
        }
    }

    private void MixBack(Pose pose, double amount)
    {
        foreach (var bone in pose.Armature.Bones)
        {
            var before = _before[bone.Name];
            var now = pose[bone.Name];
            now.Location.SetFrom(before.Location + ((now.Location - before.Location) * amount));
            now.Rotation = Ik.SlerpShortest(before.Rotation, now.Rotation, amount);
            now.Scale.SetFrom(before.Scale + ((now.Scale - before.Scale) * amount));
        }
    }
}

using System.Text.RegularExpressions;
using Orblit.VectorMath;

namespace Orblit.Rig;

internal static class RigDeform
{
    /// <summary>
    /// The deform bone for a source bone, wired to shadow its original. Every rig type ends this way,
    /// and it is the reason a rig can be rebuilt at all: the mesh is bound to bones that copy
    /// something, so the something can be replaced wholesale without the mesh noticing.
    /// </summary>
    public static string Deform(RigContext context, string sourceName, string organic, string? parent = null)
    {
        var deform = context.Copy(sourceName, BoneRole.Deform, parent: parent);
        context.Constrain(deform, new CopyTransform(organic));
        return deform;
    }
}

/// <summary>
/// One bone, one control. The plainest thing a rig type can be, and most of any rig by count: a
/// jaw, an eye, a prop socket. Worth having as a type rather than as a special case, because it
/// still wants a control an animator can find and a deform bone the mesh binds to.
/// </summary>
public sealed class CopyRig : IRigType
{
    public string Id => "basic.copy";

    public int MinimumBones => 1;

    public void Generate(RigContext context, IReadOnlyList<string> chain)
    {
        var sourceName = chain[0];

        var organic = context.Copy(sourceName, BoneRole.Original);
        var control = context.Copy(sourceName);

        var side = BoneNaming.SideOf(sourceName);
        context.Widget(control, new BoneWidget(WidgetShape.Circle, size: 1.1));
        context.Collect(
            control,
            BoneCollections.LimbCollection(BoneNaming.BaseOf(sourceName), side, null),
            colour: BoneCollections.ColourFor(side));

        context.Constrain(organic, new CopyTransform(control));
        RigDeform.Deform(context, sourceName, organic);
    }
}

/// <summary>
/// An arm or a leg: three bones, driven either way round. The type that earns a generator. A limb
/// wants forward controls for animating an arc, inverse controls for keeping a hand on a table, and
/// a way to move between them mid-shot. That is a dozen bones and twice as many constraints that
/// nobody should assemble twice, let alone mirror by hand.
/// </summary>
/// <param name="stretch">
/// How far past full extension the limb may reach, from zero to one. Off by default: stretch is a
/// choice about a character, and a rig that stretches when nobody asked reads as a bug the first
/// time an arm reaches for something.
/// </param>
public sealed class LimbRig(double stretch = 0) : IRigType
{
    public double Stretch { get; } = stretch;

    public string Id => "limbs.limb";

    public int MinimumBones => 3;

    public void Generate(RigContext context, IReadOnlyList<string> chain)
    {
        var upper = chain[0];
        var lower = chain[1];
        var end = chain[2];

        var side = BoneNaming.SideOf(upper);
        var baseName = BoneNaming.BaseOf(upper);
        var switchName = BoneNaming.Compose($"{baseName}_ik_fk", side: side);

        // Zero is inverse, one is forward. Starting on forward because that is what an animator
        // blocks a shot in. Inverse is for when something has to stay put.
        context.Property(switchName, 1);

        var stretchName = BoneNaming.Compose($"{baseName}_stretch", side: side);
        if (Stretch > 0)
        {
            context.Property(stretchName, Stretch);
        }

        var colour = BoneCollections.ColourFor(side);
        var forwardGroup = BoneCollections.LimbCollection(baseName, side, "FK");
        var inverseGroup = BoneCollections.LimbCollection(baseName, side, "IK");

        // The originals, in their own chain.
        var organicUpper = context.Copy(upper, BoneRole.Original);
        var organicLower = context.Copy(lower, BoneRole.Original, parent: organicUpper);
        var organicEnd = context.Copy(end, BoneRole.Original, parent: organicLower);

        // Forward controls: a chain an animator turns joint by joint.
        var forwardUpper = context.Copy(upper, rename: $"{baseName}_fk");
        var forwardLower = context.Copy(lower, rename: $"{BoneNaming.BaseOf(lower)}_fk", parent: forwardUpper);
        var forwardEnd = context.Copy(end, rename: $"{BoneNaming.BaseOf(end)}_fk", parent: forwardLower);

        // Inverse controls: a goal for the end of the limb, and a pole that decides which way the
        // joint bends. Without the pole a knee is as likely to bend backwards as forwards, and no
        // amount of animating fixes that.
        var endBone = context.SourceBone(end);
        var goal = context.Create(
            BoneNaming.Compose($"{BoneNaming.BaseOf(end)}_ik", side: side),
            endBone.Head,
            endBone.Tail,
            BoneRole.Control);

        var pole = context.Create(
            BoneNaming.Compose($"{baseName}_pole", side: side),
            PolePosition(context, upper, lower),
            PolePosition(context, upper, lower) + new Vector3(0, 0.2, 0),
            BoneRole.Control);

        // The chain the solver actually runs on, hidden from the animator.
        var solvedUpper = context.Copy(upper, BoneRole.Mechanism, $"{baseName}_ik");
        var solvedLower = context.Copy(lower, BoneRole.Mechanism, $"{BoneNaming.BaseOf(lower)}_ik", solvedUpper);

        context.Ik(new IkChain(
            solvedUpper,
            solvedLower,
            goal,
            pole,
            // Read from a property even when it starts at zero, so an animator can turn stretch on
            // for one shot without the rig being regenerated.
            stretch: Stretch,
            stretchProperty: Stretch > 0 ? stretchName : null));

        // Rings turn, the box is dragged, the square marks the plane the joint bends in. Three
        // shapes an animator can tell apart without reading a name.
        context.Widget(forwardUpper, new BoneWidget(WidgetShape.Circle));
        context.Widget(forwardLower, new BoneWidget(WidgetShape.Circle));
        context.Widget(forwardEnd, new BoneWidget(WidgetShape.Circle));
        context.Widget(goal, new BoneWidget(WidgetShape.Cube, size: 0.8));
        context.Widget(pole, new BoneWidget(WidgetShape.Square, size: 0.5));

        foreach (var bone in new[] { forwardUpper, forwardLower, forwardEnd })
        {
            context.Collect(bone, forwardGroup, colour: colour);
        }

        foreach (var bone in new[] { goal, pole })
        {
            context.Collect(bone, inverseGroup, colour: colour);
        }

        // The handover. Two stacks reading one number, one of them inverted: at one the originals
        // follow the forward controls, at zero they follow the solved chain, and in between they
        // cross over without anything else in the rig knowing a handover happened.
        context.Constrain(organicUpper, new CopyTransform(forwardUpper, influenceProperty: switchName));
        context.Constrain(organicUpper, new CopyTransform(solvedUpper, influenceProperty: switchName, invertInfluence: true));
        context.Constrain(organicLower, new CopyTransform(forwardLower, influenceProperty: switchName));
        context.Constrain(organicLower, new CopyTransform(solvedLower, influenceProperty: switchName, invertInfluence: true));
        context.Constrain(organicEnd, new CopyTransform(forwardEnd, influenceProperty: switchName));
        context.Constrain(organicEnd, new CopyTransform(goal, influenceProperty: switchName, invertInfluence: true));

        var deformUpper = RigDeform.Deform(context, upper, organicUpper);
        var deformLower = RigDeform.Deform(context, lower, organicLower, deformUpper);
        RigDeform.Deform(context, end, organicEnd, deformLower);
    }

    /// <summary>
    /// Puts the pole out in front of the joint, along the way it already bends. Derived from the rest
    /// pose rather than asked for, because the meta-rig already says which way the limb is bent. A
    /// straight limb in the rest pose is the thing to complain about, not a pole nobody positioned.
    /// </summary>
    private static Vector3 PolePosition(RigContext context, string upper, string lower)
    {
        var upperBone = context.SourceBone(upper);
        var lowerBone = context.SourceBone(lower);

        var root = upperBone.Head;
        var joint = lowerBone.Head;
        var end = lowerBone.Tail;

        var straight = end - root;
        straight.Normalize();
        var toJoint = joint - root;
        // How far the joint sits off the line between the two ends: the direction the limb already
        // bends in.
        var offset = toJoint - (straight * toJoint.Dot(straight));

        if (offset.Length2 < 1e-9)
        {
            // A limb modelled dead straight names no plane. Somewhere in front is a guess, and a
            // guess in a known direction beats one in a random one.
            return joint + new Vector3(0, 0, (end - root).Length);
        }

        return joint + (offset.Normalized() * (end - root).Length);
    }
}

/// <summary>
/// A finger: one control that curls the whole chain. Animating three knuckles separately is possible
/// and nobody does it. The per-segment bones still exist underneath, so a pose that needs them is
/// not locked out. The control is the fast path, not the only one.
/// </summary>
public sealed partial class FingerRig : IRigType
{
    public string Id => "limbs.finger";

    public int MinimumBones => 2;

    // Strips a trailing segment number, so a chain beginning f_index_01 gives a control called
    // f_index_curl rather than f_index_01_curl. A convention rather than a rule: the control is
    // named for the finger, and the finger is what the numbering counts through.
    [GeneratedRegex(@"_[0-9]+\z")]
    private static partial Regex Segment();

    public void Generate(RigContext context, IReadOnlyList<string> chain)
    {
        var side = BoneNaming.SideOf(chain[0]);
        var baseName = Segment().Replace(BoneNaming.BaseOf(chain[0]), string.Empty);

        var master = context.Copy(chain[0], rename: $"{baseName}_curl");
        context.Property(BoneNaming.Compose($"{baseName}_curl_amount", side: side), 1);

        context.Widget(master, new BoneWidget(WidgetShape.Circle, size: 1.4));
        context.Collect(
            master,
            BoneCollections.LimbCollection("Fingers", side, null),
            colour: BoneCollections.ColourFor(side));

        string? organicParent = null;
        string? deformParent = null;

        for (var i = 0; i < chain.Count; i++)
        {
            var organic = context.Copy(chain[i], BoneRole.Original, parent: organicParent);

            // Every segment takes the master's turn. The first follows it exactly and the rest
            // follow proportionally, which is what a finger does. Knuckles do not all bend the same
            // amount.
            context.Constrain(organic, new CopyRotation(master, influence: i == 0 ? 1.0 : 0.8));

            deformParent = RigDeform.Deform(context, chain[i], organic, deformParent);
            organicParent = organic;
        }
    }
}

/// <summary>
/// A spine: two ends, and everything between them following. Hips and chest are what an animator
/// moves. The vertebrae between them bend proportionally rather than being posed one at a time.
/// Turning the chest twists the upper back most and the lower back least, which is what a spine does
/// and what posing each bone by hand never quite achieves.
/// </summary>
public sealed class SpineRig : IRigType
{
    public string Id => "spines.spine";

    public int MinimumBones => 2;

    public void Generate(RigContext context, IReadOnlyList<string> chain)
    {
        var side = BoneNaming.SideOf(chain[0]);
        var baseName = BoneNaming.BaseOf(chain[0]);

        // One control that moves the whole torso, with the two ends under it, so walking the
        // character does not mean moving hips and chest in step.
        var torso = context.Copy(chain[0], rename: $"{baseName}_torso");
        var hips = context.Copy(chain[0], rename: "hips", parent: torso);
        var chest = context.Copy(chain[^1], rename: "chest", parent: torso);

        context.Property(BoneNaming.Compose($"{baseName}_bend", side: side), 1);

        // The torso is what a whole character is carried by, so it is the widest thing on screen and
        // shaped like nothing else in the rig.
        context.Widget(torso, new BoneWidget(WidgetShape.Arrow, size: 3));
        context.Widget(hips, new BoneWidget(WidgetShape.Circle, size: 2));
        context.Widget(chest, new BoneWidget(WidgetShape.Circle, size: 2.2));

        foreach (var bone in new[] { torso, hips, chest })
        {
            context.Collect(bone, "Torso", colour: BoneCollections.CentreColour);
        }

        string? organicParent = null;
        string? deformParent = null;

        for (var i = 0; i < chain.Count; i++)
        {
            var organic = context.Copy(chain[i], BoneRole.Original, parent: organicParent);

            // Proportional along the chain: the lowest bone is all hips, the highest all chest, and
            // the ones between are a mixture.
            var t = chain.Count == 1 ? 1.0 : (double)i / (chain.Count - 1);
            context.Constrain(organic, new CopyRotation(hips, influence: 1 - t));
            context.Constrain(organic, new CopyRotation(chest, influence: t));

            deformParent = RigDeform.Deform(context, chain[i], organic, deformParent);
            organicParent = organic;
        }
    }
}

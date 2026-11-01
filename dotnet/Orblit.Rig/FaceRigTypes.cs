using Orblit.VectorMath;

namespace Orblit.Rig;

public static class FaceRigTypes
{
    /// <summary>
    /// The collection every face control goes in, so the whole face can be hidden while a body is
    /// animated, which is most of the time.
    /// </summary>
    public const string FaceCollection = "Face";
}

/// <summary>
/// A jaw: one control that opens, and cannot open backwards. A hinge rather than a free joint. The
/// limit is the whole reason this is not a plain copy. A jaw with no limit turns inside the skull the
/// first time an animator overshoots, and it is not obvious from the curve that they did.
/// </summary>
/// <param name="maximumAngle">How far the jaw may turn from its rest, in radians.</param>
public sealed class JawRig(double maximumAngle = Math.PI / 5) : IRigType
{
    public double MaximumAngle { get; } = maximumAngle;

    public string Id => "face.jaw";

    public int MinimumBones => 1;

    public void Generate(RigContext context, IReadOnlyList<string> chain)
    {
        var sourceName = chain[0];

        var organic = context.Copy(sourceName, BoneRole.Original);
        var control = context.Copy(sourceName);

        context.Constrain(organic, new CopyTransform(control));
        context.Constrain(organic, new LimitRotation(MaximumAngle));
        context.Widget(control, new BoneWidget(WidgetShape.Circle, size: 1.3));
        context.Collect(control, FaceRigTypes.FaceCollection, colour: BoneCollections.CentreColour);

        RigDeform.Deform(context, sourceName, organic);
    }
}

/// <summary>
/// An eye: a target out in front that it looks at. Eyes are aimed, not rotated. Nobody animates an
/// eyeball by turning it. They move the thing it is looking at.
/// Each eye has its own target, and the targets hang off one shared control so a character can look
/// somewhere in a single move. Every eye also gets a second aim at the shared control itself, blended
/// in by <see cref="FocusProperty"/>. At zero the eyes stay parallel, each looking exactly where the
/// modeller pointed it. This has to be the default, because a rig with nothing posed must reproduce
/// the rest pose, and anything else changes the neutral the mesh was built in. At one both eyes aim
/// at the same point, so they converge. That is what makes a character appear to be looking at
/// something near rather than staring through it.
/// The shared control is created once however many eyes ask for it, and sits midway between them, so
/// the second eye joins the first rather than getting a control of its own.
/// </summary>
/// <param name="distance">
/// How far in front of the eye the target sits, in bone lengths. Far enough that moving it turns the
/// eye by a usable amount, near enough that it stays on screen.
/// </param>
public sealed class EyeRig(double distance = 8) : IRigType
{
    /// <summary>What the shared look-at control is called.</summary>
    public const string MasterName = "eyes";

    /// <summary>The value that crosses the eyes from parallel onto a single point.</summary>
    public const string FocusProperty = "eyes_focus";

    public double Distance { get; } = distance;

    public string Id => "face.eye";

    public int MinimumBones => 1;

    public void Generate(RigContext context, IReadOnlyList<string> chain)
    {
        var sourceName = chain[0];
        var side = BoneNaming.SideOf(sourceName);
        var baseName = BoneNaming.BaseOf(sourceName);

        var eye = context.SourceBone(sourceName);
        var ahead = eye.Head + (eye.Direction * (eye.Length * Distance));

        // Midway between the two eyes, worked out from the mirrored bone rather than by assuming
        // which axis the body is symmetrical about. A single or central eye simply uses its own.
        var mirror = BoneNaming.Mirror(sourceName);
        var other = mirror is null ? null : context.Source[mirror];
        var centre = other is null
            ? ahead
            : (ahead + other.Head + (other.Direction * (other.Length * Distance))).Scaled(0.5);

        // One control both eyes hang off, so a character can look somewhere in one move rather than
        // two that have to agree.
        var master = context.CreateShared(MasterName, centre, centre + new Vector3(0, eye.Length, 0));

        // Each eye keeps its own target under the shared one, sitting exactly on the eye's own axis
        // so that an unposed rig reproduces the rest pose.
        var target = context.Create(
            BoneNaming.Compose($"{baseName}_target", side: side),
            ahead,
            ahead + new Vector3(0, eye.Length * 0.5, 0),
            BoneRole.Control,
            parent: master);

        context.Property(FocusProperty, 0);

        var organic = context.Copy(sourceName, BoneRole.Original);
        // Two aims reading one number, one of them inverted: the same shape as a limb's
        // inverse-forward switch. At zero the eye follows its own target and the pair stay parallel.
        // At one both follow the shared control and converge on it.
        context.Constrain(organic, new DampedTrack(target, influenceProperty: FocusProperty, invertInfluence: true));
        context.Constrain(organic, new DampedTrack(master, influenceProperty: FocusProperty));

        RigDeform.Deform(context, sourceName, organic);

        context.Widget(master, new BoneWidget(WidgetShape.Square, size: 4));
        context.Widget(target, new BoneWidget(WidgetShape.Circle, size: 1.5));
        context.Collect(master, FaceRigTypes.FaceCollection, colour: BoneCollections.CentreColour);
        context.Collect(target, FaceRigTypes.FaceCollection, colour: BoneCollections.ColourFor(side));
    }
}

/// <summary>
/// An eyelid: follows the eye part of the way, and closes on its own. A lid that ignored the eye
/// would slide off the eyeball as it turned, and one that followed it exactly would never blink
/// independently. Partial following plus its own control is what a lid actually does.
/// </summary>
/// <param name="eye">
/// The bone in the meta-rig this lid belongs to. Named rather than guessed, because an upper and a
/// lower lid sit either side of the same eye and nothing in the skeleton says which eye that is.
/// </param>
/// <param name="follow">
/// How much of the eye's rotation the lid takes, from zero to one. Around a third is the usual
/// answer: enough that the lid stays with the eye, little enough that looking down does not close it.
/// </param>
public sealed class EyelidRig(string eye, double follow = 0.35) : IRigType
{
    public string Eye { get; } = eye;

    public double Follow { get; } = follow;

    public string Id => "face.eyelid";

    public int MinimumBones => 1;

    public void Generate(RigContext context, IReadOnlyList<string> chain)
    {
        var sourceName = chain[0];
        var side = BoneNaming.SideOf(sourceName);

        var organic = context.Copy(sourceName, BoneRole.Original);
        var control = context.Copy(sourceName);

        context.Constrain(organic, new CopyTransform(control));

        // Checked against the meta-rig, not the generated one. The eye may not have been generated
        // yet, because assignments run in name order, and testing the output would make this lid
        // silently stop following depending on what the eye happened to be called.
        if (!context.Source.Contains(Eye))
        {
            context.Problem(sourceName, $"follows an eye called \"{Eye}\", which is not in the meta-rig");
        }
        else
        {
            // The eye's original, which is where the aiming ended up. Following the control instead
            // would miss everything the look-at target did.
            context.Constrain(
                organic,
                new CopyRotation(
                    BoneNaming.AsRole(Eye, BoneRole.Original),
                    influence: Doubles.Clamp(Follow, 0.0, 1.0)));
        }

        RigDeform.Deform(context, sourceName, organic);

        context.Widget(control, new BoneWidget(WidgetShape.Circle, size: 0.9));
        context.Collect(control, FaceRigTypes.FaceCollection, colour: BoneCollections.ColourFor(side));
    }
}

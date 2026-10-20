using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// A shot, not a camera. There is one real camera; these describe what it should be doing. A
/// scene holds as many as it has situations, each already framed, and the engine picks between
/// them, which is why cutting to a different angle is raising a number rather than moving anything.
/// </summary>
public sealed class VirtualCamera
{
    // F2: only the name is required. The rest are options, called by name, as Dart's named parameters were.
    public VirtualCamera(
        string name,
        long priority = 10,
        bool enabled = true,
        Lens? lens = null,
        ICameraTarget? follow = null,
        ICameraTarget? lookAt = null,
        ICameraBody? body = null,
        ICameraAim? aim = null,
        CameraNoise? noise = null,
        Vector3? position = null,
        Quaternion? rotation = null)
    {
        Name = name;
        Priority = priority;
        Enabled = enabled;
        Lens = lens ?? new Lens();
        Follow = follow;
        LookAt = lookAt;
        Body = body ?? new StaticBody(position ?? Vector3.Zero());
        Aim = aim ?? new StaticAim(rotation ?? Quaternion.Identity());
        Noise = noise;
        State = new CameraState(position ?? Vector3.Zero(), rotation ?? Quaternion.Identity(), Lens);
    }

    /// <summary>Used in blend rules and in the editor. Worth being a real name.</summary>
    public string Name { get; }

    /// <summary>
    /// The highest priority among the enabled cameras is the one in use. A number rather than a
    /// stack, so a camera can be armed long before it matters and take over the moment its
    /// situation arises.
    /// </summary>
    public long Priority { get; set; }

    public bool Enabled { get; set; }

    public Lens Lens { get; set; }

    /// <summary>What the camera moves with.</summary>
    public ICameraTarget? Follow { get; set; }

    /// <summary>What it points at. Often, but not always, the same thing.</summary>
    public ICameraTarget? LookAt { get; set; }

    public ICameraBody Body { get; set; }

    public ICameraAim Aim { get; set; }

    /// <summary>Optional handheld movement, applied after framing so it never fights it.</summary>
    public CameraNoise? Noise { get; set; }

    /// <summary>
    /// Where this camera would be if it were live. Kept up to date whether or not it is, so
    /// cutting to it does not start from a stale position.
    /// </summary>
    public CameraState State { get; set; }

    /// <summary>
    /// The framing rules in force, for something to draw over the frame. From whichever of the
    /// two is doing the framing: a camera frames either by turning or by moving, never both.
    /// </summary>
    public CameraGuides? Guides => Aim.Guides ?? Body.Guides;

    /// <summary>Advances this camera's own solution by <paramref name="delta"/> seconds.</summary>
    public void Solve(double delta, double aspect)
    {
        State.Lens = Lens;

        // Last frame's rotation, which is what the body has to work with: the aim has not run
        // yet, and it cannot, because what it does depends on where the body puts the camera.
        State.Position = Body.Solve(State.Position, Follow, State.Rotation, Lens, aspect, delta);
        State.Rotation = Aim.Solve(State.Rotation, State.Position, LookAt, Lens, aspect, delta);
    }

    /// <summary>
    /// Puts the camera exactly where it wants to be, with no lag. For the first frame after a
    /// camera is enabled, and for a cut: there is nothing to catch up to when it has only just
    /// started existing.
    /// </summary>
    public void Snap(double aspect) =>
        // A very large step drives every damping term to essentially one, which is arriving at once.
        Solve(1e6, aspect);
}

namespace Orblit.Filament;

/// <summary>
/// The smear a camera's shutter leaves on anything that moved while it was open. A streak is as
/// many pixels long as the thing moves in a second times the seconds the shutter stays open. It
/// makes an effect pass in the render graph and is no switch on the post process, because it
/// needs the scene's depth and the finished picture, which is what the graph hands an effect.
/// Everything it remembers between frames is kept inside the renderer. Off unless a graph asks for
/// it, and free while off.
/// </summary>
public sealed record MotionBlur
{
    /// <summary>
    /// How long the shutter is open, in seconds, or nothing for the camera's own. Following the
    /// camera means the exposure and the blur cannot disagree.
    /// </summary>
    public double? Shutter { get; init; }

    /// <summary>
    /// The longest a streak may be, in pixels, end to end. Held between 1 and 64 by the renderer,
    /// so a camera whipped round in one frame does not blur the picture into its average.
    /// </summary>
    public double MaxPixels { get; init; } = 40;

    /// <summary>Whether objects that moved blur by their own motion, or only by the camera's.</summary>
    public bool Objects { get; init; } = true;

    /// <summary>How many samples each pixel gathers along the motion near it. Odd, and held between 3 and 31 by the renderer.</summary>
    public int Samples { get; init; } = 15;

    /// <summary>
    /// How long a streak is, in pixels, for something crossing the picture at
    /// <paramref name="pixelsPerSecond"/> under a camera whose own shutter is
    /// <paramref name="cameraShutter"/>. Speed times the time the shutter is open, held to
    /// <see cref="MaxPixels"/>.
    /// </summary>
    public double Streak(double pixelsPerSecond, double cameraShutter = 1 / 125.0) =>
        Math.Min(pixelsPerSecond * (Shutter ?? cameraShutter), MaxPixels);

    /// <summary>
    /// The four numbers the pass carries in its plane, in the order the renderer reads them: the
    /// shutter (nought for the camera's own), the clamp, whether objects blur (minus one for the
    /// camera only, so that nought keeps meaning the default), and how many samples.
    /// </summary>
    public IReadOnlyList<double> Dials => [Shutter ?? 0, MaxPixels, Objects ? 1 : -1, Samples];

    /// <summary>The pass that blurs <paramref name="reads"/>, a target that kept its depth, into <paramref name="into"/> or into the frame.</summary>
    public RenderPass Pass(string name = "motion blur", string reads = "frame", string? into = null) => new()
    {
        Name = name,
        Kind = PassKind.Effect,
        Effect = PassEffect.MotionBlur,
        Reads = [reads],
        Into = into,
        Plane = Dials,
    };

    /// <summary>The shortest graph with motion blur in it: the world into a target, and the blur from there onto the screen.</summary>
    public RenderGraph Graph() => new()
    {
        Targets = [new RenderTarget { Name = "frame" }],
        Passes = [new RenderPass { Name = "world", Into = "frame" }, Pass()],
    };
}

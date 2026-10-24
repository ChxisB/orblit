namespace Orblit.Filament;

/// <summary>
/// What a pass draws into, when it is not drawing into the frame. A named image the renderer keeps
/// between passes: the scene from a mirror's point of view, a depth buffer an outline is traced
/// from, a thumbnail. It is a declaration and not an allocation. The renderer makes it when
/// something writes it, keeps it while something reads it and drops it when nothing does.
/// </summary>
public sealed record RenderTarget
{
    /// <summary>What passes call it. Unique within a graph.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Its width in pixels, or nought to follow the view's own size. Following is the right
    /// default, because a target that did not resize with the window would be sampled at the wrong
    /// scale from the first drag of its corner.
    /// </summary>
    public int Width { get; init; }

    /// <summary>Its height in pixels, or nought to follow the view's own size.</summary>
    public int Height { get; init; }

    /// <summary>
    /// A fraction of the view's size, applied when the width and height are nought. Half is the
    /// usual answer for anything blurred afterwards, and costs a quarter of the pixels.
    /// </summary>
    public double Scale { get; init; } = 1.0;

    /// <summary>
    /// Whether it keeps depth. A depth prepass writes depth and no colour, and a flat overlay
    /// writes colour and no depth. Each would otherwise pay for a buffer nothing reads.
    /// </summary>
    public bool Depth { get; init; } = true;

    /// <summary>Whether it keeps colour.</summary>
    public bool Colour { get; init; } = true;

    internal float[] Packed => Packing.Floats(Width, Height, Scale, Packing.Flag(Depth), Packing.Flag(Colour), 0);
}

/// <summary>
/// The screen space effects the renderer knows how to run. Each is a compiled shader inside the
/// renderer, so this list is what exists and not what a host can invent. The order is the
/// renderer's own numbering.
/// </summary>
public enum PassEffect
{
    /// <summary>
    /// Puts back the edge the anti aliasing took off. Temporal anti aliasing spreads a pixel's
    /// history over several frames, and the cost is a softer picture. This is a small contrast
    /// adaptive sharpen afterwards, which lifts detail back without ringing.
    /// </summary>
    Sharpen,

    /// <summary>
    /// Pass one of three of the edge based anti aliasing: where the edges are. It writes a picture
    /// of the edges, which <see cref="SmaaWeights"/> reads.
    /// </summary>
    SmaaEdges,

    /// <summary>Pass two: how much of each pixel the edge covers. It reads what <see cref="SmaaEdges"/> wrote.</summary>
    SmaaWeights,

    /// <summary>
    /// Pass three: the blend itself. It reads the original image and then the weights, in that
    /// order, so a graph lists both in the pass's reads.
    /// </summary>
    SmaaBlend,

    /// <summary>
    /// Light bouncing off what is already on the screen. Each pixel marches the depth buffer
    /// outwards along a fan of directions and credits the colour of the surface it sees as light
    /// arriving from that direction. It reads the picture and the depth of the same target, so a
    /// graph lists that target once. The plane carries four dials, each defaulting when left at
    /// nought: how far it looks in metres, how much of the bounce comes back, how solid the
    /// surfaces are taken to be, and how many directions each pixel fans along. It costs about a
    /// millisecond a direction at 800 by 600. It only knows surfaces that are on screen, and it
    /// scales the light already there, so it can tint a lit surface but never light an unlit one.
    /// </summary>
    Bounce,

    /// <summary>
    /// A target, put on the screen, and nothing else. Anything that reads what the scene drew
    /// needs the scene drawn into a target, and then needs something to put that target on the
    /// screen without changing it.
    /// </summary>
    Copy,

    /// <summary>
    /// Shafts of light through the gaps in whatever stands against the sky. Each pixel walks
    /// towards the light's place on the screen and adds up how much of the way is open sky. Open
    /// sky is read from the depth buffer, so it reads the picture and the depth of the same
    /// target. Its settings are the scene's <see cref="GodRays"/>, and at a strength of nought it
    /// is a copy. A scene with no graph of its own does not name this, because turning god rays on
    /// puts it in.
    /// </summary>
    GodRays,

    /// <summary>
    /// The picture read from a little way off, where air bends the light. It sums the scene's
    /// <see cref="Orblit.Filament.Distortion"/> values into one offset per pixel. It reads depth as
    /// well as colour, and takes the depth from the first target that kept it, which lets it take
    /// its colour from a pass that ran after the world was drawn.
    /// </summary>
    Distortion,

    /// <summary>
    /// What moved while the shutter was open, smeared along the way it moved. It reads the colour
    /// and the depth of one target, like <see cref="Bounce"/>, and is usually built by
    /// <see cref="MotionBlur.Pass"/>, because the plane carries its four dials in the order that
    /// type writes them. A frame in which nothing moved skips the inner passes and copies.
    /// </summary>
    MotionBlur,
}

/// <summary>
/// What a pass is for. Each kind is something the renderer already knows how to do, and the
/// graph decides how many of them there are, in what order and against which targets. A kind
/// nobody has implemented cannot be declared, so a graph that schedules is a graph that draws.
/// </summary>
public enum PassKind
{
    /// <summary>
    /// Everything the scene contains, lit, from the scene's own camera. Into the frame it is the
    /// ordinary picture, and into a target it is a thumbnail, a monitor or a portal.
    /// </summary>
    Scene,

    /// <summary>
    /// The scene reflected in a plane. It is the same pass from a camera mirrored about the plane,
    /// with the winding turned inside out because reflecting the world reverses which side of a
    /// triangle faces the camera.
    /// </summary>
    Reflection,

    /// <summary>
    /// A material run over every pixel of what another pass drew, rather than a camera pointed at
    /// the world. It reads a target, writes a target and draws one triangle covering the lot.
    /// Which effect it runs is the pass's effect.
    /// </summary>
    Effect,
}

/// <summary>
/// One step of a frame. A pass says what it draws, where it draws it and what must have been
/// drawn first. The order it runs in is worked out from the last of those and not declared,
/// because an order somebody maintains by hand goes wrong the first time a pass is inserted in the
/// middle, and goes wrong silently, as a frame that samples a target from the last frame.
/// </summary>
public sealed record RenderPass
{
    /// <summary>What this pass is called, and what a capture reports it as. Unique within a graph.</summary>
    public required string Name { get; init; }

    public PassKind Kind { get; init; } = PassKind.Scene;

    /// <summary>
    /// The target it writes, or null for the frame itself. Exactly one pass may write the frame,
    /// because a second would overwrite the first.
    /// </summary>
    public string? Into { get; init; }

    /// <summary>The targets it samples, which is what the schedule is worked out from.</summary>
    public IReadOnlyList<string> Reads { get; init; } = [];

    /// <summary>
    /// Which render layers it draws, as a bit field against the object's layer. This is what lets
    /// one scene serve several passes: a reflection that leaves out the water it is reflecting, or
    /// a thumbnail without the editor's own handles.
    /// </summary>
    public int Layers { get; init; } = 0xFF;

    /// <summary>
    /// Whether it happens at all. A pass switched off is skipped, and everything that reads it
    /// gets whatever it held last, rather than the graph refusing to schedule.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Whether its target is cleared first.</summary>
    public bool Clear { get; init; } = true;

    /// <summary>The mirror, for a reflection pass: a plane as <c>nx, ny, nz, d</c>. An effect keeps its dials here too.</summary>
    public IReadOnlyList<double>? Plane { get; init; }

    /// <summary>Which screen space effect this pass runs, for <see cref="PassKind.Effect"/>.</summary>
    public PassEffect? Effect { get; init; }
}

/// <summary>
/// Something a graph says that the renderer cannot do. It is reported and not thrown. A graph is
/// edited a pass at a time and half of the intermediate states are incomplete, so refusing to draw
/// at each of them would be an editor that goes black while somebody types.
/// </summary>
/// <param name="Pass">The pass it is about, or the empty string for the graph as a whole.</param>
/// <param name="What">What is wrong.</param>
public sealed record GraphProblem(string Pass, string What)
{
    public override string ToString() => Pass.Length == 0 ? What : $"{Pass}: {What}";
}

/// <summary>
/// How a frame gets put together: the passes, the targets between them and the order that falls
/// out of what each one reads. <see cref="Pipeline"/> says how much of each step happens and this
/// says which steps there are. The default is one pass into the frame, which is what the renderer
/// did before there was a graph, so a host that never mentions one draws the same frame it always
/// drew.
/// </summary>
public sealed record RenderGraph
{
    /// <summary>
    /// The most passes a graph may hold. Every pass is a view and a target the renderer allocates,
    /// and a graph built in a loop that ran away should hit something that names the problem and
    /// not the memory ceiling.
    /// </summary>
    public const int MaxPasses = 32;

    /// <summary>What the world is drawn into when god rays or distortion need a picture to work on.</summary>
    public const string ScreenTarget = "orblit.screen";

    /// <summary>What the god rays write when a distortion comes after them.</summary>
    public const string RaysTarget = "orblit.rays";

    /// <summary>Floats each pass takes on the wire.</summary>
    public const int PassStride = 13;

    /// <summary>Floats each target takes on the wire.</summary>
    public const int TargetStride = 6;

    public IReadOnlyList<RenderPass> Passes { get; init; } = [];

    public IReadOnlyList<RenderTarget> Targets { get; init; } = [];

    /// <summary>The frame as it was before anything else was possible: everything, lit, straight into the picture.</summary>
    public static RenderGraph Standard() => new() { Passes = [new RenderPass { Name = "scene" }] };

    public RenderGraph With(RenderPass pass) => this with { Passes = [.. Passes, pass] };

    public RenderGraph WithTarget(RenderTarget target) => this with { Targets = [.. Targets, target] };

    /// <summary>
    /// The passes that will run, in the order they will run in. A pass that writes a target comes
    /// before every pass that reads it. Among passes that do not depend on each other, the order
    /// they were declared in is kept, because two independent passes have no correct order and the
    /// one somebody wrote down is the one they will expect to see in a capture. The pass that
    /// writes the frame is last whatever it depends on. Empty when the graph cannot be scheduled
    /// at all, and <see cref="Problems"/> says why.
    /// </summary>
    public IReadOnlyList<RenderPass> Schedule
    {
        get
        {
            if (Problems.Any(problem => problem.Pass.Length == 0))
            {
                return [];
            }

            var running = Passes.Where(pass => pass.Enabled).ToList();
            var written = running.Where(pass => pass.Into is not null).Select(pass => pass.Into!).ToHashSet();
            var ordered = new List<RenderPass>();
            var done = new HashSet<string>();
            var remaining = running.Where(pass => pass.Into is not null).ToList();

            while (remaining.Count > 0)
            {
                var ready = remaining.Where(pass => pass.Reads.All(read => !written.Contains(read) || done.Contains(read))).ToList();
                if (ready.Count == 0)
                {
                    break;
                }

                ordered.AddRange(ready);
                done.UnionWith(ready.Select(pass => pass.Into!));
                remaining.RemoveAll(new HashSet<RenderPass>(ready, ReferenceEqualityComparer.Instance).Contains);
            }

            return [.. ordered, .. running.Where(pass => pass.Into is null)];
        }
    }

    /// <summary>
    /// Everything wrong with this graph. A problem naming a pass is that pass being dropped, and a
    /// problem naming no pass is the graph as a whole failing to schedule, with nothing running.
    /// </summary>
    public IReadOnlyList<GraphProblem> Problems =>
        [.. TooMany(), .. PassProblems(), .. FrameProblems(), .. CycleProblems()];

    /// <summary>Whether every pass can run.</summary>
    public bool IsRunnable => Problems.Count == 0;

    /// <summary>Whether this is the frame as the renderer draws it unasked: everything, straight into the picture, with nothing after it.</summary>
    public bool IsStandard =>
        Passes.Count == 0 || (Passes.Count == 1 && Passes[0] is { Kind: PassKind.Scene, Into: null, Enabled: true });

    /// <summary>
    /// This graph with god rays and distortion put into it. Only into the renderer's own graph. A
    /// scene that never built one gets the shafts it asked for without learning what a pass is,
    /// and a scene that did build one has decided its own order, so it is returned untouched and
    /// places the effect itself. With neither asked for it is this graph unchanged, which is what
    /// makes both free when off. God rays come before distortion, because hot air bends everything
    /// behind it, the shafts included.
    /// </summary>
    public RenderGraph WithScreenEffects(bool godRays = false, bool distortion = false)
    {
        if ((!godRays && !distortion) || !IsStandard)
        {
            return this;
        }

        return new RenderGraph
        {
            Targets = ScreenTargets(godRays && distortion),
            Passes = [.. ScreenPasses(Passes.Count == 0 ? null : Passes[0], godRays, distortion)],
        };
    }

    /// <summary>
    /// The targets nothing reads. Not an error, because a pass may draw into a target for a host to
    /// pick up itself and a half built graph has them constantly. They are worth showing in an
    /// editor, because the commonest reason a reflection does not appear is that the pass that
    /// should sample it does not.
    /// </summary>
    public IReadOnlyList<string> UnreadTargets
    {
        get
        {
            var read = Passes.SelectMany(pass => pass.Reads).ToHashSet();
            return [.. Targets.Where(target => !read.Contains(target.Name)).Select(target => target.Name)];
        }
    }

    /// <summary>
    /// The scheduled passes, in the order the renderer runs them. Targets travel as positions in
    /// <see cref="PackedTargets"/> and not as names, so the renderer never matches a string on a
    /// frame.
    /// </summary>
    internal float[] PackedPasses
    {
        get
        {
            var at = SceneMessage.PositionOf(Targets.Select(target => target.Name));
            return [.. Schedule.SelectMany(pass => PackPass(pass, at))];
        }
    }

    internal float[] PackedTargets => [.. Targets.SelectMany(target => target.Packed)];

    private static List<RenderTarget> ScreenTargets(bool withRays)
    {
        List<RenderTarget> targets = [new RenderTarget { Name = ScreenTarget }];
        if (withRays)
        {
            targets.Add(new RenderTarget { Name = RaysTarget, Depth = false });
        }

        return targets;
    }

    private static IEnumerable<RenderPass> ScreenPasses(RenderPass? world, bool godRays, bool distortion)
    {
        yield return new RenderPass { Name = world?.Name ?? "scene", Into = ScreenTarget, Layers = world?.Layers ?? 0xFF };

        if (godRays)
        {
            yield return new RenderPass
            {
                Name = "god rays",
                Kind = PassKind.Effect,
                Effect = PassEffect.GodRays,
                Reads = [ScreenTarget],
                Into = distortion ? RaysTarget : null,
            };
        }

        if (distortion)
        {
            yield return new RenderPass
            {
                Name = "distortion",
                Kind = PassKind.Effect,
                Effect = PassEffect.Distortion,
                // The colour to bend first, and the depth to bend it by second.
                Reads = godRays ? [RaysTarget, ScreenTarget] : [ScreenTarget],
            };
        }
    }

    private static float[] PackPass(RenderPass pass, Dictionary<string, int> targetAt)
    {
        double Slot(string? name) => name is not null && targetAt.TryGetValue(name, out var at) ? at : -1;
        // Up to four reads, which is more than any pass needs and few enough to keep the row a fixed width.
        double Read(int i) => Slot(pass.Reads.ElementAtOrDefault(i));
        double Plane(int i) => pass.Plane?.ElementAtOrDefault(i) ?? 0;

        // The effect has a slot of its own, because the plane belongs to a reflection and an
        // effect keeps its dials there. One field that means two things is read wrong once.
        return Packing.Floats(
            (int)pass.Kind, Slot(pass.Into), pass.Layers, Packing.Flag(pass.Clear),
            Read(0), Read(1), Read(2), Read(3),
            Plane(0), Plane(1), Plane(2), Plane(3),
            pass.Effect is { } effect ? (int)effect : -1);
    }

    private IEnumerable<GraphProblem> TooMany()
    {
        if (Passes.Count > MaxPasses)
        {
            yield return new GraphProblem("", $"more than {MaxPasses} passes");
        }
    }

    private IEnumerable<GraphProblem> PassProblems()
    {
        var declared = Targets.Select(target => target.Name).ToHashSet();
        var names = new HashSet<string>();
        var written = new HashSet<string>();

        foreach (var pass in Passes)
        {
            if (!names.Add(pass.Name))
            {
                yield return new GraphProblem(pass.Name, "two passes with this name");
            }

            var problems = WriteProblems(pass, declared, written)
                .Concat(ReadProblems(pass, declared))
                .Concat(BounceProblems(pass))
                .Concat(ScreenEffectProblems(pass))
                .Concat(ReflectionProblems(pass));
            foreach (var problem in problems)
            {
                yield return problem;
            }
        }
    }

    private static IEnumerable<GraphProblem> WriteProblems(RenderPass pass, HashSet<string> declared, HashSet<string> written)
    {
        if (pass.Into is not { } into)
        {
            yield break;
        }

        if (!declared.Contains(into))
        {
            yield return new GraphProblem(pass.Name, $"writes {into}, which is not a target of this graph");
        }

        if (!written.Add(into))
        {
            yield return new GraphProblem(pass.Name, $"writes {into}, which another pass already writes");
        }
    }

    private static IEnumerable<GraphProblem> ReadProblems(RenderPass pass, HashSet<string> declared)
    {
        foreach (var read in pass.Reads.Where(read => !declared.Contains(read)))
        {
            yield return new GraphProblem(pass.Name, $"reads {read}, which is not a target of this graph");
        }

        if (pass.Into is { } into && pass.Reads.Contains(into))
        {
            yield return new GraphProblem(pass.Name, "reads the target it writes");
        }
    }

    // An effect that reads the shape of the scene needs a target that kept it. The renderer skips
    // such a pass rather than sample a buffer that is not there, which draws a frame with the
    // effect silently absent, and that looks the same as the effect not working.
    private IEnumerable<GraphProblem> BounceProblems(RenderPass pass)
    {
        if (pass.Effect is not (PassEffect.Bounce or PassEffect.MotionBlur))
        {
            yield break;
        }

        foreach (var read in pass.Reads.Where(read => TargetNamed(read) is { Depth: false }))
        {
            yield return new GraphProblem(
                pass.Name,
                pass.Effect == PassEffect.Bounce
                    ? $"bounces light off {read}, which keeps no depth, so there is no telling what is in front of what"
                    : $"blurs {read}, which keeps no depth, so the camera's motion cannot be rebuilt and nothing knows what is in front of what");
        }
    }

    // God rays and distortion need depth too, but from any one of their reads and not every one.
    // The distortion takes its colour from the god rays' output, which is a picture with no
    // depth, and its depth from the world that pass read.
    private IEnumerable<GraphProblem> ScreenEffectProblems(RenderPass pass)
    {
        if (pass.Effect is PassEffect.GodRays or PassEffect.Distortion
            && pass.Reads.Count > 0
            && !pass.Reads.Any(read => TargetNamed(read)?.Depth ?? false))
        {
            yield return new GraphProblem(
                pass.Name, "reads no target that keeps depth, so there is no telling what stands in front of what");
        }
    }

    private static IEnumerable<GraphProblem> ReflectionProblems(RenderPass pass)
    {
        if (pass.Kind == PassKind.Reflection && pass.Plane is null)
        {
            yield return new GraphProblem(pass.Name, "is a reflection with no plane to reflect in");
        }
    }

    private IEnumerable<GraphProblem> FrameProblems()
    {
        var frames = Passes.Count(pass => pass.Into is null);
        if (frames == 0 && Passes.Count > 0)
        {
            yield return new GraphProblem("", "no pass draws the frame");
        }

        if (frames > 1)
        {
            yield return new GraphProblem("", $"{frames} passes draw the frame; the second would only overwrite the first");
        }
    }

    // A cycle is what is left when the ordering runs out of passes it can place. It is worked out
    // here and not in Schedule, so that Schedule stays the answer to what runs and this stays the
    // answer to why not.
    private IEnumerable<GraphProblem> CycleProblems()
    {
        var enabled = Passes.Where(pass => pass.Enabled && pass.Into is not null).ToList();
        var written = enabled.Select(pass => pass.Into!).ToHashSet();
        var settled = new HashSet<string>();
        var moved = true;
        while (moved)
        {
            moved = false;
            foreach (var pass in enabled.Where(pass => !settled.Contains(pass.Into!)))
            {
                if (pass.Reads.All(read => !written.Contains(read) || settled.Contains(read)))
                {
                    settled.Add(pass.Into!);
                    moved = true;
                }
            }
        }

        foreach (var pass in enabled.Where(pass => !settled.Contains(pass.Into!)))
        {
            yield return new GraphProblem(pass.Name, "waits on a pass that waits on it");
        }
    }

    private RenderTarget? TargetNamed(string name) => Targets.FirstOrDefault(target => target.Name == name);
}

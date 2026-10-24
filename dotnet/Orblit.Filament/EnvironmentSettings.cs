using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// The settings a volume can move, as numbers, and the resolver that moves them.
/// </summary>
/// <remarks>
/// Pure and deterministic: the same scene, volumes and point always give the same answer, bit for
/// bit, whatever order the volumes were listed in. The resolver runs on every frame for every
/// view, and an answer that depended on list order or on the last frame would flicker when an
/// editor reordered its outliner, and would draw two views of one place differently.
/// Each quantity is blended in the space it is perceived in. Colours go component by component,
/// which is correct because every colour in the engine is already linear. Light levels, the
/// ambient and the environment in lux, go in log space, because the eye sees ratios: halfway
/// between a hundred lux and ten thousand looks like a thousand and not like five thousand.
/// Exposure is in stops, which is already a logarithm, so it goes plainly. The environment's
/// rotation goes round the circle the short way, so that blending 350 degrees towards 10 passes
/// through 0 and not 180.
/// </remarks>
public sealed record EnvironmentSettings
{
    public required double FogDensity { get; init; }

    public required Vector3 FogColour { get; init; }

    public required double FogHeight { get; init; }

    public required double FogHeightFalloff { get; init; }

    public required double FogStart { get; init; }

    public required double FogMaximumOpacity { get; init; }

    public required double ExposureCompensation { get; init; }

    public required double Ambient { get; init; }

    public required Vector3 SkyColour { get; init; }

    public required double EnvironmentIntensity { get; init; }

    public required double EnvironmentRotation { get; init; }

    public required double BloomStrength { get; init; }

    public required double Saturation { get; init; }

    public required double Contrast { get; init; }

    public required double Temperature { get; init; }

    public required double Tint { get; init; }

    public required Vector3 Midtones { get; init; }

    /// <summary>
    /// Every number, in a fixed order. For comparing two resolutions and for printing one, which
    /// is how a blend is checked for a step.
    /// </summary>
    public IReadOnlyList<double> Values =>
    [
        FogDensity, FogColour.X, FogColour.Y, FogColour.Z, FogHeight, FogHeightFalloff, FogStart, FogMaximumOpacity,
        ExposureCompensation, Ambient, SkyColour.X, SkyColour.Y, SkyColour.Z, EnvironmentIntensity,
        EnvironmentRotation, BloomStrength, Saturation, Contrast, Temperature, Tint,
        Midtones.X, Midtones.Y, Midtones.Z,
    ];

    /// <summary>
    /// What <paramref name="scene"/> already says, before any volume has touched it. Effective
    /// values and not stored ones: a switched off bloom has a strength of nothing whatever its
    /// field says, and a switched off grade is neutral, so a volume that turns either on starts
    /// from what was on screen and the edge of it does not jump.
    /// </summary>
    public static EnvironmentSettings Of(RenderScene scene)
    {
        var fog = scene.Fog;
        var bloom = scene.Post.Bloom;
        var grading = scene.Post.Grading;
        return new EnvironmentSettings
        {
            FogDensity = fog.Density,
            FogColour = fog.Colour.Clone(),
            FogHeight = fog.Height,
            FogHeightFalloff = fog.HeightFalloff,
            FogStart = fog.Distance,
            FogMaximumOpacity = fog.MaximumOpacity,
            ExposureCompensation = 0,
            Ambient = scene.Sky.Ambient,
            SkyColour = scene.Sky.Colour.Clone(),
            EnvironmentIntensity = scene.Environment.Intensity,
            EnvironmentRotation = scene.Environment.Rotation,
            BloomStrength = bloom.Enabled ? bloom.Strength : 0,
            Saturation = grading.Enabled ? grading.Saturation : 1,
            Contrast = grading.Enabled ? grading.Contrast : 1,
            Temperature = grading.Enabled ? grading.Temperature : 0,
            Tint = grading.Enabled ? grading.Tint : 0,
            Midtones = grading.Enabled ? grading.Midtones.Clone() : Vector3.All(1),
        };
    }

    /// <summary>
    /// These settings with <paramref name="volumes"/> applied, as seen from <paramref name="at"/>.
    /// Lowest priority first and highest last, ties broken by key. Each one moves every setting it
    /// names from wherever the volumes before it left it towards its own value, by its influence
    /// at <paramref name="at"/>. Settings it does not name are not touched at all, not moved
    /// towards themselves and not rounded, so a volume that only fogs leaves the ambient as it was.
    /// </summary>
    public EnvironmentSettings Resolve(IReadOnlyList<EnvironmentVolume> volumes, Vector3 at)
    {
        var settings = this;
        foreach (var volume in volumes.OrderBy(one => one.Priority).ThenBy(one => one.Key))
        {
            var influence = volume.InfluenceAt(at);
            if (influence > 0)
            {
                settings = settings.Blended(volume.Overrides, influence);
            }
        }

        return settings;
    }

    /// <summary>
    /// <paramref name="scene"/> with these settings in it. Whatever came out equal to what the
    /// scene already said is handed back as the very same object, not a copy of it. Outside every
    /// volume the scene that is sent is therefore the scene the host built, down to identity, and
    /// the renderer's own "nothing moved" checks see nothing move.
    /// </summary>
    public RenderScene ApplyTo(RenderScene scene)
    {
        var basis = Of(scene);
        return scene with
        {
            Fog = FogOf(scene.Fog, basis),
            Camera = CameraOf(scene.Camera),
            Sky = SkyOf(scene.Sky, basis),
            Environment = EnvironmentOf(scene.Environment, basis),
            Post = PostOf(scene.Post, basis),
            Volumes = [],
        };
    }

    private static double Linear(double from, double? to, double weight) =>
        to is not { } target ? from : weight >= 1 ? target : from + ((target - from) * weight);

    private static Vector3 Mixed(Vector3 from, Vector3? to, double weight)
    {
        if (to is null)
        {
            return from;
        }

        // At full strength the answer is the volume's own number, exactly. a + (b - a) * 1 is not
        // always b in floating point, and inside the hall the fog is what the hall says.
        return weight >= 1
            ? to.Clone()
            : new Vector3(
                from.X + ((to.X - from.X) * weight),
                from.Y + ((to.Y - from.Y) * weight),
                from.Z + ((to.Z - from.Z) * weight));
    }

    /// <summary>
    /// Towards <paramref name="to"/> in log space, where the eye judges light. Nothing has no
    /// logarithm, so a blend to or from zero lux falls back to a straight line, which is also what
    /// it looks like: the last few lux of a light going out are invisible either way.
    /// </summary>
    private static double Logarithmic(double from, double? to, double weight)
    {
        if (to is not { } target)
        {
            return from;
        }

        if (weight >= 1)
        {
            return target;
        }

        return from <= 0 || target <= 0
            ? from + ((target - from) * weight)
            : Math.Exp(Math.Log(from) + ((Math.Log(target) - Math.Log(from)) * weight));
    }

    /// <summary>Towards <paramref name="to"/> the short way round the circle, in radians.</summary>
    private static double Angular(double from, double? to, double weight)
    {
        if (to is not { } target)
        {
            return from;
        }

        if (weight >= 1)
        {
            return target;
        }

        // Into (-pi, pi]. The remainder keeps the dividend's sign here, so a negative one is
        // brought back up by a turn.
        const double turn = 2 * Math.PI;
        var wrapped = (target - from + Math.PI) % turn;
        if (wrapped < 0)
        {
            wrapped += turn;
        }

        return from + ((wrapped - Math.PI) * weight);
    }

    private EnvironmentSettings Blended(EnvironmentOverrides one, double weight) =>
        new()
        {
            FogDensity = Linear(FogDensity, one.FogDensity, weight),
            FogColour = Mixed(FogColour, one.FogColour, weight),
            FogHeight = Linear(FogHeight, one.FogHeight, weight),
            FogHeightFalloff = Linear(FogHeightFalloff, one.FogHeightFalloff, weight),
            FogStart = Linear(FogStart, one.FogStart, weight),
            FogMaximumOpacity = Linear(FogMaximumOpacity, one.FogMaximumOpacity, weight),
            ExposureCompensation = Linear(ExposureCompensation, one.ExposureCompensation, weight),
            Ambient = Logarithmic(Ambient, one.Ambient, weight),
            SkyColour = Mixed(SkyColour, one.SkyColour, weight),
            EnvironmentIntensity = Logarithmic(EnvironmentIntensity, one.EnvironmentIntensity, weight),
            EnvironmentRotation = Angular(EnvironmentRotation, one.EnvironmentRotation, weight),
            BloomStrength = Linear(BloomStrength, one.BloomStrength, weight),
            Saturation = Linear(Saturation, one.Saturation, weight),
            Contrast = Linear(Contrast, one.Contrast, weight),
            Temperature = Linear(Temperature, one.Temperature, weight),
            Tint = Linear(Tint, one.Tint, weight),
            Midtones = Mixed(Midtones, one.Midtones, weight),
        };

    /// <summary>The fog these settings ask for, or <paramref name="fog"/> itself where they ask for nothing it does not already say.</summary>
    private Fog FogOf(Fog fog, EnvironmentSettings basis) =>
        FogDensity != basis.FogDensity
        || !FogColour.Equals(basis.FogColour)
        || FogHeight != basis.FogHeight
        || FogHeightFalloff != basis.FogHeightFalloff
        || FogStart != basis.FogStart
        || FogMaximumOpacity != basis.FogMaximumOpacity
            ? fog with
            {
                Colour = FogColour.Clone(),
                Density = FogDensity,
                Distance = FogStart,
                MaximumOpacity = FogMaximumOpacity,
                Height = FogHeight,
                HeightFalloff = FogHeightFalloff,
            }
            : fog;

    /// <summary>
    /// The camera with the exposure these settings compensate by. More stops is more light, and a
    /// longer shutter lets in more: one stop up is the shutter open twice as long.
    /// </summary>
    private RenderCamera CameraOf(RenderCamera camera) =>
        ExposureCompensation == 0
            ? camera
            : camera with { ShutterSpeed = camera.ShutterSpeed * Math.Pow(2, ExposureCompensation) };

    private Sky SkyOf(Sky sky, EnvironmentSettings basis) =>
        Ambient != basis.Ambient || !SkyColour.Equals(basis.SkyColour)
            ? sky with { Colour = SkyColour.Clone(), Ambient = Ambient }
            : sky;

    private RenderEnvironment EnvironmentOf(RenderEnvironment environment, EnvironmentSettings basis) =>
        EnvironmentIntensity != basis.EnvironmentIntensity || EnvironmentRotation != basis.EnvironmentRotation
            ? environment with { Intensity = EnvironmentIntensity, Rotation = EnvironmentRotation }
            : environment;

    private PostProcess PostOf(PostProcess post, EnvironmentSettings basis)
    {
        var bloomMoved = BloomStrength != basis.BloomStrength;
        var gradingMoved =
            Saturation != basis.Saturation
            || Contrast != basis.Contrast
            || Temperature != basis.Temperature
            || Tint != basis.Tint
            || !Midtones.Equals(basis.Midtones);
        if (!bloomMoved && !gradingMoved)
        {
            return post;
        }

        return post with
        {
            Bloom = bloomMoved
                ? post.Bloom with { Enabled = post.Bloom.Enabled || BloomStrength > 0, Strength = BloomStrength }
                : post.Bloom,
            Grading = gradingMoved ? GradingOf(post.Grading) : post.Grading,
        };
    }

    /// <summary>
    /// A grade that is on, starting from <paramref name="grading"/> where that was on and from
    /// neutral where it was not. A new value and not the host's with a field changed: the vectors
    /// are mutable, and the host's scene is still the host's.
    /// </summary>
    private Grading GradingOf(Grading grading) =>
        (grading.Enabled ? grading : new Grading { ToneMapping = grading.ToneMapping }) with
        {
            Enabled = true,
            Contrast = Contrast,
            Saturation = Saturation,
            Temperature = Temperature,
            Tint = Tint,
            Midtones = Midtones.Clone(),
        };
}

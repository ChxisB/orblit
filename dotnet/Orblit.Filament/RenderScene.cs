using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// Everything the renderer draws, described in whole every time. A value with many fields, so it
/// is changed with <c>with</c>.
/// </summary>
public sealed record RenderScene
{
    /// <summary>
    /// The highest layer an object may be on. Seven of them, because the renderer's own mask is
    /// eight bits and the top one says whether a thing is drawn at all.
    /// </summary>
    public const int MaxLayer = 6;

    public required IReadOnlyList<RenderObject> Objects { get; init; }

    public required RenderCamera Camera { get; init; }

    public IReadOnlyList<RenderLight> Lights { get; init; } = [];

    public Sky Sky { get; init; } = new();

    public IReadOnlyList<RenderMaterial> Materials { get; init; } = [];

    public IReadOnlyList<Video> Videos { get; init; } = [];

    /// <summary>
    /// Pictures the application draws itself. Keyed with the videos, as screens point at one list of
    /// both: a screen's <see cref="RenderMaterial.Video"/> is a video's key or a feed's, and a scene
    /// that gave one key to both is refused.
    /// </summary>
    public IReadOnlyList<Feed> Feeds { get; init; } = [];

    public IReadOnlyList<Decal> Decals { get; init; } = [];

    /// <summary>
    /// Thousands of copies of one mesh, drawn as a few draws. The renderer is sent a population's
    /// transforms only when its revision moves.
    /// </summary>
    public IReadOnlyList<Population> Populations { get; init; } = [];

    /// <summary>
    /// Clouds of 3D Gaussians: captured places, or generated ones. Apart from the objects and
    /// populations because they are not surfaces. They are drawn after everything solid, sorted
    /// back to front among themselves, and hidden by anything solid in front of them.
    /// </summary>
    public IReadOnlyList<SplatCloud> Splats { get; init; } = [];

    /// <summary>
    /// Layers of flat pictures: sprites, tiles, a scrolling backdrop. Drawn after everything solid,
    /// in their layers' order and then in the order each layer gives them, which is what a 2D scene
    /// means by "on top". A scene of nothing else, seen through an orthographic camera, is a 2D
    /// game drawn by the same renderer as a 3D one.
    /// </summary>
    public IReadOnlyList<SpriteLayer> Sprites { get; init; } = [];

    /// <summary>The landscapes. Each is sent in full the first time, and after that only what changed.</summary>
    public IReadOnlyList<RenderTerrain> Terrain { get; init; } = [];

    /// <summary>
    /// Places that look different from the rest of the world. Resolved against the camera before
    /// the scene is sent, so the renderer is told a fog and a sky and an exposure as it always was.
    /// </summary>
    public IReadOnlyList<EnvironmentVolume> Volumes { get; init; } = [];

    /// <summary>Which objects are drawn with a line round them. Nothing by default, and free while nothing is.</summary>
    public Outline Outline { get; init; } = Outline.None;

    /// <summary>Shafts of light from the first directional light. Off by default, and free when off.</summary>
    public GodRays GodRays { get; init; } = GodRays.Off;

    /// <summary>Air that bends the light through it. None by default, and free when none moves anything.</summary>
    public IReadOnlyList<Distortion> Distortions { get; init; } = [];

    public Fog Fog { get; init; } = Fog.None;

    public Precipitation Precipitation { get; init; } = Precipitation.None;

    public PostProcess Post { get; init; } = new();

    public Pipeline Pipeline { get; init; } = new();

    /// <summary>The passes a frame is drawn in. The standard graph is one pass that draws the scene to the screen.</summary>
    public RenderGraph Graph { get; init; } = RenderGraph.Standard();

    /// <summary>
    /// The place the scene is standing in, as light and as a backdrop. It overrules the sky's flat
    /// ambient while it is set, because a scene lit by a photograph of a room and also by an even
    /// grey wash is lit twice, and the wash is the half that flattens it.
    /// </summary>
    public RenderEnvironment Environment { get; init; } = RenderEnvironment.None;

    /// <summary>
    /// The reflections captured from points inside the scene. The camera is inside one at a time,
    /// and that one lights the scene in place of the environment, so adding probes to an existing
    /// scene changes nothing until one of them contains the camera.
    /// </summary>
    public IReadOnlyList<Probe> Probes { get; init; } = [];

    /// <summary>The light kept in the world and not on the screen. Off by default, and free when off.</summary>
    public IrradianceField Field { get; init; } = IrradianceField.None;

    /// <summary>
    /// Whether objects that share a mesh and a material are merged into fewer draws. The picture
    /// is the same either way, so the only thing at stake is time.
    /// </summary>
    public bool Batching { get; init; } = true;

    /// <summary>
    /// Whether depth is drawn first, so that colour is worked out only for what is seen. The
    /// picture is bit identical either way, so the only thing at stake is time.
    /// </summary>
    public bool DepthPrepass { get; init; }

    /// <summary>
    /// The graph the renderer is actually sent: <see cref="Graph"/>, with the passes for the god
    /// rays and the distortions put in when the graph is the standard one and something asks for them.
    /// </summary>
    public RenderGraph DrawnGraph =>
        Graph.WithScreenEffects(
            godRays: GodRays.IsOn && Sun is not null,
            distortion: Distortions.Any(one => one.IsActive));

    /// <summary>
    /// The names of the passes that will run, in order. A capture's timings line up against them,
    /// because the renderer sends back two numbers a pass and this says which pass each pair is.
    /// </summary>
    public IReadOnlyList<string> PassNames => [.. DrawnGraph.Schedule.Select(pass => pass.Name)];

    /// <summary>The light the god rays come from: the first directional one, which is the one the renderer draws.</summary>
    internal RenderLight? Sun => Lights.FirstOrDefault(light => light.Kind == RenderLightKind.Directional);

    /// <summary>
    /// This scene with its volumes folded into the fog, sky, exposure, environment and image work,
    /// as seen from <paramref name="at"/>, which is the camera where it is not given. The same
    /// scene when there are no volumes, so a host that has none pays nothing.
    /// </summary>
    public RenderScene Resolved(Vector3? at = null) =>
        Volumes.Count == 0
            ? this
            : EnvironmentSettings.Of(this).Resolve(Volumes, at ?? Camera.Position).ApplyTo(this);

    /// <summary>
    /// The scene packed into the flat arrays the renderer reads, under the names the renderer's
    /// parts agree on. <paramref name="at"/> is when the application reckons this is, in its own
    /// seconds: the renderer draws far more often than it is told anything and works out where the
    /// camera is in between, and the clock the camera was solved on is steadier than the clock a
    /// message arrives on. <paramref name="held"/> is what the renderer already has, which keeps
    /// the populations, clouds, sprite layers and terrain it holds out of the message.
    /// </summary>
    public OrderedDictionary<string, object> ToMessage(double? at = null, Held? held = null)
    {
        if (Volumes.Count > 0)
        {
            return Resolved().ToMessage(at, held);
        }

        var message = new OrderedDictionary<string, object>();
        SceneMessage.Objects(Objects, Materials, message);
        SceneMessage.Materials(Materials, Videos, Feeds, message);
        SceneMessage.Surfaces(Materials, message);
        SceneMessage.Videos(Videos, Feeds, message);
        SceneMessage.Decals(Decals, message);
        SceneMessage.Probes(Probes, message);
        SceneMessage.Lights(Lights, message);
        SceneMessage.Camera(Camera, message);
        SceneMessage.Air(Sky, Fog, Precipitation, message);
        SceneMessage.Picture(this, message);
        SceneMessage.Screen(this, message);
        message["batching"] = Batching;
        message["depthPrepass"] = DepthPrepass;
        message["at"] = at ?? 0.0;
        SceneMessage.Populations(Populations, held?.Populations, message);
        SceneMessage.Splats(Splats, held?.Splats, message);
        SceneMessage.Sprites(Sprites, held?.Sprites, message);
        SceneMessage.Terrain(Terrain, held?.Terrain, message);
        if (PoseMessage.Pack(Objects) is { } poses)
        {
            foreach (var (key, value) in poses)
            {
                message[key] = value;
            }
        }

        return message;
    }
}

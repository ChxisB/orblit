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

    public IReadOnlyList<Decal> Decals { get; init; } = [];

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
    /// The scene packed into the flat arrays the renderer reads, under the names the renderer's
    /// parts agree on. <paramref name="at"/> is when the application reckons this is, in its own
    /// seconds: the renderer draws far more often than it is told anything and works out where the
    /// camera is in between, and the clock the camera was solved on is steadier than the clock a
    /// message arrives on.
    /// </summary>
    public OrderedDictionary<string, object> ToMessage(double? at = null)
    {
        var message = new OrderedDictionary<string, object>();
        SceneMessage.Objects(Objects, Materials, message);
        SceneMessage.Materials(Materials, Videos, message);
        SceneMessage.Surfaces(Materials, message);
        SceneMessage.Videos(Videos, message);
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

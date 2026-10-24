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

    public Fog Fog { get; init; } = Fog.None;

    public Precipitation Precipitation { get; init; } = Precipitation.None;

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
    /// The scene packed into the flat arrays the renderer reads, under the names the renderer's
    /// parts agree on. <paramref name="at"/> is when the application reckons this is, in its own
    /// seconds: the renderer draws far more often than it is told anything and works out where the
    /// camera is in between, and the clock the camera was solved on is steadier than the clock a
    /// message arrives on.
    /// </summary>
    public OrderedDictionary<string, object> ToMessage(double? at = null)
    {
        var message = new OrderedDictionary<string, object>();
        SceneMessage.Objects(Objects, message);
        SceneMessage.Lights(Lights, message);
        SceneMessage.Camera(Camera, message);
        SceneMessage.Air(Sky, Fog, Precipitation, message);
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

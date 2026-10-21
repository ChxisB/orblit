using System.Collections.Frozen;

namespace Orblit.Scene;

/// <summary>
/// One fact about an entity.
/// </summary>
/// <remarks>
/// A scene used to say what an object was, a mesh, a light, a camera, and every property of every
/// kind lived on one class, so a light carried a mesh path it would never use and a camera carried
/// a shadow flag. It also meant that a thing which was two of them at once could not be said at
/// all: a lamp is a mesh and a light, and the old shape made somebody build it out of two objects
/// and keep them in step by hand.
/// <para>
/// A component says one thing, an entity has as many as it needs, and the two questions a renderer
/// asks, what is here and what do I do with it, are answered by looking at what the entity has
/// rather than at what it claims to be.
/// </para>
/// </remarks>
public abstract class SceneComponent
{
    /// <summary>The name this is written under, and the key it lives at on an entity.</summary>
    public abstract string Type { get; }

    /// <summary>
    /// The component as it goes into the file. Also what a diff compares, which is why it is the
    /// only view that has to be exhaustive: a field left out of here is a field an edit to which
    /// cannot be undone.
    /// </summary>
    public abstract JsonMap ToJson();
}

/// <summary>
/// A component this version of Orblit has never heard of.
/// </summary>
/// <remarks>
/// Kept exactly as it arrived, and written back exactly as it arrived. The case this exists for is
/// two people on one project with different versions of the editor, or a project that has a
/// component some tool of its own puts there: dropping what we do not recognise would mean the
/// newer half of a team silently deletes the older half's work every time they open a file.
/// <para>
/// It is not a licence to read newer files. A file whose format version is ahead of ours is still
/// refused, because a version bump says the keys we do recognise may have changed meaning. This is
/// for a known format carrying an unknown component, which is the ordinary case.
/// </para>
/// </remarks>
public sealed class UnknownComponent(string type, JsonMap json) : SceneComponent
{
    public override string Type { get; } = type;

    public override JsonMap ToJson() => json;
}

/// <summary>Reads a component of one type out of its JSON.</summary>
public delegate SceneComponent ComponentReader(JsonMap json);

/// <summary>Which components exist, how to read them, and the order they are written.</summary>
public static class SceneComponents
{
    public const string Transform = "transform";
    public const string Mesh = "mesh";
    public const string Material = "material";
    public const string Body = "body";
    public const string Joint = "joint";
    public const string Motion = "motion";
    public const string Zone = "zone";
    public const string Vehicle = "vehicle";
    public const string Wheel = "wheel";
    public const string Light = "light";
    public const string Camera = "camera";
    public const string Splats = "splats";
    public const string Terrain = "terrain";
    public const string Sprite = "sprite";
    public const string Tilemap = "tilemap";
    public const string Parallax = "parallax";
    public const string Weather = "weather";
    public const string Canvas = "canvas";
    public const string Particles = "particles";
    public const string SoundSource = "soundSource";
    public const string SoundListener = "soundListener";
    public const string SoundSurface = "soundSurface";

    /// <summary>
    /// The order components are written in, so two saves of one scene produce the same bytes and a
    /// diff of a scene file is a diff of what changed. Roughly outside-in: where a thing is, then
    /// what it draws and what it is simulated and animated as, then what it lights or watches with,
    /// then the flat layers, then what it carries. Anything not on this list is written after it, in
    /// the order it was read, which keeps an unknown component's position stable too.
    /// </summary>
    public static readonly IReadOnlyList<string> Order =
    [
        Transform,
        Mesh,
        Material,
        Body,
        Joint,
        Motion,
        Zone,
        Vehicle,
        Wheel,
        Light,
        Camera,
        Splats,
        Terrain,
        Sprite,
        Tilemap,
        Parallax,
        Weather,
        Canvas,
        Particles,
        SoundSource,
        SoundListener,
        SoundSurface,
    ];

    public static readonly FrozenDictionary<string, ComponentReader> Readers = new Dictionary<string, ComponentReader>
    {
        [Transform] = TransformComponent.FromJson,
        [Mesh] = MeshComponent.FromJson,
        [Material] = MaterialComponent.FromJson,
        [Body] = BodyComponent.FromJson,
        [Joint] = JointComponent.FromJson,
        [Motion] = MotionComponent.FromJson,
        [Zone] = ZoneComponent.FromJson,
        [Vehicle] = VehicleComponent.FromJson,
        [Wheel] = WheelComponent.FromJson,
        [Light] = LightComponent.FromJson,
        [Camera] = CameraComponent.FromJson,
        [Splats] = SplatsComponent.FromJson,
        [Terrain] = TerrainComponent.FromJson,
        [Sprite] = SpriteComponent.FromJson,
        [Tilemap] = TilemapComponent.FromJson,
        [Parallax] = ParallaxComponent.FromJson,
        [Weather] = WeatherComponent.FromJson,
        [Canvas] = CanvasComponent.FromJson,
        [Particles] = ParticleEmitterComponent.FromJson,
        [SoundSource] = SoundSourceComponent.FromJson,
        [SoundListener] = SoundListenerComponent.FromJson,
        [SoundSurface] = SoundSurfaceComponent.FromJson,
    }.ToFrozenDictionary();

    /// <summary>One component, typed when we know the name and kept whole when we do not.</summary>
    public static SceneComponent Read(string type, JsonMap json) =>
        Readers.TryGetValue(type, out var reader) ? reader(json) : new UnknownComponent(type, json);
}

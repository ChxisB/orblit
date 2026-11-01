using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// What a model file holds, as the renderer loaded it. It is read from the loaded model rather
/// than the file, so a file that was converted on the way in is described as what the renderer has:
/// the clips it can play, the joints it can set.
/// </summary>
public sealed record AssetInfo
{
    /// <summary>The key prefix under which the renderer reports a model's description in its notes.</summary>
    public const string NotePrefix = "orblit.model:";

    public required string Path { get; init; }

    public required Vector3 BoundsMin { get; init; }

    public required Vector3 BoundsMax { get; init; }

    /// <summary>The clips, in the order <see cref="ModelAnimation.Clip"/> counts them.</summary>
    public IReadOnlyList<ClipInfo> Clips { get; init; } = [];

    public IReadOnlyList<SkinInfo> Skins { get; init; } = [];

    /// <summary>The names of the material variants, in the order <see cref="RenderObject.Variant"/> counts them.</summary>
    public IReadOnlyList<string> Variants { get; init; } = [];

    /// <summary>The names of the file's own materials.</summary>
    public IReadOnlyList<string> Materials { get; init; } = [];

    public IReadOnlyList<FileLight> Lights { get; init; } = [];

    public IReadOnlyList<FileCamera> Cameras { get; init; } = [];

    /// <summary>What the file uses that the renderer does not draw.</summary>
    public IReadOnlyList<string> Unsupported { get; init; } = [];

    /// <summary>The clip with this name, or null if the file has none.</summary>
    public int? ClipNamed(string name)
    {
        for (var i = 0; i < Clips.Count; i++)
        {
            if (Clips[i].Name == name)
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>The first joint with this name in any skin, or null if there is none.</summary>
    public (int Skin, int Joint)? JointNamed(string name)
    {
        for (var s = 0; s < Skins.Count; s++)
        {
            for (var joint = 0; joint < Skins[s].Joints.Count; joint++)
            {
                if (Skins[s].Joints[joint] == name)
                {
                    return (s, joint);
                }
            }
        }

        return null;
    }

    /// <summary>The file's lights placed in the world at <paramref name="transform"/>, each keyed by <paramref name="keyOf"/>.</summary>
    public IReadOnlyList<RenderLight> LightsFor(Matrix4 transform, Func<int, long> keyOf, bool castShadows = true)
    {
        var placed = new List<RenderLight>(Lights.Count);
        for (var i = 0; i < Lights.Count; i++)
        {
            placed.Add(Lights[i].ToLight(transform, keyOf(i), castShadows));
        }

        return placed;
    }

    /// <summary>Reads a description. A value of the wrong type throws <see cref="InvalidCastException"/>.</summary>
    public static AssetInfo FromJson(string path, IReadOnlyDictionary<string, object?> json)
    {
        var bounds = FileJson.Map(json, "bounds");
        return new AssetInfo
        {
            Path = path,
            Clips = [.. FileJson.Maps(json, "clips").Select(ClipInfo.FromJson)],
            Skins = [.. FileJson.Maps(json, "skins").Select(SkinInfo.FromJson)],
            Variants = FileJson.Texts(json, "variants"),
            Materials = FileJson.Texts(json, "materials"),
            Lights = [.. FileJson.Maps(json, "lights").Select(FileLight.FromJson)],
            Cameras = [.. FileJson.Maps(json, "cameras").Select(FileCamera.FromJson)],
            BoundsMin = FileJson.Vector(bounds.GetValueOrDefault("min")),
            BoundsMax = FileJson.Vector(bounds.GetValueOrDefault("max")),
            Unsupported = FileJson.Texts(json, "unsupported"),
        };
    }

    /// <summary>
    /// Separates the model descriptions from the other notes. A description that does not parse, or
    /// is in a shape this version does not read, is dropped: the scene still draws.
    /// </summary>
    public static (IReadOnlyDictionary<string, string> Notes, IReadOnlyList<AssetInfo> Models) Split(
        IReadOnlyDictionary<string, string> all)
    {
        var notes = new OrderedDictionary<string, string>();
        var models = new List<AssetInfo>();
        foreach (var (key, value) in all)
        {
            if (!key.StartsWith(NotePrefix, StringComparison.Ordinal))
            {
                notes[key] = value;
                continue;
            }

            if (TryRead(key[NotePrefix.Length..], value) is { } model)
            {
                models.Add(model);
            }
        }

        return (notes, models);
    }

    private static AssetInfo? TryRead(string path, string text)
    {
        try
        {
            return JsonText.Decode(text) is IReadOnlyDictionary<string, object?> json ? FromJson(path, json) : null;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException)
        {
            return null;
        }
    }
}

/// <summary>One of a file's animation clips.</summary>
public sealed record ClipInfo(string Name, double Seconds)
{
    internal static ClipInfo FromJson(IReadOnlyDictionary<string, object?> json) =>
        new(FileJson.Text(json.GetValueOrDefault("name")), FileJson.Number(json.GetValueOrDefault("seconds")));
}

/// <summary>A skeleton: its joints by name, and where each rests.</summary>
public sealed record SkinInfo
{
    public required string Name { get; init; }

    public required IReadOnlyList<string> Joints { get; init; }

    /// <summary>For each joint, the position of its parent, or -1 for a root.</summary>
    public IReadOnlyList<long> Parents { get; init; } = [];

    /// <summary>For each joint, the transform it rests in relative to the model.</summary>
    public IReadOnlyList<Matrix4> Rest { get; init; } = [];

    /// <summary>For each joint, the transform it rests in relative to its parent.</summary>
    public IReadOnlyList<Matrix4> Local { get; init; } = [];

    internal static SkinInfo FromJson(IReadOnlyDictionary<string, object?> json) => new()
    {
        Name = FileJson.Text(json.GetValueOrDefault("name")),
        Joints = FileJson.Texts(json, "joints"),
        Parents = [.. FileJson.List(json, "parents").Select(parent => parent is null ? -1 : (long)FileJson.Number(parent))],
        Rest = [.. FileJson.List(json, "rest").Select(FileJson.Matrix)],
        Local = [.. FileJson.List(json, "local").Select(FileJson.Matrix)],
    };
}

/// <summary>A light a model file carries, in the file's own space.</summary>
public sealed record FileLight(
    string Name,
    RenderLightKind Kind,
    Vector3 Colour,
    double Intensity,
    double Falloff,
    double InnerConeAngle,
    double OuterConeAngle,
    Matrix4 Transform)
{
    /// <summary>
    /// The light placed in the world: the file's transform under <paramref name="placement"/>, aimed
    /// down its own -Z. A file that gives no falloff gets the reach at which the light fades to a
    /// tenth of a lux.
    /// </summary>
    public RenderLight ToLight(Matrix4 placement, long key, bool castShadows = true)
    {
        var world = placement.Multiplied(Transform);
        var direction = world.Transformed3(new Vector3(0, 0, -1)) - world.Transformed3(Vector3.Zero());
        return new RenderLight
        {
            Key = key,
            Kind = Kind,
            Colour = Colour,
            Intensity = Intensity,
            Position = world.GetTranslation(),
            Direction = direction.Length2 > 0 ? direction.Normalized() : new Vector3(0, -1, 0),
            FalloffRadius = Falloff > 0 ? Falloff : ReachOf(Intensity),
            InnerConeAngle = InnerConeAngle,
            OuterConeAngle = OuterConeAngle,
            CastShadows = castShadows,
        };
    }

    /// <summary>How far a light of this many lumens reaches before it is under a tenth of a lux.</summary>
    public static double ReachOf(double lumens) => Math.Sqrt(lumens / (4 * Math.PI) / 0.1);

    internal static FileLight FromJson(IReadOnlyDictionary<string, object?> json) => new(
        FileJson.Text(json.GetValueOrDefault("name")),
        (RenderLightKind)Math.Clamp(json.GetValueOrDefault("kind") is { } kind ? (long)FileJson.Number(kind) : 1, 0, 2),
        FileJson.Vector(json.GetValueOrDefault("colour"), fallback: 1),
        FileJson.Number(json.GetValueOrDefault("intensity")),
        FileJson.Number(json.GetValueOrDefault("falloff")),
        FileJson.Number(json.GetValueOrDefault("inner")),
        FileJson.Number(json.GetValueOrDefault("outer")),
        FileJson.Matrix(json.GetValueOrDefault("transform")));
}

/// <summary>A camera a model file carries.</summary>
public sealed record FileCamera(
    string Name,
    bool Orthographic,
    double FieldOfView,
    double ViewHeight,
    double Near,
    double Far,
    Matrix4 Transform)
{
    internal static FileCamera FromJson(IReadOnlyDictionary<string, object?> json) => new(
        FileJson.Text(json.GetValueOrDefault("name")),
        json.GetValueOrDefault("orthographic") is { } flag && (bool)flag,
        FileJson.Number(json.GetValueOrDefault("fieldOfView")),
        FileJson.Number(json.GetValueOrDefault("viewHeight")),
        FileJson.Number(json.GetValueOrDefault("near")),
        json.GetValueOrDefault("far") is { } far ? FileJson.Number(far) : double.PositiveInfinity,
        FileJson.Matrix(json.GetValueOrDefault("transform")));
}

/// <summary>
/// Reads the JSON tree of a model description. A value that is there but of the wrong type throws,
/// because a description in a shape this version does not read is dropped whole.
/// </summary>
internal static class FileJson
{
    internal static string Text(object? value) => value is null ? "" : (string)value;

    internal static double Number(object? value) => value switch
    {
        null => 0,
        long whole => whole,
        double number => number,
        _ => throw new InvalidCastException($"A number was expected, not {value.GetType().Name}."),
    };

    internal static IReadOnlyList<object?> List(IReadOnlyDictionary<string, object?> json, string key) =>
        json.GetValueOrDefault(key) is { } value ? (IReadOnlyList<object?>)value : [];

    internal static IReadOnlyList<string> Texts(IReadOnlyDictionary<string, object?> json, string key) =>
        [.. List(json, key).Select(Text)];

    internal static IEnumerable<IReadOnlyDictionary<string, object?>> Maps(IReadOnlyDictionary<string, object?> json, string key) =>
        List(json, key).Select(item => (IReadOnlyDictionary<string, object?>)(item ?? throw new InvalidCastException("An object was expected, not null.")));

    internal static IReadOnlyDictionary<string, object?> Map(IReadOnlyDictionary<string, object?> json, string key) =>
        json.GetValueOrDefault(key) is { } value ? (IReadOnlyDictionary<string, object?>)value : new Dictionary<string, object?>();

    /// <summary>Three numbers, with <paramref name="fallback"/> for any that are missing. A value that is not a list reads as an empty one.</summary>
    internal static Vector3 Vector(object? value, double fallback = 0)
    {
        var list = value as IReadOnlyList<object?> ?? [];
        double At(int i) => i < list.Count ? Number(list[i]) : fallback;
        return new Vector3(At(0), At(1), At(2));
    }

    /// <summary>A matrix of sixteen numbers, or the identity for anything else.</summary>
    internal static Matrix4 Matrix(object? value)
    {
        var list = value as IReadOnlyList<object?> ?? [];
        return list.Count == 16 ? Matrix4.FromList([.. list.Select(Number)]) : Matrix4.Identity();
    }
}

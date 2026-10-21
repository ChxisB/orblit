using Orblit.Light;
using Orblit.Mesh;
using Read = Orblit.Scene.Values;

namespace Orblit.Scene;

/// <summary>
/// Something with geometry, and how it is drawn.
/// </summary>
/// <remarks>
/// The geometry can arrive four ways and they are not alternatives so much as a history. A box is
/// <see cref="Shape"/>, three numbers, right up until somebody pulls a face off it, at which point
/// <see cref="Geometry"/> is what it is and the shape's numbers are a record of what it was made
/// from. An <see cref="Outline"/> is a third: a plan somebody drew and pulled up, kept beside the
/// mesh so a wall can still be moved by dragging the corner it belongs to. And <see cref="Asset"/>
/// is none of those: a file on disk that this entity draws.
/// <para>
/// All four are kept when all four are known, because each answers a question the others cannot, and
/// throwing the earlier ones away is what makes an edit irreversible a week later.
/// </para>
/// </remarks>
public sealed class MeshComponent(
    string? asset = null,
    Shape? shape = null,
    MeshGeometry? geometry = null,
    PolyShape? outline = null,
    JsonMap? boundary = null,
    JsonList? surfaces = null,
    Tint? colour = null,
    bool castShadows = true,
    bool receiveShadows = true,
    double sway = 0,
    bool authored = false) : SceneComponent
{
    /// <summary>A mesh file, relative to the project, or null for geometry held here.</summary>
    public string? Asset { get; } = asset;

    public Shape? Shape { get; } = shape;

    public MeshGeometry? Geometry { get; } = geometry;

    public PolyShape? Outline { get; } = outline;

    /// <summary>
    /// What this is to walk into, kept as it was written. Not read into a type here, and
    /// deliberately. A boundary is a collision shape and a surface is a material, and both belong to
    /// packages that do not exist yet: the material system is a phase of its own. Holding them as the
    /// file wrote them means the document round-trips them exactly today and the editor keeps reading
    /// them with the types it already has, without this package pretending to understand either.
    /// </summary>
    public JsonMap? Boundary { get; } = boundary;

    /// <summary>The materials painted onto this shape's faces, kept as they were written.</summary>
    public JsonList? Surfaces { get; } = surfaces;

    /// <summary>The colour it is drawn in, before any material overrides it.</summary>
    public Tint Colour { get; } = colour ?? Tint.Hex(0xD9634F);

    public bool CastShadows { get; } = castShadows;

    public bool ReceiveShadows { get; } = receiveShadows;

    /// <summary>How much this moves in wind, from nothing to fully.</summary>
    public double Sway { get; } = sway;

    /// <summary>
    /// Whether the geometry belongs to this document rather than to a file. The difference between
    /// something somebody is modelling here, a box they can still pull a face off, and something
    /// exported from elsewhere that this scene only places. A tool offers to edit the first and not
    /// the second, and the two are otherwise indistinguishable: an authored shape that has not been
    /// touched yet has no geometry stored, and a referenced model given a boundary has some.
    /// <para>
    /// Stated rather than guessed from which fields are filled in, because the guess is wrong in both
    /// directions and what it costs is somebody's modelling panel disappearing when they reopen the
    /// file.
    /// </para>
    /// </summary>
    public bool Authored { get; } = authored;

    public override string Type => SceneComponents.Mesh;

    public static MeshComponent FromJson(JsonMap json) => new(
        Values.Text(json, "asset"),
        Orblit.Mesh.Shape.FromJson(Values.At(json, "shape")),
        MeshGeometry.FromJson(Values.At(json, "geometry")),
        PolyShape.FromJson(Values.At(json, "outline")),
        Values.At(json, "boundary") as JsonMap,
        Values.At(json, "surfaces") as JsonList,
        Values.ReadTint(Values.At(json, "colour")),
        Values.Flag(json, "castShadows", true),
        Values.Flag(json, "receiveShadows", true),
        Values.Number(json, "sway", 0),
        Values.Flag(json, "authored", false));

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["asset"] = Asset,
        ["shape"] = Shape?.ToJson(),
        ["geometry"] = Geometry?.ToJson(),
        ["outline"] = Outline?.ToJson(),
        ["boundary"] = Boundary,
        ["surfaces"] = Surfaces,
        ["colour"] = Values.TintToJson(Colour),
        ["castShadows"] = CastShadows,
        ["receiveShadows"] = ReceiveShadows,

        // Left out when there is none, because almost nothing sways and a key on every mesh in
        // every scene is noise in somebody's diff.
        ["sway"] = Sway > 0 ? Sway : null,

        // Likewise: most things in most scenes are placed, not modelled.
        ["authored"] = Authored ? true : null,
    });
}

/// <summary>
/// A material this entity wears, overriding whatever its geometry brought.
/// </summary>
/// <remarks>
/// Its own component rather than a key on the mesh, because a material can be worn by things that
/// are not meshes, a sprite or a tilemap, and because what it carries grows: groups and inheritance
/// live in the material file, looks live here.
/// </remarks>
public sealed class MaterialComponent(
    string? asset = null,
    IReadOnlyDictionary<string, string>? looks = null,
    IReadOnlyDictionary<string, object>? values = null) : SceneComponent
{
    /// <summary>The material, relative to the project. An <c>.omat</c>, an image, or an <c>.oshader</c> that an author wrote.</summary>
    public string? Asset { get; } = asset;

    /// <summary>
    /// What this entity wears instead under each named look, by look name. The same shape
    /// <c>KHR_materials_variants</c> uses, and for the same reason it chose it: the names live once,
    /// for the whole scene, and each object says only which material it swaps to under each. An
    /// object with nothing to say about a look keeps <see cref="Asset"/>, so a "winter" look is
    /// authored by naming the dozen things that change, not by restating the four hundred that do not.
    /// </summary>
    public IReadOnlyDictionary<string, string> Looks { get; } = looks ?? new OrderedDictionary<string, string>();

    /// <summary>
    /// What this entity gives an author-written <see cref="Asset"/>, by the name the shader declares
    /// it under. A number, a flag, a list of up to four numbers for a vector or a colour, or a
    /// project path for a picture. Whatever is left out keeps the shader's own default, so two
    /// entities can share one shader and differ only in what they state. Read only when
    /// <see cref="Asset"/> is a shader.
    /// </summary>
    public IReadOnlyDictionary<string, object> Values { get; } = values ?? new OrderedDictionary<string, object>();

    /// <summary>Every look this entity has something of its own for.</summary>
    public IEnumerable<string> LookNames => Looks.Keys;

    public override string Type => SceneComponents.Material;

    public static MaterialComponent FromJson(JsonMap json)
    {
        var looks = new OrderedDictionary<string, string>();
        foreach (var (name, path) in Read.Object(Read.At(json, "looks")))
        {
            if (path is string { Length: > 0 } text)
            {
                looks[name] = text;
            }
        }

        var values = new OrderedDictionary<string, object>();
        foreach (var (name, raw) in Read.Object(Read.At(json, "values")))
        {
            if (SurfaceValue(raw) is { } value)
            {
                values[name] = value;
            }
        }

        return new MaterialComponent(Read.Text(json, "asset"), looks, values);
    }

    /// <summary>
    /// The material to wear under <paramref name="look"/>, falling back to <see cref="Asset"/> when
    /// this entity has nothing different to wear.
    /// </summary>
    public string? Under(string? look) => look is null ? Asset : Looks.GetValueOrDefault(look) ?? Asset;

    public override JsonMap ToJson() => Read.Pruned(new OrderedDictionary<string, object?>
    {
        ["asset"] = Asset,
        ["looks"] = Looks.Count == 0 ? null : Looks,
        ["values"] = Values.Count == 0 ? null : Values,
    });

    /// <summary>
    /// A value a surface can take, or null for anything else: a number, a flag, one to four numbers,
    /// or a path for a picture.
    /// </summary>
    private static object? SurfaceValue(object? raw) => raw switch
    {
        long or double or bool => raw,
        string { Length: > 0 } => raw,
        JsonList { Count: > 0 and <= 4 } list when list.All(static n => Read.Num(n) is not null) => raw,
        _ => null,
    };
}

/// <summary>
/// A Gaussian splat capture this entity draws.
/// </summary>
/// <remarks>
/// The budget is stated here and not only asked of the device, because the two mean different things:
/// the device profile says what this machine can carry, and this says what the scene was authored to
/// look right at. The renderer takes the smaller. A capture with no <see cref="Budget"/> is drawn as
/// fully as the device allows.
/// </remarks>
public sealed class SplatsComponent(string? asset = null, long? budget = null, long? harmonics = null) : SceneComponent
{
    /// <summary>The capture, <c>.ply</c>, <c>.spz</c> or a cooked <c>.osplat</c>, as a project path.</summary>
    public string? Asset { get; } = asset;

    /// <summary>The most splats this should be drawn with, or null for as many as fit.</summary>
    public long? Budget { get; } = budget;

    /// <summary>How many bands of spherical harmonics to keep, or null for the file's.</summary>
    public long? Harmonics { get; } = harmonics;

    public override string Type => SceneComponents.Splats;

    public static SplatsComponent FromJson(JsonMap json) =>
        new(Values.Text(json, "asset"), Whole(json, "budget"), Whole(json, "harmonics"));

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["asset"] = Asset,
        ["budget"] = Budget,
        ["harmonics"] = Harmonics,
    });

    private static long? Whole(JsonMap json, string key) =>
        Values.MaybeNumber(json, key) is { } number ? Doubles.Round(number) : null;
}

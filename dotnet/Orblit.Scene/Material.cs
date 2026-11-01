using Read = Orblit.Scene.Values;

namespace Orblit.Scene;

/// <summary>
/// The sort of value a material parameter holds, and therefore how it is read out of a file and how
/// two of them merge.
/// </summary>
/// <remarks>
/// Merging is by whole value, never by part: a parent that sets <c>baseColour</c> to a warm white and
/// a child that sets it to red gives red, not some blend of the two. Anything else and a material's
/// colour would depend on how many ancestors it happened to have.
/// </remarks>
public enum MaterialKind
{
    /// <summary>One number.</summary>
    Number,

    /// <summary>True or false.</summary>
    Flag,

    /// <summary>Four numbers: red, green, blue and alpha, each nought to one, linear.</summary>
    Rgba,

    /// <summary>
    /// Three numbers, linear. Colours that have no alpha because the thing they describe cannot be
    /// partly present, a glow or a sheen.
    /// </summary>
    Rgb,

    /// <summary>Two numbers. A scale, an offset, anything that comes in u and v.</summary>
    Pair,

    /// <summary>
    /// One of a fixed set of names. Spelt out in the file rather than numbered, because a material is
    /// something people read and merge by hand.
    /// </summary>
    Choice,
}

/// <summary>
/// One parameter a material may set.
/// </summary>
/// <remarks>
/// <see cref="MaterialFields.All"/> is the only place these names exist. A renderer that grows a
/// parameter adds a row there and one line where the resolved material is turned into the renderer's
/// own. A file naming anything not in the table is kept out of the resolved values and reported, so a
/// typo is visible rather than silently doing nothing.
/// </remarks>
public sealed class MaterialField(string name, MaterialKind kind, params string[] choices)
{
    /// <summary>As it is spelt in the file.</summary>
    public string Name { get; } = name;

    public MaterialKind Kind { get; } = kind;

    /// <summary>For <see cref="MaterialKind.Choice"/>, the names allowed. Empty otherwise.</summary>
    public IReadOnlyList<string> Choices { get; } = choices;

    public override string ToString() => $"{Name} ({EnumNames.Of(Kind)})";
}

/// <summary>
/// Every parameter a material file may set, and every map it may name.
/// </summary>
/// <remarks>
/// Two tables rather than one, because a texture is an asset reference and the cook has to follow it.
/// Keeping maps in their own object means anything that needs a material's dependencies, the
/// importer or the bundler, reads one key and is done, without having to know what any parameter
/// means.
/// </remarks>
public static class MaterialFields
{
    /// <summary>The parameters, in the order an inspector would sensibly show them.</summary>
    public static readonly IReadOnlyList<MaterialField> All =
    [
        new("shading", MaterialKind.Choice, "lit", "unlit", "video", "shadowCatcher"),
        new("blend", MaterialKind.Choice, "opaque", "transparent", "fade", "masked", "add"),
        new("culling", MaterialKind.Choice, "back", "front", "none"),
        new("doubleSided", MaterialKind.Flag),

        new("baseColour", MaterialKind.Rgba),
        new("metallic", MaterialKind.Number),
        new("roughness", MaterialKind.Number),
        new("reflectance", MaterialKind.Number),
        new("clearCoat", MaterialKind.Number),
        new("clearCoatRoughness", MaterialKind.Number),
        new("anisotropy", MaterialKind.Number),
        new("sheenColour", MaterialKind.Rgb),
        new("sheenRoughness", MaterialKind.Number),

        new("emissive", MaterialKind.Rgb),
        new("emissiveIntensity", MaterialKind.Number),
        new("ambientOcclusion", MaterialKind.Number),
        new("normalScale", MaterialKind.Number),

        new("tiling", MaterialKind.Pair),
        new("offset", MaterialKind.Pair),
        new("wrap", MaterialKind.Choice, "repeat", "clamp", "mirror"),
        new("filter", MaterialKind.Choice, "smooth", "sharp"),

        new("maskThreshold", MaterialKind.Number),
        new("depthWrite", MaterialKind.Flag),
        new("depthBias", MaterialKind.Number),
        new("screenMapped", MaterialKind.Flag),
        new("litScreen", MaterialKind.Flag),

        new("blendMode", MaterialKind.Choice, "none", "linear", "masked", "maskedDepth"),
        new("blendAmount", MaterialKind.Number),
        new("blendSharpness", MaterialKind.Number),
        new("blendTiling", MaterialKind.Pair),
        new("blendOffset", MaterialKind.Pair),

        new("windBearing", MaterialKind.Number),
        new("windSpeed", MaterialKind.Number),
        new("windStrength", MaterialKind.Number),
    ];

    /// <summary>The maps a material may name, by the slot each fills.</summary>
    public static readonly IReadOnlyList<string> Maps =
    [
        "baseColour",
        "normal",
        "metallicRoughness",
        "occlusion",
        "emissive",
        "blendBaseColour",
        "blendMask",
    ];

    public static readonly IReadOnlyDictionary<string, MaterialField> ByName =
        All.ToDictionary(static field => field.Name, StringComparer.Ordinal);

    /// <summary>Whether <paramref name="name"/> is a parameter this build understands.</summary>
    public static bool Has(string name) => ByName.ContainsKey(name);
}

/// <summary>
/// A material as it sits in a file: what it sets, and what it inherits from.
/// </summary>
/// <remarks>
/// Deliberately not a resolved material. A document says only what <em>this</em> file states, so that
/// saving it back writes the same file it read: a child that inherited roughness from its parent must
/// not acquire a roughness of its own the first time somebody opens and saves it. Resolving is
/// <see cref="MaterialLibrary"/>'s job and happens once, before a scene is ever sent to a renderer.
/// </remarks>
public sealed class MaterialDocument(
    string? parent = null,
    string? group = null,
    IReadOnlyDictionary<string, object>? values = null,
    IReadOnlyDictionary<string, string>? maps = null)
{
    /// <summary>What a material file is called.</summary>
    public const string Extension = ".omat";

    /// <summary>What a shader an author wrote is called.</summary>
    public const string ShaderExtension = ".oshader";

    /// <summary>The material this one starts from, as a project path, or null for none.</summary>
    public string? Parent { get; } = parent;

    /// <summary>The group whose overrides are laid over this material, or null.</summary>
    public string? Group { get; } = group;

    /// <summary>
    /// The parameters this file states, already read as the kind each is: a <see cref="double"/>, a
    /// <see cref="bool"/>, an <see cref="IReadOnlyList{T}"/> of double or a choice name.
    /// </summary>
    public IReadOnlyDictionary<string, object> Values { get; } = values ?? new OrderedDictionary<string, object>();

    /// <summary>The maps this file names, by slot, as project paths.</summary>
    public IReadOnlyDictionary<string, string> Maps { get; } = maps ?? new OrderedDictionary<string, string>();

    /// <summary>
    /// Reads one, keeping what it can and saying what it could not.
    /// <para>
    /// A material with one unreadable parameter is still worth having: the alternative is a scene
    /// that loses a wall because somebody typed <c>roughness: "half"</c>.
    /// </para>
    /// </summary>
    public static MaterialLoad FromJson(JsonMap json)
    {
        var problems = new List<string>();
        var values = ReadValues(json, problems);
        var maps = ReadMaps(json, problems);
        return new MaterialLoad(
            new MaterialDocument(Read.Text(json, "parent"), Read.Text(json, "group"), values, maps),
            problems);
    }

    public JsonMap ToJson() => Read.Pruned(new OrderedDictionary<string, object?>
    {
        ["parent"] = Parent,
        ["group"] = Group,
        ["values"] = Values.Count == 0 ? null : WrittenValues(),
        ["maps"] = Maps.Count == 0 ? null : Read.TextMapToJson(Maps),
    });

    private OrderedDictionary<string, object?> WrittenValues()
    {
        var json = new OrderedDictionary<string, object?>();
        foreach (var (name, value) in Values)
        {
            json[name] = value is IReadOnlyList<double> numbers ? Read.NumbersToJson(numbers) : value;
        }

        return json;
    }

    private static OrderedDictionary<string, object> ReadValues(JsonMap json, List<string> problems)
    {
        var values = new OrderedDictionary<string, object>();
        foreach (var (name, raw) in Read.Object(Read.At(json, "values")))
        {
            if (!MaterialFields.ByName.TryGetValue(name, out var field))
            {
                problems.Add($"no parameter called {name}");
            }
            else if (ReadValue(field, raw) is { } value)
            {
                values[name] = value;
            }
            else
            {
                problems.Add($"{name} is not {Shape(field)}");
            }
        }

        return values;
    }

    private static OrderedDictionary<string, string> ReadMaps(JsonMap json, List<string> problems)
    {
        var maps = new OrderedDictionary<string, string>();
        foreach (var (slot, path) in Read.Object(Read.At(json, "maps")))
        {
            if (!MaterialFields.Maps.Contains(slot))
            {
                problems.Add($"no map slot called {slot}");
            }
            else if (path is string { Length: > 0 } text)
            {
                maps[slot] = text;
            }
            else
            {
                problems.Add($"the {slot} map is not a path");
            }
        }

        return maps;
    }

    // One value read as the kind its field says, or null when the file has something else there.
    private static object? ReadValue(MaterialField field, object? raw) => field.Kind switch
    {
        MaterialKind.Number => Read.Num(raw) is { } number ? number : null,
        MaterialKind.Flag => raw is bool flag ? flag : null,
        MaterialKind.Rgba => Numbers(raw, 4),
        MaterialKind.Rgb => Numbers(raw, 3),
        MaterialKind.Pair => Numbers(raw, 2),
        _ => raw is string name && field.Choices.Contains(name) ? name : null,
    };

    private static double[]? Numbers(object? raw, int count)
    {
        if (raw is not JsonList list || list.Count != count)
        {
            return null;
        }

        var read = new double[count];
        for (var i = 0; i < count; i++)
        {
            if (Read.Num(list[i]) is not { } number)
            {
                return null;
            }

            read[i] = number;
        }

        return read;
    }

    private static string Shape(MaterialField field) => field.Kind switch
    {
        MaterialKind.Number => "a number",
        MaterialKind.Flag => "true or false",
        MaterialKind.Rgba => "four numbers",
        MaterialKind.Rgb => "three numbers",
        MaterialKind.Pair => "two numbers",
        _ => $"one of {string.Join(", ", field.Choices)}",
    };
}

/// <summary>A material read back off disk, with anything that could not be read.</summary>
public sealed class MaterialLoad(MaterialDocument document, IReadOnlyList<string>? problems = null)
{
    public MaterialDocument Document { get; } = document;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}

/// <summary>
/// A material with nothing left to look up: every parameter its ancestors and its group between them
/// decided, and every map resolved to a path.
/// </summary>
/// <remarks>
/// This is what a renderer is handed. There is no parent here and no group, because both have
/// already been spent.
/// </remarks>
public sealed class ResolvedMaterial(
    IReadOnlyDictionary<string, object>? values = null,
    IReadOnlyDictionary<string, string>? maps = null,
    IReadOnlyList<string>? problems = null)
{
    public IReadOnlyDictionary<string, object> Values { get; } = values ?? new OrderedDictionary<string, object>();

    public IReadOnlyDictionary<string, string> Maps { get; } = maps ?? new OrderedDictionary<string, string>();

    /// <summary>
    /// Anything that went wrong on the way: a parent that does not exist, a chain that loops.
    /// Reported rather than thrown, because a material that lost its parent still draws, in the
    /// colours it states itself.
    /// </summary>
    public IReadOnlyList<string> Problems { get; } = problems ?? [];

    /// <summary>
    /// A parameter, or null when nothing in the chain set it. The caller's own default stands in,
    /// which is the renderer's, so a material that says nothing looks the same whether it was
    /// resolved through ten ancestors or none.
    /// </summary>
    public object? this[string name] => Values.GetValueOrDefault(name);

    public double? Number(string name) => Values.GetValueOrDefault(name) is double value ? value : null;

    public bool? Flag(string name) => Values.GetValueOrDefault(name) is bool value ? value : null;

    public IReadOnlyList<double>? Numbers(string name) =>
        Values.GetValueOrDefault(name) is IReadOnlyList<double> value ? value : null;

    public string? Choice(string name) => Values.GetValueOrDefault(name) as string;
}

/// <summary>
/// Every material a project has, and the one place a parent chain is walked.
/// </summary>
/// <remarks>
/// Resolution happens here and is cached, so a scene with four hundred objects wearing eleven
/// materials walks eleven chains rather than four hundred. The cost is paid once at load and never
/// again per frame, which is the whole reason inheritance is allowed to exist at all: a renderer that
/// had to ask "and what did its parent say" while drawing would be doing it sixty times a second for
/// something that cannot change between frames.
/// </remarks>
public sealed class MaterialLibrary(
    IReadOnlyDictionary<string, MaterialDocument>? materials = null,
    IReadOnlyDictionary<string, MaterialDocument>? groups = null)
{
    private readonly Dictionary<string, MaterialDocument> _materials = new(materials ?? new Dictionary<string, MaterialDocument>());
    private readonly Dictionary<string, MaterialDocument> _groups = new(groups ?? new Dictionary<string, MaterialDocument>());
    private readonly Dictionary<string, ResolvedMaterial> _resolved = [];

    /// <summary>The materials, by project path.</summary>
    public IReadOnlyDictionary<string, MaterialDocument> Materials => new Dictionary<string, MaterialDocument>(_materials);

    /// <summary>The groups, by name.</summary>
    public IReadOnlyDictionary<string, MaterialDocument> Groups => new Dictionary<string, MaterialDocument>(_groups);

    /// <summary>
    /// Adds or replaces one material, forgetting whatever was resolved from it.
    /// <para>
    /// The whole cache goes, not just this material's entry: anything that named it as a parent
    /// resolved through it, and finding those would cost more than resolving the handful of
    /// materials a project has again.
    /// </para>
    /// </summary>
    public void Put(string path, MaterialDocument material)
    {
        _materials[path] = material;
        _resolved.Clear();
    }

    /// <summary>Adds or replaces one group, on the same terms.</summary>
    public void PutGroup(string name, MaterialDocument group)
    {
        _groups[name] = group;
        _resolved.Clear();
    }

    /// <summary>
    /// What <paramref name="path"/> finally says, with its ancestors and its group spent.
    /// <para>
    /// Order is: the eldest ancestor first, then each descendant over the top, then the group. The
    /// group wins, and deliberately: a group exists to override a whole set of materials from
    /// outside, and one that lost to every material that had bothered to state a value could
    /// override almost nothing.
    /// </para>
    /// </summary>
    public ResolvedMaterial Resolve(string path)
    {
        if (_resolved.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var problems = new List<string>();
        var chain = Chain(path, problems);

        var values = new OrderedDictionary<string, object>();
        var maps = new OrderedDictionary<string, string>();

        // Eldest first: the chain was collected downwards, so it is walked back.
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            Lay(values, maps, chain[i]);
        }

        // The group named nearest the bottom of the chain, because that is the one this material
        // chose. An ancestor's group still applies to the ancestor's own descendants, but a child
        // that names its own is not overruled by it.
        if (chain.Select(static material => material.Group).FirstOrDefault(static group => group is not null) is { } named)
        {
            if (_groups.TryGetValue(named, out var group))
            {
                Lay(values, maps, group);
            }
            else
            {
                problems.Add($"no group called {named}");
            }
        }

        return _resolved[path] = new ResolvedMaterial(values, maps, problems);
    }

    private static void Lay(
        OrderedDictionary<string, object> values,
        OrderedDictionary<string, string> maps,
        MaterialDocument material)
    {
        foreach (var (name, value) in material.Values)
        {
            values[name] = value;
        }

        foreach (var (slot, file) in material.Maps)
        {
            maps[slot] = file;
        }
    }

    // The material at the path and each parent above it, nearest first, ending where there is no
    // parent, no such material, or a parent already seen.
    private List<MaterialDocument> Chain(string path, List<string> problems)
    {
        var chain = new List<MaterialDocument>();
        var walked = new List<string>();

        var at = path;
        while (true)
        {
            if (walked.Contains(at))
            {
                walked.Add(at);
                problems.Add($"{at} inherits from itself, through {string.Join(" -> ", walked.Distinct())}");
                return chain;
            }

            walked.Add(at);
            if (!_materials.TryGetValue(at, out var material))
            {
                problems.Add($"no material at {at}");
                return chain;
            }

            chain.Add(material);
            if (material.Parent is not { } parent)
            {
                return chain;
            }

            at = parent;
        }
    }
}

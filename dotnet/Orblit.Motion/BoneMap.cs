using System.Collections.ObjectModel;
using Orblit.Json;
using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>A file that is not a bone map, or is one from a newer Orblit.</summary>
public sealed class BoneMapFormatException(string message) : Exception(message);

/// <summary>A bone map read back off disk, with anything that could not be read.</summary>
public sealed class BoneMapLoad(BoneMap map, IReadOnlyList<string>? problems = null)
{
    public BoneMap Map { get; } = map;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}

/// <summary>
/// How the bones of one skeleton move the bones of another, kept as a file so the same pairing carries every
/// clip made for the one onto the other.
/// </summary>
/// <remarks>
/// It holds what <see cref="ClipRetarget.Of"/> is given besides the skeletons and the clip: the
/// <see cref="Bones"/> named by hand, the <see cref="Unpaired"/> bones left alone, and whether to pair by
/// <see cref="Humanoid"/> and to fix the rest pose. <see cref="FromAsset"/> and <see cref="ToAsset"/> are what
/// the two skeletons came from, such as the paths of two models. They are kept for whoever opens the file and
/// are never read here.
/// </remarks>
public sealed class BoneMap
{
    /// <summary>The extension a bone map file carries.</summary>
    public const string Extension = ".obonemap";

    public const string Marker = "orblit.bonemap";

    /// <summary>The shape of the file. Bumped with a migration whenever it changes.</summary>
    public const int FormatVersion = 1;

    private static readonly FileFormat Format = new(
        Marker,
        "bone map",
        FormatVersion,
        message => new BoneMapFormatException(message),
        []);

    // F2: six parameters, all optional and called by name.
    public BoneMap(
        string fromAsset = "",
        string toAsset = "",
        IReadOnlyDictionary<string, string>? bones = null,
        IReadOnlySet<string>? unpaired = null,
        bool humanoid = false,
        bool fixRest = false)
    {
        bones ??= new Dictionary<string, string>();
        unpaired ??= new HashSet<string>();
        if (unpaired.FirstOrDefault(bones.ContainsKey) is { } name)
        {
            throw new ArgumentException($"\"{name}\" is paired by hand, so it cannot be left unpaired.", nameof(unpaired));
        }

        FromAsset = fromAsset;
        ToAsset = toAsset;
        Bones = new ReadOnlyDictionary<string, string>(new OrderedDictionary<string, string>(bones));
        Unpaired = new ReadOnlySet<string>(new HashSet<string>(unpaired));
        Humanoid = humanoid;
        FixRest = fixRest;
    }

    public string FromAsset { get; }

    public string ToAsset { get; }

    /// <summary>Bone of the target to the bone of the source that moves it, over whatever names and body parts pair.</summary>
    public IReadOnlyDictionary<string, string> Bones { get; }

    /// <summary>Bones of the target that nothing moves, whatever names would pair.</summary>
    public IReadOnlySet<string> Unpaired { get; }

    /// <summary>Whether bones are also paired by the part of a person each is.</summary>
    public bool Humanoid { get; }

    /// <summary>Whether each bone is first turned to point the way its driver does at rest.</summary>
    public bool FixRest { get; }

    /// <summary>A bone map out of a file's text, with whatever could not be read.</summary>
    /// <remarks>
    /// Lenient about the parts, strict about the whole, like a clip: a pair that is not two names is left out
    /// with a note, and a file that is not a bone map or is one from a newer Orblit throws
    /// <see cref="BoneMapFormatException"/>.
    /// </remarks>
    public static BoneMapLoad Decode(string text)
    {
        var problems = new List<string>();
        var json = Format.Open(text, problems);
        var bones = PairsOf(json.GetValueOrDefault("bones"), problems);
        var unpaired = NamesOf(json.GetValueOrDefault("unpaired"), problems);
        foreach (var name in unpaired.Where(bones.ContainsKey).ToList())
        {
            problems.Add($"\"{name}\" is paired by hand and also left unpaired. The pair was kept.");
            unpaired.Remove(name);
        }

        var map = new BoneMap(
            Values.Text(json, "from") ?? string.Empty,
            Values.Text(json, "to") ?? string.Empty,
            bones,
            unpaired,
            Values.Flag(json, "humanoid", fallback: false),
            Values.Flag(json, "fixRest", fallback: false));
        return new BoneMapLoad(map, problems);
    }

    // F2: six parameters, all optional and called by name.
    public BoneMap CopyWith(
        string? fromAsset = null,
        string? toAsset = null,
        IReadOnlyDictionary<string, string>? bones = null,
        IReadOnlySet<string>? unpaired = null,
        bool? humanoid = null,
        bool? fixRest = null) => new(
        fromAsset ?? FromAsset,
        toAsset ?? ToAsset,
        bones ?? Bones,
        unpaired ?? Unpaired,
        humanoid ?? Humanoid,
        fixRest ?? FixRest);

    /// <summary>This map with <paramref name="bone"/> of the target moved by <paramref name="source"/>, by hand. Whatever left <paramref name="bone"/> unpaired is undone, so a bone is never both.</summary>
    public BoneMap WithPair(string bone, string source) =>
        CopyWith(bones: new OrderedDictionary<string, string>(Bones) { [bone] = source }, unpaired: WithoutName(Unpaired, bone));

    /// <summary>This map with <paramref name="bone"/> of the target moved by nothing, whatever a name or the body would pair it with.</summary>
    public BoneMap WithUnpaired(string bone) =>
        CopyWith(bones: WithoutPair(Bones, bone), unpaired: new HashSet<string>(Unpaired) { bone });

    /// <summary>This map with <paramref name="bone"/> of the target paired as names and the body say.</summary>
    public BoneMap WithAuto(string bone) => CopyWith(bones: WithoutPair(Bones, bone), unpaired: WithoutName(Unpaired, bone));

    /// <summary>
    /// The bones this map names that <paramref name="from"/> or <paramref name="to"/> lacks, sorted. A map
    /// made for another model has some, and <see cref="Apply"/> throws until they are out.
    /// </summary>
    public IReadOnlyList<string> Stale(RestSkeleton from, RestSkeleton to) =>
    [
        .. Bones.Where(pair => !Fits(pair.Key, pair.Value, from, to)).Select(static pair => pair.Key)
            .Concat(Unpaired.Where(name => to.IndexOf(name) is null))
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>This map without the names <see cref="Stale"/> answers.</summary>
    public BoneMap Fitted(RestSkeleton from, RestSkeleton to) => CopyWith(
        bones: new OrderedDictionary<string, string>(Bones.Where(pair => Fits(pair.Key, pair.Value, from, to))),
        unpaired: new HashSet<string>(Unpaired.Where(name => to.IndexOf(name) is not null)));

    /// <summary>
    /// The bone of <paramref name="from"/> that moves each bone of <paramref name="to"/> under this map, by
    /// name, and what could not be paired. Names and the body are read as <see cref="Apply"/> reads them, so
    /// this is what a clip would get.
    /// </summary>
    /// <exception cref="ArgumentException">As <see cref="Apply"/> does, for a name <see cref="Stale"/> answers.</exception>
    public (OrderedDictionary<string, string> Drivers, IReadOnlyList<string> Problems) Pairing(RestSkeleton from, RestSkeleton to)
    {
        var plan = RetargetPlan.Of(from, to, Bones, Unpaired, Humanoid, FixRest);
        var drivers = new OrderedDictionary<string, string>(plan.Drivers.Select(pair => KeyValuePair.Create(to.Names[pair.Key], from.Names[pair.Value])));
        return (drivers, plan.Problems);
    }

    /// <summary><paramref name="clip"/>, made for <paramref name="from"/>, made to move <paramref name="to"/>, paired as this map says.</summary>
    /// <exception cref="ArgumentException">As <see cref="ClipRetarget.Of"/> does, for a bone the map names that the skeleton it is named in lacks.</exception>
    public ClipRetargeted Apply(ClipDocument clip, RestSkeleton from, RestSkeleton to, string target = "") =>
        ClipRetarget.Of(clip, from, to, Bones, Unpaired, target, Humanoid, FixRest);

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = FormatVersion,
        };
        if (FromAsset.Length > 0)
        {
            json["from"] = FromAsset;
        }

        if (ToAsset.Length > 0)
        {
            json["to"] = ToAsset;
        }

        json["humanoid"] = Humanoid;
        json["fixRest"] = FixRest;
        json["bones"] = new OrderedDictionary<string, object?>(Bones.OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => KeyValuePair.Create(pair.Key, (object?)pair.Value)));
        json["unpaired"] = Unpaired.Order(StringComparer.Ordinal).Select(static name => (object?)name).ToList();
        return json;
    }

    /// <summary>The file's text, one pair to a line so two maps diff as the pairs that changed.</summary>
    public string Encode() => $"{JsonText.Encode(ToJson(), "  ")}\n";

    private static bool Fits(string bone, string source, RestSkeleton from, RestSkeleton to) =>
        to.IndexOf(bone) is not null && from.IndexOf(source) is not null;

    private static OrderedDictionary<string, string> WithoutPair(IReadOnlyDictionary<string, string> bones, string bone) =>
        new(bones.Where(pair => pair.Key != bone));

    private static HashSet<string> WithoutName(IReadOnlySet<string> names, string bone) =>
        new(names.Where(name => name != bone));

    private static OrderedDictionary<string, string> PairsOf(object? raw, List<string> problems)
    {
        var pairs = new OrderedDictionary<string, string>();
        if (raw is not JsonMap map)
        {
            return pairs;
        }

        foreach (var (key, value) in map)
        {
            if (value is string { Length: > 0 } source && key.Length > 0)
            {
                pairs[key] = source;
            }
            else
            {
                problems.Add($"The pair for \"{key}\" is not a bone name, so it was left out.");
            }
        }

        return pairs;
    }

    private static HashSet<string> NamesOf(object? raw, List<string> problems)
    {
        var names = new HashSet<string>();
        foreach (var name in raw is JsonList list ? list : [])
        {
            if (name is string { Length: > 0 } bone)
            {
                names.Add(bone);
            }
            else
            {
                problems.Add("An unpaired bone that is not a name was left out.");
            }
        }

        return names;
    }
}

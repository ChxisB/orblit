using System.Text;
using Orblit.Json;
using Orblit.Sprite;

namespace Orblit.Asset;

/// <summary>
/// Packs a folder of sprites into as few pages as it can.
/// <para>
/// An atlas is not a file somebody has. It is a file somebody wants made out of a folder they have. So the
/// asset this claims is a small document naming the sprites, <c>ui.atlas.json</c>, rather than any one of the
/// pictures, and the pictures it names are its dependencies. That is what makes it fit the cook at all: adding
/// a sprite to the folder edits the document, editing one sprite changes a dependency, and either one repacks
/// while nothing else in the project moves.
/// </para>
/// <para>
/// The sprites are listed rather than globbed. A glob would make the cook's answer depend on what happened to
/// be in a folder, which is the one thing a content-addressed cache cannot key on, and it would quietly pick
/// up the <c>.png</c> somebody left in there while exporting.
/// </para>
/// <para>
/// The outputs are <c>page0.png</c>, <c>page0.json</c>, <c>page1.png</c> and so on: pages a texture importer
/// can then cook, and a descriptor per page saying which rectangle each sprite landed in. Pages come out as PNG
/// rather than as a compressed texture because packing and encoding are separate jobs. The pages want cooking
/// per target the same as any other picture, and doing it here would mean doing it again.
/// </para>
/// </summary>
public sealed class AtlasImporter : Importer
{
    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "json" };

    private static readonly OrderedDictionary<string, MaxRectsHeuristic> HeuristicsByName = new()
    {
        ["short"] = MaxRectsHeuristic.BestShortSideFit,
        ["long"] = MaxRectsHeuristic.BestLongSideFit,
        ["area"] = MaxRectsHeuristic.BestAreaFit,
        ["corner"] = MaxRectsHeuristic.BottomLeft,
        ["contact"] = MaxRectsHeuristic.ContactPoint,
    };

    public override string Name => "atlas";

    public override IReadOnlySet<string> Extensions => Claimed;

    public override int Version => 1;

    /// <summary>
    /// Only a document that says it is an atlas. <c>.json</c> is far too common an extension to claim
    /// outright: a scene's settings, a localisation table and somebody's notes all end in it. The double
    /// extension is the claim, the same way <c>.import.json</c> is.
    /// </summary>
    public override bool CanHandle(AssetId id) =>
        DartText.ToLower(id.Name).EndsWith(".atlas.json", StringComparison.Ordinal);

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>
        {
            ["maxPageSize"] = Size(settings, "maxPageSize"),
            ["padding"] = Count(settings, "padding"),
            ["border"] = Count(settings, "border"),
            ["extrude"] = Count(settings, "extrude"),
            ["trimAlphaThreshold"] = Count(settings, "trimAlphaThreshold"),
            ["trim"] = settings.Values.GetValueOrDefault("trim"),
            ["mergeDuplicates"] = settings.Values.GetValueOrDefault("mergeDuplicates"),
            ["allowRotation"] = settings.Values.GetValueOrDefault("allowRotation"),
            ["square"] = settings.Values.GetValueOrDefault("square"),
            ["powerOfTwo"] = settings.Values.GetValueOrDefault("powerOfTwo"),
            ["heuristic"] = settings.Values.GetValueOrDefault("heuristic"),
        };

    // On the pool so that a document that cannot be read faults the task, as an async method would, rather
    // than throwing at the call.
    public override Task<IReadOnlySet<AssetId>> DependenciesOfAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings) =>
        Task.Run<IReadOnlySet<AssetId>>(() => SpritesIn(id, bytes).ToHashSet());

    public override async Task<ImportResult> ImportAsync(ImportRequest request)
    {
        var document = DocumentIn(request.Id, request.Bytes);
        var sprites = SpritesIn(request.Id, request.Bytes);
        if (sprites.Count == 0)
        {
            throw new ImportFailureException(
                request.Id, "an atlas has to name at least one sprite, and this one names none");
        }

        var settled = Settled(document, request.Settings);
        var maxPageSize = Typed<long>(settled, "maxPageSize") ?? request.Target.MaxTextureSize() ?? 2048;

        var packed = new List<AtlasSprite>();
        foreach (var sprite in sprites)
        {
            packed.Add(await ReadSpriteAsync(request, sprite).ConfigureAwait(false));
        }

        var result = AtlasPacker.PackAtlas(packed, OptionsFor(request.Id, settled, maxPageSize));

        // A sprite too large for a page at all is a failure rather than a note. The alternative is an atlas
        // that packed nine of ten sprites and a game that draws nothing where the tenth was, found at run time.
        if (result.Problems.Count > 0)
        {
            throw new ImportFailureException(
                request.Id,
                $"these sprites do not fit on a {maxPageSize}x{maxPageSize} page: "
                + string.Join("; ", result.Problems));
        }

        return new ImportResult { Outputs = OutputsOf(result), Notes = NotesOf(sprites.Count, result) };
    }

    // The document's own settings sit under the sprites. Anything in a `.import.json` or given at the call
    // wins, so that a project can cook the same atlas differently for two targets without editing it.
    //
    // Nulls are dropped before the overlay rather than after it. Resolved settings carry a key for every
    // option whether or not anybody set one, so laying them over the document whole would have every unset
    // option erase what the document said, which is the same as the document having no settings at all.
    private static Dictionary<string, object?> Settled(
        GltfMap document, IReadOnlyDictionary<string, object?> overrides)
    {
        var settled = new Dictionary<string, object?>(document);
        foreach (var (key, value) in overrides.Where(pair => pair.Value is not null))
        {
            settled[key] = value;
        }

        return settled;
    }

    private static AtlasPackOptions OptionsFor(AssetId id, Dictionary<string, object?> settled, long maxPageSize) =>
        new(
            maxPageSize: maxPageSize,
            padding: Typed<long>(settled, "padding") ?? 2,
            border: Typed<long>(settled, "border") ?? 0,
            extrude: Typed<long>(settled, "extrude") ?? 1,
            trimAlphaThreshold: Typed<long>(settled, "trimAlphaThreshold") ?? 0,
            trim: Typed<bool>(settled, "trim") ?? true,
            mergeDuplicates: Typed<bool>(settled, "mergeDuplicates") ?? true,
            allowRotation: Typed<bool>(settled, "allowRotation") ?? false,
            square: Typed<bool>(settled, "square") ?? false,
            powerOfTwo: Typed<bool>(settled, "powerOfTwo") ?? true,
            heuristic: HeuristicNamed(id, settled.GetValueOrDefault("heuristic")));

    private static async Task<AtlasSprite> ReadSpriteAsync(ImportRequest request, AssetId sprite)
    {
        var bytes = await request.Source.ReadAsync(sprite).ConfigureAwait(false);
        DecodedPng picture;
        try
        {
            picture = PngCodec.Decode(bytes);
        }
        catch (PngFormatException error)
        {
            throw new ImportFailureException(
                request.Id, $"the sprite {sprite} could not be read: {error.Message}", error.Message);
        }

        // Named by id, so two folders can each have a `play.png` and the descriptor still says which one a
        // region is.
        return new AtlasSprite(sprite.ToString(), picture.Width, picture.Height, picture.Pixels);
    }

    private static Dictionary<string, byte[]> OutputsOf(AtlasPackResult result)
    {
        var outputs = new Dictionary<string, byte[]>();
        for (var page = 0; page < result.Pages.Count; page++)
        {
            var it = result.Pages[page];
            outputs[$"page{page}.png"] = PngCodec.Encode(it.Width, it.Height, it.Pixels);
            outputs[$"page{page}.json"] =
                Encoding.UTF8.GetBytes(AtlasPacker.WriteAtlas(it.ToAtlas($"page{page}.png")));
        }

        return outputs;
    }

    private static List<string> NotesOf(int spriteCount, AtlasPackResult result)
    {
        var pages = result.Pages;
        var notes = new List<string>
        {
            $"packed {spriteCount} sprites onto {pages.Count} page{(pages.Count == 1 ? "" : "s")} "
            + $"with {HeuristicLabel(result.Heuristic)}",
        };
        notes.AddRange(pages.Select((page, index) =>
            $"page{index} is {page.Width}x{page.Height}, "
            + $"{(long)Math.Round(page.FillRatio * 100, MidpointRounding.AwayFromZero)}% full"));
        return notes;
    }

    /// <summary>The document, or a failure saying what is wrong with it.</summary>
    private static GltfMap DocumentIn(AssetId id, byte[] bytes)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(DartText.Decode(bytes));
        }
        catch (FormatException error)
        {
            throw new ImportFailureException(id, $"this is not JSON: {error.Message}", error.Message);
        }

        return parsed as GltfMap ?? throw new ImportFailureException(id, "an atlas document has to be a JSON object");
    }

    // The order is kept because the packer is deterministic for a given list, and a list that came back in a
    // different order would repack a page that nothing about actually changed.
    private static List<AssetId> SpritesIn(AssetId id, byte[] bytes)
    {
        var named = DocumentIn(id, bytes).GetValueOrDefault("sprites")
            ?? throw new ImportFailureException(id, "an atlas document needs a \"sprites\" list");
        if (named is not List<object?> entries)
        {
            throw new ImportFailureException(
                id, $"\"sprites\" has to be a list of asset names, and this is a {DartType(named)}");
        }

        return [.. entries.Select(entry => SpriteNamed(id, entry))];
    }

    // Relative to the project, like a scene's references and unlike a glTF's, with `./` as the way to say
    // "beside me". It is the same rule SceneImporter follows, because both are documents a person writes.
    private static AssetId SpriteNamed(AssetId id, object? entry)
    {
        if (entry is not string name)
        {
            throw new ImportFailureException(
                id, $"every entry in \"sprites\" has to be an asset name, and one is a {DartType(entry)}");
        }

        var sprite = name.StartsWith("./", StringComparison.Ordinal) || name.StartsWith("../", StringComparison.Ordinal)
            ? id.Resolve(name)
            : AssetId.TryParse(name);
        return sprite ?? throw new ImportFailureException(id, $"\"{name}\" is not an asset name");
    }

    private static MaxRectsHeuristic? HeuristicNamed(AssetId id, object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value is string name && HeuristicsByName.TryGetValue(name, out var found)
            ? found
            : throw new ImportFailureException(
                id,
                $"\"{DartText.Print(value)}\" is not a packing heuristic. Use one of "
                + $"{string.Join(", ", HeuristicsByName.Keys)}, or leave it out to have the best one chosen");
    }

    private static string HeuristicLabel(MaxRectsHeuristic heuristic)
    {
        var name = heuristic.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static long? Size(ImportSettings settings, string key) =>
        settings.Values.GetValueOrDefault(key) switch
        {
            null => null,
            long size and > 0 => size,
            var other => throw new ArgumentException(
                $"Invalid argument ({key}): has to be a size in texels: {DartText.Print(other)}", key),
        };

    private static long? Count(ImportSettings settings, string key) =>
        settings.Values.GetValueOrDefault(key) switch
        {
            null => null,
            long count and >= 0 => count,
            var other => throw new ArgumentException(
                $"Invalid argument ({key}): cannot be negative: {DartText.Print(other)}", key),
        };

    // Dart's `as int?`: absent is null, and a value of any other type is an error.
    private static T? Typed<T>(Dictionary<string, object?> settled, string key)
        where T : struct =>
        settled.GetValueOrDefault(key) switch
        {
            null => null,
            T value => value,
            var other => throw new InvalidCastException(
                $"type '{DartType(other)}' is not a subtype of type '{DartType(default(T))}?' in type cast"),
        };

    // What Dart's runtimeType prints, for the messages that name the wrong type.
    private static string DartType(object? value) => value switch
    {
        null => "Null",
        bool => "bool",
        long => "int",
        double => "double",
        string => "String",
        List<object?> => "_GrowableList<dynamic>",
        _ => "_Map<String, dynamic>",
    };
}

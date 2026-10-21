using System.Buffers.Binary;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// Cooks a glTF model. Without settings it passes the file through under its own extension: the loader
/// reads glTF directly and well, and re-encoding it here would be work that changes nothing and a chance to
/// lose something the loader understands. What the cook adds is the dependency list and a stable key, which
/// is what makes a scene's rebuild correct.
/// <para>
/// <c>"atlas": true</c> in a model's settings asks for more. Its textures are packed onto shared pages, its
/// texture coordinates moved onto them, and the materials and primitives that come to be identical joined,
/// which turns a kitbashed set of a hundred small draws into a handful. That is a build machine's work and
/// it needs <c>orblit_texture_cook</c>, so it is a setting rather than the default: a cook with the setting
/// off still runs anywhere.
/// </para>
/// <para>
/// It is a setting on this importer and not an importer of its own because <see cref="Importer.CanHandle"/>
/// cannot see settings. The decision is made from the file name, and two models with the same extension
/// have to be able to disagree.
/// </para>
/// </summary>
public sealed class GltfImporter(ModelAtlasCook? atlas = null) : Importer
{
    private const uint GlbMagic = 0x46546C67; // 'glTF', little-endian.
    private const uint JsonChunk = 0x4E4F534A; // 'JSON'.
    private const int ChunkStart = 20;

    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "gltf", "glb" };

    /// <summary>The packer, held here so a test can point it at its own tool.</summary>
    public ModelAtlasCook Atlas { get; } = atlas ?? new ModelAtlasCook();

    public override string Name => "gltf";

    /// <summary>
    /// 3 since models can carry a static collision sidecar. Pass-through cooks produce the same bytes they
    /// always did, but the key has to change anyway: the settings a cached entry was made under did not
    /// include <c>collision</c>, and an entry that predates a setting cannot be trusted to have ignored it.
    /// </summary>
    public override int Version => 3;

    public override IReadOnlySet<string> Extensions => Claimed;

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings)
    {
        var values = settings.Values;
        var collision = values.GetValueOrDefault("collision") is true;
        if (values.GetValueOrDefault("atlas") is not true)
        {
            return new Dictionary<string, object?> { ["atlas"] = false, ["collision"] = collision };
        }

        return new Dictionary<string, object?>
        {
            ["atlas"] = true,
            ["collision"] = collision,
            // 2048 rather than the target's largest: a page is only worth making bigger if the cells filling
            // it are big, and a 16384 page of 256-texel props is one texture upload nothing benefits from.
            ["maxPageSize"] = WholeNumber(values, "maxPageSize", 2048, new Bounds(64, 16384)),
            ["padding"] = WholeNumber(values, "padding", 4, new Bounds(0, 64)),
            ["extrude"] = WholeNumber(values, "extrude", 2, new Bounds(0, 64)),
            ["maxCellSize"] = WholeNumber(values, "maxCellSize", 0, new Bounds(0, 16384)),
            ["minMaterials"] = WholeNumber(values, "minMaterials", 2, new Bounds(1, 4096)),
            ["mergePrimitives"] = values.GetValueOrDefault("mergePrimitives") is not false,
            ["maxMergedVertices"] = WholeNumber(values, "maxMergedVertices", 65536, new Bounds(3, 1 << 24)),
        };
    }

    // On the pool so that a model that cannot be read faults the task, as an async method would, rather than
    // throwing at the call.
    public override Task<IReadOnlySet<AssetId>> DependenciesOfAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings) =>
        Task.Run(() => UrisIn(id, id.Extension == "glb" ? JsonChunkOf(id, bytes) : bytes));

    public override async Task<ImportResult> ImportAsync(ImportRequest request)
    {
        var model = request.Settings.GetValueOrDefault("atlas") is true
            ? await Atlas.RunAsync(request, request.Settings).ConfigureAwait(false)
            : new ImportResult { Outputs = new Dictionary<string, byte[]> { [request.Id.Extension] = request.Bytes } };
        if (request.Settings.GetValueOrDefault("collision") is not true)
        {
            return model;
        }

        var geometry = await CollisionMesh.FromGltfAsync(request.Id, request.Bytes, request.Source).ConfigureAwait(false);
        return new ImportResult
        {
            Outputs = new Dictionary<string, byte[]>(model.Outputs) { ["collision.json"] = geometry.Encode() },
            Notes = model.Notes,
        };
    }

    /// <summary>
    /// Every file a glTF document refers to, as ids relative to the document. Exposed because a scene importer
    /// needs the same answer about a model it includes, and because a wrong answer here is the failure that is
    /// hardest to recognise later: the cook simply stops noticing an edit.
    /// <para>
    /// Only <c>buffers</c> and <c>images</c> can carry a URI in glTF 2.0. Both may instead carry a data URI,
    /// which is bytes already in the file and therefore not a dependency, or a <c>bufferView</c>, the same. A
    /// URI is percent-encoded, so it is decoded before it becomes an id.
    /// </para>
    /// </summary>
    public static IReadOnlySet<AssetId> UrisIn(AssetId document, ReadOnlySpan<byte> json)
    {
        var parsed = Parse(document, json);
        var found = new HashSet<AssetId>();
        foreach (var section in new[] { "buffers", "images" })
        {
            if (GltfJson.Get(parsed, section) is not List<object?> entries)
            {
                continue;
            }

            var uris = entries.OfType<GltfMap>()
                .Select(entry => GltfJson.Get(entry, "uri"))
                .OfType<string>()
                .Where(uri => uri.Length > 0 && !uri.StartsWith("data:", StringComparison.Ordinal));
            foreach (var uri in uris)
            {
                if (Resolved(document, uri) is { } id)
                {
                    found.Add(id);
                }
            }
        }

        return found;
    }

    private static GltfMap Parse(AssetId document, ReadOnlySpan<byte> json)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(DartText.Decode(json));
        }
        catch (FormatException error)
        {
            throw new ImportFailureException(document, "is not readable as glTF JSON", error.Message);
        }

        return parsed as GltfMap
            ?? throw new ImportFailureException(document, "is not a glTF document: its JSON is not an object");
    }

    // A URI with a scheme or a Windows drive is not ours to resolve, and a relative one is percent-encoded.
    // Null is for the ones to leave alone.
    private static AssetId? Resolved(AssetId document, string uri) =>
        uri.Contains(':', StringComparison.Ordinal)
            ? null
            : document.Resolve(DartText.PercentDecoded(uri))
                ?? throw new ImportFailureException(
                    document,
                    $"refers to \"{uri}\", which is not a name inside the project. A glTF that reaches outside the "
                    + "project cannot be cooked, because the file it wants will not be there on another machine.");

    // A GLB is a 12-byte header (magic, version, length) then chunks of length, type, payload. The first chunk
    // is the JSON. A malformed one is worth an error rather than an empty dependency list, because an empty
    // list is indistinguishable from a model that depends on nothing, and that would cook silently and wrongly.
    private static ReadOnlySpan<byte> JsonChunkOf(AssetId id, byte[] bytes)
    {
        if (bytes.Length < ChunkStart)
        {
            throw new ImportFailureException(id, $"is too short to be a GLB ({bytes.Length} bytes)");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes) != GlbMagic)
        {
            throw new ImportFailureException(
                id,
                "is named .glb but does not start with the glTF magic. An exporter that wrote a .gltf under a "
                + ".glb name is the usual cause.");
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)) != JsonChunk)
        {
            throw new ImportFailureException(id, "'s first chunk is not JSON, and a GLB's has to be");
        }

        return ChunkStart + (long)length > bytes.Length
            ? throw new ImportFailureException(
                id, $"says its JSON chunk is {length} bytes, which runs past the end of the file. It is truncated.")
            : bytes.AsSpan(ChunkStart, (int)length);
    }

    private static long WholeNumber(IReadOnlyDictionary<string, object?> values, string key, long fallback, Bounds bounds) =>
        values.GetValueOrDefault(key) switch
        {
            null => fallback,
            long number when number >= bounds.Min && number <= bounds.Max => number,
            var other => throw new ArgumentException(
                $"Invalid argument ({key}): must be a whole number between {bounds.Min} and {bounds.Max}: {DartText.Print(other)}",
                key),
        };

    private readonly record struct Bounds(long Min, long Max);
}

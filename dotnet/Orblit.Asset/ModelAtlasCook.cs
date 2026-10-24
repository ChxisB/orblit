using System.Buffers.Binary;
using System.Text.RegularExpressions;
using Orblit.Json;
using Orblit.Sprite;

namespace Orblit.Asset;

/// <summary>
/// Cooks a model into one self-contained GLB with its textures packed onto shared pages.
/// <para>
/// Run from <see cref="GltfImporter"/> when a model's settings ask for it, and separate from it because it is a
/// different kind of work. The pass-through importer runs anywhere and touches nothing, while this reads every
/// texture, decodes it, packs it, rewrites the document and cooks the result. That is a build machine's job,
/// done once, so that the device does none of it.
/// </para>
/// <para>
/// What comes out is a GLB that names its pages as siblings, in the same shape every other cooked texture has:
/// <c>atlas0.basecolour.ktx2</c> with <c>atlas0.basecolour.bc.ktx2</c> and the rest beside it, and the loader
/// taking the first one its device can sample.
/// </para>
/// </summary>
public sealed partial class ModelAtlasCook(NativeTool? tool = null)
{
    private const string Basisu = "KHR_texture_basisu";

    public NativeTool Tool { get; } = tool ?? new NativeTool("orblit_texture_cook", environmentVariable: "ORBLIT_TEXTURE_COOK");

    public async Task<ImportResult> RunAsync(ImportRequest request, IReadOnlyDictionary<string, object?> settings)
    {
        var notes = new List<string>();
        var document = await GltfDocument.ReadAsync(request.Id, request.Bytes, request.Source).ConfigureAwait(false);

        var budget = request.Target.MaxTextureSize() ?? 8192;
        var asked = Whole(settings, "maxPageSize");
        var maxPageSize = Math.Clamp(asked, 64, budget);
        if (maxPageSize != asked)
        {
            notes.Add($"pages held to {maxPageSize}px, which is what this target samples");
        }

        return await NativeWork.WithTemporaryDirectoryAsync(
            "model_atlas",
            work => PackAsync(
                new Job { Request = request, Document = document, Work = work, Targets = TargetsOf(request), Budget = budget },
                OptionsOf(settings, (int)maxPageSize),
                notes)).ConfigureAwait(false);
    }

    private static int Whole(IReadOnlyDictionary<string, object?> settings, string key) =>
        GltfJson.AsInt(settings.GetValueOrDefault(key)) ?? throw new InvalidCastException($"{key} is not a whole number.");

    private static ModelAtlasOptions OptionsOf(IReadOnlyDictionary<string, object?> settings, int maxPageSize) => new(
        MaxPageSize: maxPageSize,
        Padding: Whole(settings, "padding"),
        Extrude: Whole(settings, "extrude"),
        MaxCellSize: Whole(settings, "maxCellSize"),
        MinMaterials: Whole(settings, "minMaterials"),
        MergePrimitives: settings.GetValueOrDefault("mergePrimitives") as bool? ?? throw new InvalidCastException("mergePrimitives is not a bool."),
        MaxMergedVertices: Whole(settings, "maxMergedVertices"));

    // Always basis, whatever the target asked for. A model names its textures by URI and gltfio fetches the
    // name it is given, so atlas0.basecolour.ktx2 has to be a file that exists. The family siblings beside it
    // are for the loader to prefer once the model path picks between them, as the material path already does.
    private static List<string> TargetsOf(ImportRequest request)
    {
        var families = request.Target.TextureFamilies();
        return
        [
            .. new[] { TextureFamily.Basis }
                .Concat((families.Count == 0 ? TextureFamily.All : families).Where(family => family != TextureFamily.Raw))
                .Distinct(),
        ];
    }

    private async Task<ImportResult> PackAsync(Job job, ModelAtlasOptions options, List<string> notes)
    {
        var result = await ModelAtlas.AtlasModelAsync(job.Document, (_, definition) => DecodeAsync(job, definition), options)
            .ConfigureAwait(false);
        notes.AddRange(result.Notes);

        if (!result.Changed)
        {
            // Nothing was packed, so the document was never edited and the bytes that came in are still the
            // right answer. Re-emitting a GLB built from them would be a different file for no gain, and a
            // different file is a cache miss for everything downstream.
            notes.Add("left as it was");
            return new ImportResult
            {
                Outputs = new Dictionary<string, byte[]> { [job.Request.Id.Extension] = job.Request.Bytes },
                Notes = notes,
            };
        }

        // The pages, cooked with the flags their role calls for. A normal map encoded as colour and a colour
        // map encoded as a normal map are both wrong in ways that read as a lighting bug, which is why the
        // role is carried this far rather than guessed at from the file name.
        foreach (var page in result.Pages)
        {
            var png = PngCodec.Encode(page.Image.Width, page.Image.Height, page.Image.Pixels);
            await CookAsync(job, $"{page.Stem}.png", png, page.Role).ConfigureAwait(false);
        }

        // Before cooking anything else, throw away what the atlas swallowed. A packed material's old texture
        // is still in the document, pointing at its old image. Nothing samples either, and cooking that image
        // would be the most expensive part of this run spent on a file no one reads.
        DropUnused(job.Document);

        // Then everything the packed materials did not take. A model half cooked is a model that draws its
        // other half untextured, and the source PNG the URI still names is not beside the cooked file.
        await CookTheRestAsync(job, notes).ConfigureAwait(false);

        job.Outputs["glb"] = job.Document.ToGlb();
        return new ImportResult { Outputs = job.Outputs, Notes = notes };
    }

    // Keyed on the image rather than on its index, because dropping the images the atlas swallowed renumbers
    // every one after them.
    private static async Task<byte[]?> BytesOfAsync(Job job, GltfMap image)
    {
        if (job.Bytes.TryGetValue(image, out var known))
        {
            return known;
        }

        return job.Bytes[image] = await ImageBytesAsync(job, image).ConfigureAwait(false);
    }

    /// <summary>One image's bytes, wherever glTF let it be put.</summary>
    private static async Task<byte[]?> ImageBytesAsync(Job job, GltfMap image)
    {
        if (GltfJson.WholeAt(image, "bufferView") is { } view)
        {
            return job.Document.ViewBytes(view).ToArray();
        }

        if (GltfJson.Get(image, "uri") is not string { Length: > 0 } uri)
        {
            return null;
        }

        if (uri.StartsWith("data:", StringComparison.Ordinal))
        {
            return DataUri(uri);
        }

        // Not ours to fetch.
        if (uri.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        if (job.Request.Id.Resolve(TextRules.PercentDecoded(uri)) is not { } id)
        {
            return null;
        }

        try
        {
            return await job.Request.Source.ReadAsync(id).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A texture that cannot be read is a texture to leave untextured, with a note saying so.
            return null;
        }
    }

    private static byte[]? DataUri(string uri)
    {
        var comma = uri.IndexOf(',', StringComparison.Ordinal);
        return comma < 0 || !uri[..comma].EndsWith(";base64", StringComparison.Ordinal)
            ? null
            : TextRules.DecodeBase64(uri[(comma + 1)..]);
    }

    // PNG is decoded here because the codec is already in the repository and a cook that needs no native tool
    // is a cook that runs in a test. Everything else goes to orblit_texture_cook, which already decodes JPEG
    // and KTX2 on its way to encoding them: one flag on a tool that exists beats a JPEG decoder written here.
    private async Task<Rgba?> DecodeAsync(Job job, GltfMap definition)
    {
        if (await BytesOfAsync(job, definition).ConfigureAwait(false) is not { } source)
        {
            return null;
        }

        return LooksLikePng(source) ? DecodePng(source) : await DecodeWithToolAsync(job, source, definition).ConfigureAwait(false);
    }

    private static Rgba? DecodePng(byte[] source)
    {
        try
        {
            var png = PngCodec.Decode(source);
            return new Rgba((int)png.Width, (int)png.Height, png.Pixels);
        }
        catch (PngFormatException)
        {
            return null;
        }
    }

    private async Task<Rgba?> DecodeWithToolAsync(Job job, byte[] source, GltfMap definition)
    {
        if (Tool.Locate() is null)
        {
            return null;
        }

        var input = NativeWork.Inside(job.Work, $"decode.{ExtensionOf(definition)}");
        await File.WriteAllBytesAsync(input, source).ConfigureAwait(false);
        var output = NativeWork.Inside(job.Work, "decode.raw");
        await Tool.RunAsync([input, NativeWork.Inside(job.Work, ""), "--decode", output, "--quiet"], job.Request.Id)
            .ConfigureAwait(false);
        if (!File.Exists(output))
        {
            return null;
        }

        var raw = await File.ReadAllBytesAsync(output).ConfigureAwait(false);
        File.Delete(output);
        return FromRaw(raw);
    }

    // Eight bytes of width and height, little-endian, then that many RGBA8 pixels.
    private static Rgba? FromRaw(byte[] raw)
    {
        if (raw.Length < 8)
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32LittleEndian(raw);
        var height = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4));
        var fits = width <= int.MaxValue && height <= int.MaxValue && raw.Length - 8 == (long)width * height * 4;
        return fits ? new Rgba((int)width, (int)height, raw[8..]) : null;
    }

    /// <summary>Cooks the images the pack did not take, and points them at the result.</summary>
    private async Task CookTheRestAsync(Job job, List<string> notes)
    {
        var roles = TextureRoles.RolesOf(job.Document).Roles;
        var images = job.Document.List("images");
        var textures = job.Document.List("textures");
        var cooked = AlreadyCooked(textures);
        var names = new HashSet<string>();
        for (var t = 0; t < textures.Count; t++)
        {
            if (GltfJson.WholeAt(textures[t], "source") is not { } source
                || source < 0 || source >= images.Count || cooked.Contains(source))
            {
                continue;
            }

            if (await BytesOfAsync(job, images[source]).ConfigureAwait(false) is not { } bytes)
            {
                notes.Add($"image {source} could not be read, so the material using it will draw untextured");
                continue;
            }

            var stem = StemFor(images[source], source);
            while (!names.Add(stem))
            {
                stem += "_";
            }

            var role = roles.TryGetValue(t, out var found) ? found : TextureRole.BaseColour;
            await CookAsync(job, $"{stem}.{ExtensionOf(images[source])}", bytes, role).ConfigureAwait(false);

            Repoint(images[source], textures[t], source, stem);
            cooked.Add(source);
        }
    }

    // Which images the atlas added. They are cooked already.
    private static HashSet<int> AlreadyCooked(List<GltfMap> textures)
    {
        var cooked = new HashSet<int>();
        foreach (var texture in textures)
        {
            if (GltfJson.Get(texture, "extensions") is GltfMap extensions
                && GltfJson.Get(extensions, Basisu) is GltfMap basis
                && GltfJson.WholeAt(basis, "source") is { } source)
            {
                cooked.Add(source);
            }
        }

        return cooked;
    }

    private static void Repoint(GltfMap image, GltfMap texture, int source, string stem)
    {
        image.Remove("bufferView");
        image["uri"] = $"{stem}.ktx2";
        image["mimeType"] = "image/ktx2";

        texture.Remove("source");
        var existing = (GltfMap?)GltfJson.Get(texture, "extensions");
        var extensions = existing is null ? new GltfMap() : new GltfMap(existing);
        extensions[Basisu] = new GltfMap { ["source"] = source };
        texture["extensions"] = extensions;
    }

    /// <summary>Runs the texture cook over one image and files what it wrote.</summary>
    private async Task CookAsync(Job job, string name, byte[] bytes, TextureRole role)
    {
        var input = NativeWork.Inside(job.Work, name);
        await File.WriteAllBytesAsync(input, bytes).ConfigureAwait(false);
        var output = NativeWork.Inside(job.Work, "cooked");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);

        var arguments = new List<string>
        {
            input,
            NativeWork.Beside(output, ""),
            "--targets",
            string.Join(',', job.Targets),
            role.Srgb() ? "--srgb" : "--linear",
        };
        if (role == TextureRole.Normal)
        {
            arguments.Add("--normal");
        }

        if (role == TextureRole.Occlusion)
        {
            arguments.Add("--single-channel");
        }

        arguments.AddRange(["--max-size", $"{job.Budget}", "--uastc", "2", "--zstd", "19", "--quiet"]);
        await Tool.RunAsync(arguments, job.Request.Id).ConfigureAwait(false);

        foreach (var file in Directory.EnumerateFiles(output))
        {
            job.Outputs[Path.GetFileName(file)] = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
        }

        File.Delete(input);
    }

    // Removes the textures and images nothing points at any more, and renumbers what is left.
    //
    // Worth doing rather than leaving them. gltfio asks a model for the files it names and the loader reads
    // every one of them before drawing anything, so an image left behind after its material moved onto a page
    // is a file read, a decode and a texture upload for something never sampled. Run before the rest are
    // cooked, it saves a whole texture cook as well.
    //
    // Textures go first because they are what keeps the images alive: a packed material stopped naming its
    // old texture, but the old texture still names its old image, so dropping images alone would drop nothing.
    private static void DropUnused(GltfDocument document)
    {
        // What a material names, found by walking the material rather than by listing the slots this file
        // knows. clearcoatTexture and the rest of the KHR_materials_* maps are texture references too, and a
        // material carrying one was left unpacked precisely because they are not understood. Dropping a
        // texture one of them names would break it.
        var naming = new List<GltfMap>();
        foreach (var material in document.List("materials"))
        {
            Walk(material, naming);
        }

        Keep(document, "textures", naming, "index");

        // Then the images, which only a surviving texture can keep alive.
        var sourcing = new List<GltfMap>();
        foreach (var texture in document.List("textures"))
        {
            sourcing.Add(texture);
            if (GltfJson.Get(texture, "extensions") is GltfMap extensions)
            {
                sourcing.AddRange(extensions.Values.OfType<GltfMap>());
            }
        }

        Keep(document, "images", sourcing, "source");
    }

    private static void Walk(object? value, List<GltfMap> naming)
    {
        if (value is List<object?> list)
        {
            foreach (var one in list)
            {
                Walk(one, naming);
            }

            return;
        }

        if (value is not GltfMap map)
        {
            return;
        }

        if (GltfJson.WholeAt(map, "index") is not null)
        {
            naming.Add(map);
        }

        foreach (var entry in map.Values)
        {
            Walk(entry, naming);
        }
    }

    // Keeps only the entries of document.Json[key] that a holder's field names, and points every holder's
    // field at where its entry moved to.
    private static void Keep(GltfDocument document, string key, List<GltfMap> holders, string field)
    {
        var list = document.List(key);
        var used = holders.Select(holder => GltfJson.WholeAt(holder, field)).OfType<int>().ToHashSet();
        if (list.Count == 0 || used.Count == list.Count)
        {
            return;
        }

        var kept = new List<object?>();
        var to = new Dictionary<int, int>();
        for (var i = 0; i < list.Count; i++)
        {
            if (used.Contains(i))
            {
                to[i] = kept.Count;
                kept.Add(list[i]);
            }
        }

        foreach (var holder in holders)
        {
            if (GltfJson.WholeAt(holder, field) is { } at && to.TryGetValue(at, out var moved))
            {
                holder[field] = moved;
            }
        }

        document.Json[key] = kept;
    }

    private static string StemFor(GltfMap image, int index)
    {
        if (GltfJson.Get(image, "uri") is string { Length: > 0 } uri && !uri.StartsWith("data:", StringComparison.Ordinal))
        {
            var name = TextRules.PercentDecoded(uri);
            name = name[(name.LastIndexOf('/') + 1)..];
            var dot = name.LastIndexOf('.');
            var stem = NotFileSafe().Replace(dot > 0 ? name[..dot] : name, "_");
            if (stem.Length > 0)
            {
                return stem;
            }
        }

        if (GltfJson.Get(image, "name") is string { Length: > 0 } named)
        {
            var cleaned = NotFileSafe().Replace(named, "_");
            if (cleaned.Length > 0)
            {
                return cleaned;
            }
        }

        return $"image{index}";
    }

    private static string ExtensionOf(GltfMap image)
    {
        switch (GltfJson.Get(image, "mimeType"))
        {
            case "image/png":
                return "png";
            case "image/jpeg":
                return "jpg";
            case "image/ktx2":
                return "ktx2";
        }

        if (GltfJson.Get(image, "uri") is string uri)
        {
            var dot = uri.LastIndexOf('.');
            var extension = dot > 0 ? PlainText.ToLower(uri[(dot + 1)..]) : "";
            if (dot < uri.Length - 1 && ShortExtension().IsMatch(extension))
            {
                return extension;
            }
        }

        return "png";
    }

    private static bool LooksLikePng(byte[] bytes) =>
        bytes.Length > 8 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71;

    [GeneratedRegex("[^A-Za-z0-9_.-]", RegexOptions.CultureInvariant)]
    private static partial Regex NotFileSafe();

    [GeneratedRegex(@"^[a-z0-9]{2,5}\z", RegexOptions.CultureInvariant)]
    private static partial Regex ShortExtension();

    // What one run holds together, so that a step takes the run rather than seven things from it.
    private sealed class Job
    {
        public required ImportRequest Request { get; init; }

        public required GltfDocument Document { get; init; }

        public required DirectoryInfo Work { get; init; }

        public required List<string> Targets { get; init; }

        public required long Budget { get; init; }

        public Dictionary<GltfMap, byte[]?> Bytes { get; } = new(ReferenceEqualityComparer.Instance);

        public Dictionary<string, byte[]> Outputs { get; } = [];
    }
}

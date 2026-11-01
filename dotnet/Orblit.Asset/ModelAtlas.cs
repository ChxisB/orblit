namespace Orblit.Asset;

/// <summary>How a model's textures are packed.</summary>
/// <param name="MaxPageSize">
/// The largest a page may be. Comes from the target's texture budget, not from a constant, because a page
/// the device has to halve on load was packed at the wrong size.
/// </param>
/// <param name="Padding">Texels left around each cell, which the extrusion is written into.</param>
/// <param name="Extrude">How far a cell's edge is copied outwards. Clamped to the padding.</param>
/// <param name="MaxCellSize">
/// A cap on a single material's cell, or nought for none. Worth setting when a model mixes a 4096 hero
/// texture with fifty 256 props: without it the hero decides the page size and the props get a few texels each.
/// </param>
/// <param name="MinMaterials">
/// How many materials have to be packable before packing is worth it. One material in an atlas saves nothing
/// and costs a rewrite, so the default declines to do it.
/// </param>
/// <param name="MergePrimitives">
/// Whether primitives that end up sharing a material are merged. This is where the frame time comes from:
/// sharing a texture saves binds, sharing a draw saves the draw, and a kitbashed model is usually a hundred
/// small primitives that become one.
/// </param>
/// <param name="MaxMergedVertices">
/// The vertex count a merged primitive will not be pushed past. 65,536 on purpose: one more vertex forces
/// 32-bit indices, which doubles the index bandwidth of every draw for the rest of the model's life.
/// </param>
// F2: seven options, all named and all optional.
internal sealed record ModelAtlasOptions(
    int MaxPageSize = 2048,
    int Padding = 4,
    int Extrude = 2,
    int MaxCellSize = 0,
    int MinMaterials = 2,
    bool MergePrimitives = true,
    int MaxMergedVertices = 65536);

/// <summary>One page of one role, ready to cook.</summary>
internal sealed record AtlasPage(int Index, TextureRole Role, Rgba Image)
{
    /// <summary><c>atlas0.basecolour</c>, to which the cook adds <c>.ktx2</c> and its siblings.</summary>
    public string Stem => $"atlas{Index}.{Role.Suffix()}";
}

/// <summary>What packing a model did.</summary>
internal sealed record ModelAtlasResult(IReadOnlyList<AtlasPage> Pages, IReadOnlyList<string> Notes)
{
    public bool Changed => Pages.Count > 0;
}

/// <summary>
/// Decodes one of a document's images to RGBA8, or returns null when it cannot, a format this build has no
/// decoder for, say.
/// </summary>
internal delegate Task<Rgba?> ImageDecoder(int image, GltfMap definition);

/// <summary>
/// Packs a model's material textures onto shared pages, remaps its texture coordinates onto them, merges the
/// materials that become identical and then merges the primitives that come to share one.
/// </summary>
internal static class ModelAtlas
{
    /// <summary>
    /// <paramref name="document"/> is edited in place and stays a glTF throughout: every key this does not
    /// name travels through untouched, so extensions, <c>extras</c>, skins, animation and variants survive a
    /// rewrite that has never heard of them. Nothing here is all-or-nothing. A material that cannot be
    /// packed, because it tiles, carries a texture transform or has textures that disagree about what they
    /// are, is left exactly as it was, said so in <see cref="ModelAtlasResult.Notes"/>, and the rest of the
    /// model is still packed around it.
    /// </summary>
    public static Task<ModelAtlasResult> AtlasModelAsync(
        GltfDocument document, ImageDecoder decode, ModelAtlasOptions? options = null) =>
        new AtlasRun(document, decode, options ?? new ModelAtlasOptions()).RunAsync();
}

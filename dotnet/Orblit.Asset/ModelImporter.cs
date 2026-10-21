namespace Orblit.Asset;

/// <summary>
/// Turns an FBX or an OBJ into a GLB. FBX import is ufbx's and OBJ import is the same program's. Both come
/// out as glTF, so that everything downstream (the loader, the atlas packer, the exporter) deals with one
/// format. That was the decision behind dropping FBX export while keeping FBX import: the engine reads what
/// artists have, and writes what the ecosystem reads.
/// </summary>
/// <remarks>
/// The GLB this writes carries its textures inside it when the source did, and points at files beside it
/// when the source did. An FBX names its textures by path in its own header, and reading that here would
/// mean parsing FBX twice, so the textures are cooked as the assets they already are.
/// </remarks>
public sealed class ModelImporter(NativeTool? tool = null) : Importer
{
    private static readonly IReadOnlySet<string> Claimed = new HashSet<string> { "fbx", "obj" };

    public NativeTool Tool { get; } = tool ?? new NativeTool("orblit_import", environmentVariable: "ORBLIT_IMPORT");

    public override string Name => "model";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions => Claimed;

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        NativeWork.WithTemporaryDirectoryAsync("model", async work =>
        {
            var input = NativeWork.Inside(work, $"in.{request.Id.Extension}");
            await File.WriteAllBytesAsync(input, request.Bytes).ConfigureAwait(false);
            var output = NativeWork.Inside(work, "out.glb");

            await Tool.RunAsync([input, output], request.Id).ConfigureAwait(false);

            return File.Exists(output)
                ? new ImportResult
                {
                    Outputs = new Dictionary<string, byte[]> { ["glb"] = await File.ReadAllBytesAsync(output).ConfigureAwait(false) },
                }
                : throw new ImportFailureException(
                    request.Id,
                    "converted without error but wrote no GLB. The source may hold no geometry. "
                    + "An FBX of only cameras and lights does this.");
        });
}

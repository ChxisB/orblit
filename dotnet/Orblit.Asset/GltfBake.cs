using Orblit.VectorMath;

namespace Orblit.Asset;

/// <summary>Bakes a model's placements into its vertices.</summary>
public static class GltfBake
{
    /// <summary>
    /// A model placed once for each of <paramref name="placements"/>, as one GLB. A scene of a thousand rocks
    /// is a thousand draws however small each rock is, because a draw is a primitive and a renderer cannot
    /// join primitives that sit at different transforms. Baking each placement into the vertices turns the
    /// thousand into one model with as many primitives as the rock has materials. What it costs is that the
    /// thousand can no longer move on their own.
    /// </summary>
    /// <remarks>
    /// The transform of every mesh node in the model is baked in too. Normals turn with the inverse transpose,
    /// so a stretch does not tilt the light, and a mirrored placement turns its triangles round or it would
    /// draw inside out. Returns null when the model cannot be baked, which is a model with a skin, an
    /// animation, a morph target, a compressed buffer, a node that is instanced already, a position, normal or
    /// tangent not stored as floats, a buffer that is not in the file, or bytes that are not a model. A
    /// placement with no volume or with a NaN or an infinity in it is skipped. Lights and cameras are not
    /// carried over. The model's own geometry stays in the file beside the baked copy, because its textures
    /// share the buffer.
    /// </remarks>
    public static async Task<byte[]?> BakeInstancesAsync(
        byte[] model,
        IReadOnlyList<Matrix4> placements,
        int maxVertices = 65536)
    {
        if (placements.Count == 0)
        {
            return null;
        }

        GltfDocument document;
        try
        {
            document = await GltfDocument.ReadAsync(AssetId.Parse("baked.glb"), model, new MemoryAssetSource())
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is ImportFailureException or FormatException or AssetNotFoundException)
        {
            return null;
        }

        if (BakeParts.Read(document) is not { } parts)
        {
            return null;
        }

        var batches = new OrderedDictionary<string, BakeBatch>();
        foreach (var placement in placements.Where(Placeable))
        {
            foreach (var part in parts)
            {
                if (!batches.TryGetValue(part.Key, out var batch))
                {
                    batches[part.Key] = batch = new BakeBatch(document, maxVertices);
                }

                batch.Add(part.Source, placement.Multiplied(part.World));
            }
        }

        if (batches.Count == 0)
        {
            return null;
        }

        var primitives = batches.Values.SelectMany(batch => batch.Finish()).Cast<object?>().ToList();
        document.Json["meshes"] = new List<object?> { new GltfMap { ["primitives"] = primitives } };
        document.Json["nodes"] = new List<object?> { new GltfMap { ["mesh"] = 0 } };
        document.Json["scenes"] = new List<object?> { new GltfMap { ["nodes"] = new List<object?> { 0 } } };
        document.Json["scene"] = 0;
        return document.ToGlb();
    }

    // A placement with no volume draws nothing and has no normal matrix. One with a NaN or an infinity
    // would put them in the file's bounds.
    private static bool Placeable(Matrix4 placement) =>
        Enumerable.Range(0, 16).All(i => double.IsFinite(placement[i])) && placement.Determinant() != 0;
}

using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// Rows that say what a scene submitted, for a host that answers questions about a frame. A row
/// is the scene's statement and not the renderer's work: culling, range fade and batching come
/// after it, and the renderer's own count is in <see cref="FrameCapture"/>.
/// </summary>
public static class SceneRows
{
    // shortcut: builds every row on each call, which is cheap for thousands of draws. Make it
    // lazy when a scene holds tens of thousands and an assistant polls.

    /// <summary>
    /// One row for each object, then each population. The <c>id</c> is <c>object:</c> or
    /// <c>population:</c> and the key, because the two key spaces are separate. A null
    /// <c>mesh</c> is the built-in cube. <c>materialListed</c> is null when the row names no
    /// material, and false when an object names one the scene does not list, which draws it in its
    /// own colour on the default surface. <c>layer</c> is the one the renderer is sent, so an
    /// object past <see cref="RenderScene.MaxLayer"/> reads as the last. Only objects and
    /// populations have rows, so a pass can count more draws than there are rows.
    /// </summary>
    public static IReadOnlyList<OrderedDictionary<string, object?>> DrawRows(this RenderScene scene)
    {
        var listed = scene.Materials.Select(material => material.Key).ToHashSet();
        return
        [
            .. scene.Objects.Select(one => ObjectRow(one, listed)),
            .. scene.Populations.Select(PopulationRow),
        ];
    }

    /// <summary>
    /// The material with <paramref name="key"/>, with the ids of the objects made of it, or null
    /// when the scene lists none. When a key is listed twice the last wins, as it does in the
    /// scene message.
    /// </summary>
    public static OrderedDictionary<string, object?>? MaterialRow(this RenderScene scene, long key)
    {
        if (scene.Materials.LastOrDefault(material => material.Key == key) is not { } material)
        {
            return null;
        }

        var row = material.ToJson();
        row["usedBy"] = scene.Objects.Where(one => one.Material == key).Select(one => $"object:{one.Key}").ToArray();
        return row;
    }

    private static OrderedDictionary<string, object?> ObjectRow(RenderObject one, HashSet<long> listed) => new()
    {
        ["id"] = $"object:{one.Key}",
        ["kind"] = "object",
        ["key"] = one.Key,
        ["mesh"] = one.Mesh,
        ["material"] = one.Material,
        ["materialListed"] = one.Material is { } material ? listed.Contains(material) : null,
        ["instances"] = 1,
        ["visible"] = one.Visible,
        ["layer"] = Math.Clamp(one.Layer, 0, RenderScene.MaxLayer),
        ["castShadows"] = one.CastShadows,
        ["receiveShadows"] = one.ReceiveShadows,
        ["colour"] = Values(one.Colour),
        ["transform"] = Values(one.Transform),
    };

    private static OrderedDictionary<string, object?> PopulationRow(Population population) => new()
    {
        ["id"] = $"population:{population.Key}",
        ["kind"] = "population",
        ["key"] = population.Key,
        ["mesh"] = population.Mesh,
        ["material"] = null,
        ["materialListed"] = null,
        ["instances"] = population.Count,
        ["visible"] = population.IsVisible,
        ["layer"] = Math.Clamp(population.Layer, 0, RenderScene.MaxLayer),
        ["castShadows"] = population.CastShadows,
        ["receiveShadows"] = population.ReceiveShadows,
        ["range"] = population.Range,
        ["fade"] = EnumNames.Of(population.Fade),
        ["revision"] = population.Revision,
        ["minimum"] = Values(population.Bounds.Minimum),
        ["maximum"] = Values(population.Bounds.Maximum),
    };

    private static double[] Values(Vector3 vector) => [vector.X, vector.Y, vector.Z];

    private static double[] Values(Matrix4 matrix) => [.. Enumerable.Range(0, 16).Select(at => matrix[at])];
}

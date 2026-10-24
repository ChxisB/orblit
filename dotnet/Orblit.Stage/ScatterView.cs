using Orblit.Filament;
using Orblit.Terrain;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>What a scatter is drawn as: blocks as populations, models as objects.</summary>
public sealed record ScatterDrawn(IReadOnlyList<Population> Populations, IReadOnlyList<RenderObject> Objects);

/// <summary>How a scatter is drawn, and how its model paths and materials are resolved.</summary>
public sealed record ScatterViewOptions
{
    /// <summary>The key of the first model object. Each group's models follow from it, a million keys apart.</summary>
    public long ObjectKey { get; init; }

    /// <summary>Turns a layer's model path into the file the renderer reads. Null keeps the path.</summary>
    public Func<string, string>? Mesh { get; init; }

    /// <summary>Turns a layer's material path into the key of a material in the scene. Null, or a null answer, draws the model in its own materials.</summary>
    public Func<string, long?>? Material { get; init; }

    /// <summary>False leaves the model layers out, for a view that cannot afford an object each.</summary>
    public bool Models { get; init; } = true;

    /// <summary>Scales how far every block layer draws. Nought stays nought, which is no limit.</summary>
    public double RangeScale { get; init; } = 1;
}

/// <summary>
/// Turns the groups a <see cref="ScatterPlacer"/> placed into what the renderer draws. A layer with
/// no mesh is one population a region, sharing the group's buffers, so a region the renderer holds
/// costs nothing to pass again and an edited one is sent because its revision moved. A layer that
/// names a mesh is one object a member, each its own place, since a model is a thing the scene can
/// hold and a block is only a texture of them.
/// </summary>
public static class ScatterView
{
    /// <summary>Past this a region's models would run into the next group's keys.</summary>
    private const int ModelsPerGroup = 1048576;

    public static ScatterDrawn From(ScatterPlacer placer, int key, ScatterViewOptions? options = null)
    {
        options ??= new ScatterViewOptions();
        var populations = new List<Population>();
        var objects = new List<RenderObject>();
        foreach (var group in placer.Groups.Where(group => group.Count > 0))
        {
            var layer = placer.Layers[group.Layer];
            if (layer.Mesh is null)
            {
                populations.Add(PopulationOf(layer, group, key, options));
            }
            else if (options.Models)
            {
                objects.AddRange(ObjectsOf(layer, layer.Mesh, group, options));
            }
        }

        return new ScatterDrawn(populations, objects);
    }

    private static Population PopulationOf(ScatterLayer layer, ScatterGroup group, int key, ScatterViewOptions options) =>
        new(
            checked(key + group.Id),
            group.Transforms,
            group.Colours,
            new Bounds(group.Minimum, group.Maximum))
        {
            Range = layer.Range * options.RangeScale,
            Revision = unchecked((int)group.Revision),
            CastShadows = layer.CastShadows,
        };

    private static IEnumerable<RenderObject> ObjectsOf(ScatterLayer layer, string mesh, ScatterGroup group, ScatterViewOptions options)
    {
        if (group.Count >= ModelsPerGroup)
        {
            throw new ArgumentException(
                $"The \"{layer.Name}\" layer put {group.Count} models in one region; past {ModelsPerGroup}, draw it as blocks.");
        }

        var file = options.Mesh is null ? mesh : options.Mesh(mesh);
        var material = layer.Material is null ? null : options.Material?.Invoke(layer.Material);
        for (var i = 0; i < group.Count; i++)
        {
            yield return new RenderObject
            {
                Key = options.ObjectKey + (group.Id * (long)ModelsPerGroup) + i,
                Transform = TransformAt(group.Transforms, i),
                Colour = new Vector3(group.Colours[i * 3], group.Colours[(i * 3) + 1], group.Colours[(i * 3) + 2]),
                Mesh = file,
                Material = material,
                CastShadows = layer.CastShadows,
            };
        }
    }

    // A copy, since the group's array is shared with the population the renderer holds.
    private static Matrix4 TransformAt(float[] transforms, int member)
    {
        var matrix = new Matrix4();
        for (var n = 0; n < 16; n++)
        {
            matrix[n] = transforms[(member * 16) + n];
        }

        return matrix;
    }
}

using System.Runtime.InteropServices;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// Joins what a model draws as separate things. Materials that turn out identical become one, and the
/// primitives of a mesh that then share everything that makes a draw call become one.
/// </summary>
internal static class GltfMerge
{
    /// <summary>
    /// Points every primitive at the lowest-numbered material identical to its own, and returns how many
    /// materials stopped being used. Identical means the same JSON, key for key, apart from <c>name</c>:
    /// a real model calls its materials <c>wood</c> and <c>metal</c>, and those are exactly the two a
    /// texture atlas has just made into one. The unused materials stay in the document, because removing
    /// one renumbers every index that names a material after it.
    /// </summary>
    public static int MergeMaterials(GltfDocument document)
    {
        var materials = document.List("materials");
        if (materials.Count < 2)
        {
            return 0;
        }

        var first = new Dictionary<string, int>();
        var to = new Dictionary<int, int>();
        for (var i = 0; i < materials.Count; i++)
        {
            var key = DartJson.Encode(Identity(materials[i]));
            if (first.TryGetValue(key, out var already))
            {
                to[i] = already;
            }
            else
            {
                first[key] = i;
            }
        }

        foreach (var mesh in document.List("meshes"))
        {
            if (GltfJson.Get(mesh, "primitives") is List<object?> primitives)
            {
                Rewire(primitives, to);
            }
        }

        return to.Count;
    }

    /// <summary>
    /// Joins the primitives of each mesh that share everything that makes a draw call, and returns how many
    /// primitives stopped existing. A strip, a fan, a skin, a morph target and anything a variant swaps are
    /// left alone, because a wrong merge is a silently deformed model. Batches close before the vertex count
    /// passes <paramref name="maxVertices"/>, because one vertex past 65,535 doubles every index.
    /// </summary>
    public static int MergePrimitives(GltfDocument document, int maxVertices = 65536)
    {
        var removed = 0;
        foreach (var mesh in document.List("meshes"))
        {
            if (GltfJson.Get(mesh, "primitives") is List<object?> { Count: >= 2 } primitives)
            {
                removed += MergeMesh(document, primitives, maxVertices);
            }
        }

        return removed;
    }

    /// <summary>
    /// What two primitives have to agree on before they can become one, or null when this primitive cannot
    /// be merged at all.
    /// </summary>
    public static string? PrimitiveGroupKey(GltfDocument document, GltfMap primitive)
    {
        if (!IsMergeable(primitive) || GltfJson.Get(primitive, "attributes") is not GltfMap attributes)
        {
            return null;
        }

        if (attributes.ContainsKey("JOINTS_0") || attributes.ContainsKey("WEIGHTS_0")
            || !GltfJson.IsInt(GltfJson.Get(attributes, "POSITION")))
        {
            return null;
        }

        var indices = GltfJson.Get(primitive, "indices");
        if (indices is not null && (!GltfJson.IsInt(indices) || AccessorOrNull(document, indices) is null))
        {
            return null;
        }

        return AttributeParts(document, attributes) is { } parts
            ? $"material={GltfJson.Display(GltfJson.Get(primitive, "material"))}|{string.Join('|', parts)}"
            : null;
    }

    /// <summary>
    /// Widens <paramref name="low"/> and <paramref name="high"/> to cover <paramref name="positions"/>. A
    /// stale bounding box culls geometry that is on screen, which looks like objects blinking out at some
    /// camera angles, so every merged <c>POSITION</c> recomputes its own.
    /// </summary>
    public static void Widen(float[] positions, double[] low, double[] high)
    {
        for (var i = 0; i < positions.Length; i += 3)
        {
            for (var c = 0; c < 3; c++)
            {
                if (positions[i + c] < low[c])
                {
                    low[c] = positions[i + c];
                }

                if (positions[i + c] > high[c])
                {
                    high[c] = positions[i + c];
                }
            }
        }
    }

    private static GltfMap? AccessorOrNull(GltfDocument document, object? index)
    {
        try
        {
            return document.Accessor(GltfJson.Saturated(index));
        }
        catch (FormatException)
        {
            // Sparse, or not there. Either way, left alone.
            return null;
        }
    }

    // How each attribute is stored, by name, or null when one is not an accessor this can read.
    private static List<string>? AttributeParts(GltfDocument document, GltfMap attributes)
    {
        var parts = new List<string>();
        foreach (var name in attributes.Keys.Order(StringComparer.Ordinal))
        {
            var index = attributes[name];
            if (!GltfJson.IsInt(index) || AccessorOrNull(document, index) is not { } accessor)
            {
                return null;
            }

            parts.Add($"{name}:{GltfJson.Display(GltfJson.Get(accessor, "type"))}:"
                + $"{GltfJson.Display(GltfJson.Get(accessor, "componentType"))}:"
                + (GltfJson.Get(accessor, "normalized") is true ? "true" : "false"));
        }

        return parts;
    }

    private static bool IsMergeable(GltfMap primitive)
    {
        var mode = GltfJson.Get(primitive, "mode");
        if ((mode is null ? 4 : GltfJson.AsNumber(mode)) != 4 || primitive.ContainsKey("targets"))
        {
            return false;
        }

        return GltfJson.Get(primitive, "extensions") is not GltfMap extensions
            || !(extensions.ContainsKey("KHR_materials_variants") || extensions.ContainsKey("KHR_draco_mesh_compression"));
    }

    // The JSON of a material without its name, with its keys in order and every number a double, so two
    // materials written by two exporters in two orders compare equal. 1 and 1.0 are the same glTF number
    // and different Dart ones.
    private static GltfMap Identity(GltfMap material)
    {
        var kept = new GltfMap();
        foreach (var (key, value) in material)
        {
            if (key != "name")
            {
                kept[key] = value;
            }
        }

        return Sorted(kept);
    }

    private static object? Canonical(object? value) => value switch
    {
        GltfMap map => Sorted(map),
        List<object?> list => list.Select(Canonical).ToList(),
        _ => GltfJson.AsNumber(value) is { } number ? number : value,
    };

    private static GltfMap Sorted(GltfMap map)
    {
        var sorted = new GltfMap();
        foreach (var key in map.Keys.Order(StringComparer.Ordinal))
        {
            sorted[key] = Canonical(map[key]);
        }

        return sorted;
    }

    private static void Rewire(List<object?> primitives, Dictionary<int, int> to)
    {
        foreach (var primitive in primitives.OfType<GltfMap>())
        {
            Repoint(primitive, to);
            // Variants name materials too. One left pointing at a material nothing else uses still works,
            // but pointing it at the survivor is what lets two variants turn out to be the same one.
            if (GltfJson.Get(primitive, "extensions") is GltfMap extensions
                && GltfJson.Get(extensions, "KHR_materials_variants") is GltfMap variants
                && GltfJson.Get(variants, "mappings") is List<object?> mappings)
            {
                foreach (var mapping in mappings.OfType<GltfMap>())
                {
                    Repoint(mapping, to);
                }
            }
        }
    }

    private static void Repoint(GltfMap holder, Dictionary<int, int> to)
    {
        var material = GltfJson.Get(holder, "material");
        if (GltfJson.IsInt(material) && to.TryGetValue(GltfJson.Saturated(material), out var survivor))
        {
            holder["material"] = survivor;
        }
    }

    private static int MergeMesh(GltfDocument document, List<object?> primitives, int maxVertices)
    {
        // Each batch replaces its first member where that member already was, and the rest go away.
        var replacing = new Dictionary<GltfMap, GltfMap>(ReferenceEqualityComparer.Instance);
        var dropping = new HashSet<GltfMap>(ReferenceEqualityComparer.Instance);
        var removed = 0;
        foreach (var members in GroupByKey(document, primitives).Where(members => members.Count >= 2))
        {
            foreach (var batch in Batches(document, members, maxVertices).Where(batch => batch.Count >= 2))
            {
                replacing[batch[0]] = Join(document, batch);
                dropping.UnionWith(batch.Skip(1));
                removed += batch.Count - 1;
            }
        }

        if (replacing.Count > 0)
        {
            Rebuild(primitives, replacing, dropping);
        }

        return removed;
    }

    // Grouped by what has to match, in the order the groups first appear. A model's primitive order is the
    // only draw order a glTF expresses, and shuffling it can change how two transparent surfaces overlap.
    private static List<List<GltfMap>> GroupByKey(GltfDocument document, List<object?> primitives)
    {
        var groups = new OrderedDictionary<string, List<GltfMap>>();
        foreach (var primitive in primitives.OfType<GltfMap>())
        {
            if (PrimitiveGroupKey(document, primitive) is not { } key)
            {
                continue;
            }

            if (!groups.TryGetValue(key, out var members))
            {
                groups[key] = members = [];
            }

            members.Add(primitive);
        }

        return [.. groups.Values];
    }

    private static void Rebuild(
        List<object?> primitives,
        Dictionary<GltfMap, GltfMap> replacing,
        HashSet<GltfMap> dropping)
    {
        var rebuilt = new List<object?>();
        foreach (var primitive in primitives)
        {
            if (primitive is not GltfMap map)
            {
                rebuilt.Add(primitive);
            }
            else if (replacing.TryGetValue(map, out var joined))
            {
                rebuilt.Add(joined);
            }
            else if (!dropping.Contains(map))
            {
                rebuilt.Add(primitive);
            }
        }

        primitives.Clear();
        primitives.AddRange(rebuilt);
    }

    // The batch closes before the index width changes rather than after: one vertex past 65,535 turns every
    // index in the merged primitive from two bytes into four, and that cost is paid on every draw forever.
    private static IEnumerable<List<GltfMap>> Batches(GltfDocument document, List<GltfMap> group, int maxVertices)
    {
        var batch = new List<GltfMap>();
        long vertices = 0;
        foreach (var primitive in group)
        {
            var count = VertexCount(document, primitive);
            if (batch.Count > 0 && vertices + count > maxVertices)
            {
                yield return batch;
                batch = [];
                vertices = 0;
            }

            batch.Add(primitive);
            vertices += count;
        }

        yield return batch;
    }

    private static int VertexCount(GltfDocument document, GltfMap primitive) =>
        document.AccessorCount(AttributeAt(primitive, "POSITION"));

    private static int AttributeAt(GltfMap primitive, string name) =>
        GltfJson.RequiredInt((GltfMap)primitive["attributes"]!, name);

    private static GltfMap Join(GltfDocument document, List<GltfMap> group)
    {
        var first = group[0];
        var attributes = new GltfMap();
        foreach (var name in ((GltfMap)first["attributes"]!).Keys.Order(StringComparer.Ordinal))
        {
            attributes[name] = JoinAttribute(document, group, name);
        }

        var joined = new GltfMap
        {
            ["attributes"] = attributes,
            ["indices"] = document.AddIndices(JoinIndices(document, group)),
        };
        if (GltfJson.Get(first, "material") is { } material)
        {
            joined["material"] = material;
        }

        // `extras` is somebody's data and the first primitive's is as good an answer as there is. `mode` is
        // 4 by definition here, which is the default, so it is left out.
        if (GltfJson.Get(first, "extras") is { } extras)
        {
            joined["extras"] = extras;
        }

        return joined;
    }

    private static int JoinAttribute(GltfDocument document, List<GltfMap> group, string name)
    {
        var position = name == "POSITION";
        var bytes = new List<byte>();
        var count = 0;
        double[] low = [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity];
        double[] high = [double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity];
        foreach (var primitive in group)
        {
            var accessor = AttributeAt(primitive, name);
            count += document.AccessorCount(accessor);
            bytes.AddRange(document.AccessorBytes(accessor));
            if (position)
            {
                Widen(document.ReadVec3(accessor), low, high);
            }
        }

        return document.AddLike(
            AttributeAt(group[0], name),
            CollectionsMarshal.AsSpan(bytes),
            count,
            position ? low : null,
            position ? high : null);
    }

    // Indices are renumbered as the vertices they point at move up the merged buffer. A primitive with none
    // is drawn in order, so its indices are the range it occupies.
    private static List<int> JoinIndices(GltfDocument document, List<GltfMap> group)
    {
        var indices = new List<int>();
        var baseIndex = 0;
        foreach (var primitive in group)
        {
            var count = VertexCount(document, primitive);
            var accessor = GltfJson.Get(primitive, "indices");
            if (GltfJson.IsInt(accessor))
            {
                // Dart's int is 64 bits and wraps nothing. An index at or past 2^31 throws here instead.
                indices.AddRange(document.ReadIndices(GltfJson.Saturated(accessor)).Select(i => checked((int)(baseIndex + i))));
            }
            else
            {
                indices.AddRange(Enumerable.Range(baseIndex, count));
            }

            baseIndex += count;
        }

        return indices;
    }
}

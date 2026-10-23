using Orblit.Mesh;

namespace Orblit.Scene;

/// <summary>
/// A model that could not be copied into an export, and why.
/// </summary>
/// <remarks>
/// Thrown rather than reported, because the alternative to copying a model in is writing a file that
/// silently lacks it. A scene that quietly lost a building is worse than an export that stopped and
/// said which file it could not read.
/// </remarks>
public sealed class GraftException(string message) : Exception(message);

/// <summary>
/// One model, copied in, and where its nodes landed.
/// </summary>
/// <param name="Roots">The nodes to hang under whatever is placing the model.</param>
/// <param name="From">The first node this graft wrote.</param>
/// <param name="Count">How many nodes it wrote.</param>
/// <param name="Shareable">
/// Whether placing the model a second time can share this copy's data. False for anything skinned or
/// animated. A skin names the joints it bends by node, and an animation names the nodes it moves, so
/// a second copy sharing them would be bent and moved by the first copy's bones, two models in
/// different places moving as one. Those get copied again in full. Everything else, which is most of
/// what a scene places twice, shares every byte it has.
/// </param>
public sealed record Grafted(IReadOnlyList<long> Roots, int From, int Count, bool Shareable);

/// <summary>
/// Copying one glTF document into another.
/// </summary>
/// <remarks>
/// glTF has no way for one file to point at another and say "draw that here". A scene that draws an
/// imported model therefore cannot reference it. It has to contain it. So the model's arrays are
/// appended to ours and every index inside them is moved on by however many entries were already
/// there: its meshes point at our accessors, its nodes at our meshes, its materials at our textures,
/// and so on through every cross-reference the format has.
/// <para>
/// The format keeps adding cross-references, and an extension may add one anywhere. So the links are
/// a table rather than a hundred lines of hand-written index arithmetic. A new extension that carries
/// an index is one row, in one place, which is the difference between a bug that stops the build and
/// a bug that shifts a texture by one and is found by somebody looking at a wall a month later.
/// </para>
/// <para>
/// Only self-contained sources are accepted. A model whose bytes or images live in a separate file is
/// refused by name, because the export would otherwise be a file that opens and is missing its skin.
/// Cooked models are single-buffer with their images inside, so in practice this refuses the
/// hand-made and the half-unpacked, which is what it is for.
/// </para>
/// </remarks>
public static class GltfGraft
{
    private const string Lights = "KHR_lights_punctual";
    private const string Variants = "KHR_materials_variants";

    // The top-level arrays that are copied wholesale. The order does not matter, because every base
    // was taken before any of them moved.
    private static readonly string[] Arrays =
    [
        "accessors", "animations", "cameras", "images", "materials", "meshes", "nodes", "samplers", "skins", "textures",
    ];

    // Every place one array's entry names another's, as a path inside an entry. A * stands for every
    // item of a list or every value of a map. The name on the right is the array the number at that
    // path counts into, which is the whole of the arithmetic, stated once.
    private static readonly (string Array, string Path, string Target)[] Links =
    [
        ("accessors", "bufferView", "bufferViews"),
        ("accessors", "sparse.indices.bufferView", "bufferViews"),
        ("accessors", "sparse.values.bufferView", "bufferViews"),

        // A channel's sampler counts into the animation's own samplers, not a document-level array,
        // so it is deliberately not here.
        ("animations", "samplers.*.input", "accessors"),
        ("animations", "samplers.*.output", "accessors"),
        ("animations", "channels.*.target.node", "nodes"),

        ("images", "bufferView", "bufferViews"),

        ("meshes", "primitives.*.indices", "accessors"),
        ("meshes", "primitives.*.attributes.*", "accessors"),
        ("meshes", "primitives.*.targets.*.*", "accessors"),
        ("meshes", "primitives.*.material", "materials"),
        ("meshes", "primitives.*.extensions.KHR_draco_mesh_compression.bufferView", "bufferViews"),
        ("meshes", "primitives.*.extensions.KHR_materials_variants.mappings.*.material", "materials"),
        ("meshes", "primitives.*.extensions.KHR_materials_variants.mappings.*.variants.*", "variants"),

        ("nodes", "camera", "cameras"),
        ("nodes", "children.*", "nodes"),
        ("nodes", "mesh", "meshes"),
        ("nodes", "skin", "skins"),
        ("nodes", "extensions.KHR_lights_punctual.light", "lights"),
        ("nodes", "extensions.EXT_mesh_gpu_instancing.attributes.*", "accessors"),

        ("skins", "inverseBindMatrices", "accessors"),
        ("skins", "joints.*", "nodes"),
        ("skins", "skeleton", "nodes"),

        ("textures", "source", "images"),
        ("textures", "sampler", "samplers"),
        // Every texture extension that supplies an image (basis, webp, ddsn) spells it source, so
        // one row covers all of them and the ones to come.
        ("textures", "extensions.*.source", "images"),
    ];

    // Everything a texture info may hold besides its index. The list is what makes recognising one
    // safe: a map with an index and a key outside this set is something else, and is left alone.
    private static readonly HashSet<string> TextureInfo = new(StringComparer.Ordinal)
    {
        "index", "texCoord", "scale", "strength", "extensions", "extras",
    };

    /// <summary>
    /// Copies every part of <paramref name="model"/> into <paramref name="into"/>, returning its
    /// roots' new indices. The model's bytes go on the end of <paramref name="buffer"/>.
    /// </summary>
    /// <exception cref="GraftException">The model keeps data or pictures in files beside it.</exception>
    // F2: five values, none of them optional, called in the order a reader expects.
    public static Grafted Graft(
        OrderedDictionary<string, object?> into,
        GltfBuffer buffer,
        IReadOnlyDictionary<string, object?> model,
        byte[] bytes,
        string where)
    {
        RefuseLooseFiles(model, where);
        var first = Count(into, "nodes");

        // Where each of the source's arrays lands. Captured before anything is copied: once copying
        // starts the lengths move, and every index in the source was written against the source's
        // own numbering.
        var bases = BasesOf(into, buffer);

        // The source's bytes go on the end of ours, and its views move with them. Alignment
        // survives because the builder pads to four and the format asks for no more than four.
        var at = bytes.Length == 0 ? 0 : buffer.AddBytes(bytes);
        foreach (var raw in Items(model, "bufferViews"))
        {
            var view = Clone(raw);
            view["buffer"] = 0L;
            view["byteOffset"] = OffsetOf(view) + at;
            buffer.Views.Add(view);
        }

        foreach (var name in Arrays)
        {
            CopyArray(into, buffer, model, name, bases);
        }

        MergeExtensionLists(into, model);
        MergeNames(into, model);

        return new Grafted(
            RootsOf(model, bases["nodes"]),
            first,
            Count(into, "nodes") - first,
            Items(model, "skins").Count == 0 && Items(model, "animations").Count == 0);
    }

    /// <summary>
    /// Places an already-grafted model again, without copying its data.
    /// </summary>
    /// <remarks>
    /// A scene that puts the same tree down two hundred times should be one tree and two hundred
    /// placements, not two hundred trees. glTF already allows this, since a mesh may be drawn by any
    /// number of nodes, so what has to be copied is the nodes and nothing else: no bytes, no
    /// accessors, no materials, no textures. For a model of any size that is the difference between
    /// a file somebody can ship and one they cannot.
    /// </remarks>
    public static IReadOnlyList<long> Regraft(OrderedDictionary<string, object?> into, Grafted first)
    {
        var nodes = (List<object?>)into["nodes"]!;
        long by = nodes.Count - first.From;
        for (var i = 0; i < first.Count; i++)
        {
            var copy = Clone((IReadOnlyDictionary<string, object?>)nodes[first.From + i]!);
            Shift(copy, ["children", "*"], by);
            nodes.Add(copy);
        }

        return [.. first.Roots.Select(root => root + by)];
    }

    /// <summary>
    /// A whole number as the writer or the reader holds it. The writer stores indices as
    /// <c>int</c> and a parsed document holds <c>long</c>, and both are whole numbers here.
    /// </summary>
    internal static long? Whole(object? value) => value switch
    {
        int small => small,
        long large => large,
        _ => null,
    };

    private static Dictionary<string, long> BasesOf(OrderedDictionary<string, object?> into, GltfBuffer buffer) => new(StringComparer.Ordinal)
    {
        ["accessors"] = buffer.Accessors.Count,
        ["bufferViews"] = buffer.Views.Count,
        ["animations"] = Count(into, "animations"),
        ["cameras"] = Count(into, "cameras"),
        ["images"] = Count(into, "images"),
        ["materials"] = Count(into, "materials"),
        ["meshes"] = Count(into, "meshes"),
        ["nodes"] = Count(into, "nodes"),
        ["samplers"] = Count(into, "samplers"),
        ["skins"] = Count(into, "skins"),
        ["textures"] = Count(into, "textures"),
        ["lights"] = ExtensionOf(into, Lights, "lights").Count,
        ["variants"] = ExtensionOf(into, Variants, "variants").Count,
    };

    private static void CopyArray(
        OrderedDictionary<string, object?> into,
        GltfBuffer buffer,
        IReadOnlyDictionary<string, object?> model,
        string name,
        Dictionary<string, long> bases)
    {
        var items = Items(model, name);
        if (items.Count == 0)
        {
            return;
        }

        var links = Links.Where(link => link.Array == name).ToList();
        foreach (var raw in items)
        {
            var item = Clone(raw);
            foreach (var link in links)
            {
                Shift(item, link.Path.Split('.'), bases[link.Target]);
            }

            // A material's textures are reached through texture infos, and every extension that adds
            // a map adds another place one can sit. Rather than list them, they are recognised by
            // shape: a map with an index and nothing in it that is not part of a texture info.
            if (name == "materials")
            {
                ShiftTextures(item, bases["textures"]);
            }

            if (name == "accessors")
            {
                buffer.Accessors.Add(item);
            }
            else
            {
                ListIn(into, name).Add(item);
            }
        }
    }

    // Made only when there is something to put in it. An extension object with an empty list in it
    // is a document claiming to use an extension it does not, which the validator is right to refuse.
    private static void MergeExtensionLists(OrderedDictionary<string, object?> into, IReadOnlyDictionary<string, object?> model)
    {
        foreach (var (extension, key) in new[] { (Lights, "lights"), (Variants, "variants") })
        {
            var theirs = ExtensionOf(model, extension, key);
            if (theirs.Count == 0)
            {
                continue;
            }

            var ours = Extension(into, extension, key);
            ours.AddRange(theirs.Select(item => Clone(item)));
        }
    }

    private static void MergeNames(OrderedDictionary<string, object?> into, IReadOnlyDictionary<string, object?> model)
    {
        foreach (var key in new[] { "extensionsUsed", "extensionsRequired" })
        {
            var theirs = model.GetValueOrDefault(key) is IReadOnlyList<object?> list ? list.OfType<string>().ToList() : [];
            if (theirs.Count == 0)
            {
                continue;
            }

            var ours = ListIn(into, key);
            foreach (var name in theirs.Where(name => !ours.Contains(name)))
            {
                ours.Add(name);
            }
        }
    }

    private static int Count(IReadOnlyDictionary<string, object?> json, string name) =>
        json.GetValueOrDefault(name) is IReadOnlyList<object?> list ? list.Count : 0;

    private static List<IReadOnlyDictionary<string, object?>> Items(IReadOnlyDictionary<string, object?> json, string name) =>
        json.GetValueOrDefault(name) is IReadOnlyList<object?> list
            ? [.. list.OfType<IReadOnlyDictionary<string, object?>>()]
            : [];

    // The list at the top-level key, made if it is not there yet.
    private static List<object?> ListIn(OrderedDictionary<string, object?> json, string name)
    {
        if (!json.TryGetValue(name, out var existing))
        {
            json[name] = existing = new List<object?>();
        }

        return (List<object?>)existing!;
    }

    // The list at extensions.<name>.<key>, made if it is not there yet. Only to be called when
    // something is about to go into it: an extension object nobody fills is a claim the document
    // cannot back up.
    private static List<object?> Extension(OrderedDictionary<string, object?> json, string name, string key)
    {
        var extensions = MapIn(json, "extensions");
        var extension = MapIn(extensions, name);
        return ListIn(extension, key);
    }

    private static OrderedDictionary<string, object?> MapIn(OrderedDictionary<string, object?> json, string name)
    {
        if (!json.TryGetValue(name, out var existing))
        {
            json[name] = existing = new OrderedDictionary<string, object?>();
        }

        return (OrderedDictionary<string, object?>)existing!;
    }

    // The same list, read only, without writing an empty one into a document that had none.
    private static List<IReadOnlyDictionary<string, object?>> ExtensionOf(IReadOnlyDictionary<string, object?> json, string name, string key) =>
        json.GetValueOrDefault("extensions") is IReadOnlyDictionary<string, object?> extensions
        && extensions.GetValueOrDefault(name) is IReadOnlyDictionary<string, object?> extension
            ? Items(extension, key)
            : [];

    // A private copy, so that grafting the same model twice does not write the first graft's
    // numbering into the second.
    private static OrderedDictionary<string, object?> Clone(IReadOnlyDictionary<string, object?> value) =>
        (OrderedDictionary<string, object?>)Cloned(value)!;

    private static object? Cloned(object? value) => value switch
    {
        IReadOnlyDictionary<string, object?> map => new OrderedDictionary<string, object?>(
            map.Select(entry => KeyValuePair.Create(entry.Key, Cloned(entry.Value)))),
        IReadOnlyList<object?> list => new List<object?>(list.Select(Cloned)),
        _ => value,
    };

    // Adds by to whatever integer sits at steps inside node.
    private static void Shift(object? node, ReadOnlySpan<string> steps, long by)
    {
        if (by == 0 || node is null || steps.IsEmpty)
        {
            return;
        }

        if (steps.Length == 1)
        {
            ShiftHere(node, steps[0], by);
            return;
        }

        foreach (var child in Reached(node, steps[0]))
        {
            Shift(child, steps[1..], by);
        }
    }

    // What one step of a path names inside node: the value at a key, or with * every item of a list
    // or every value of a map.
    private static List<object?> Reached(object node, string key) => (node, key) switch
    {
        (OrderedDictionary<string, object?> map, "*") => [.. map.Values],
        (OrderedDictionary<string, object?> map, _) => map.TryGetValue(key, out var value) ? [value] : [],
        (List<object?> list, "*") => [.. list],
        _ => [],
    };

    private static void ShiftHere(object node, string key, long by)
    {
        switch (node)
        {
            case OrderedDictionary<string, object?> map when key != "*":
                if (map.TryGetValue(key, out var value) && Whole(value) is { } whole)
                {
                    map[key] = whole + by;
                }

                break;
            case OrderedDictionary<string, object?> map:
                foreach (var name in map.Keys.ToList().Where(name => Whole(map[name]) is not null))
                {
                    map[name] = Whole(map[name])!.Value + by;
                }

                break;
            case List<object?> list when key == "*":
                for (var i = 0; i < list.Count; i++)
                {
                    if (Whole(list[i]) is { } one)
                    {
                        list[i] = one + by;
                    }
                }

                break;
            default:
                break;
        }
    }

    // Moves every texture info found anywhere inside a material.
    private static void ShiftTextures(object? node, long by)
    {
        if (by == 0)
        {
            return;
        }

        switch (node)
        {
            case List<object?> list:
                foreach (var item in list)
                {
                    ShiftTextures(item, by);
                }

                break;
            case OrderedDictionary<string, object?> map:
                if (map.TryGetValue("index", out var index) && Whole(index) is { } at && map.Keys.All(TextureInfo.Contains))
                {
                    map["index"] = at + by;
                }

                foreach (var value in map.Values.ToList())
                {
                    ShiftTextures(value, by);
                }

                break;
            default:
                break;
        }
    }

    // The model's own roots, moved on by by. A file with no scenes is legal and means a library of
    // nodes rather than something to draw. What it would draw is every node nobody claims as a
    // child, so that is what gets hung under ours.
    private static List<long> RootsOf(IReadOnlyDictionary<string, object?> model, long by)
    {
        var scenes = Items(model, "scenes");
        if (scenes.Count > 0)
        {
            var which = Whole(model.GetValueOrDefault("scene"));
            var scene = which is >= 0 && which < scenes.Count ? scenes[(int)which] : scenes[0];
            return Wholes(scene.GetValueOrDefault("nodes")).Select(node => node + by).ToList();
        }

        var nodes = Items(model, "nodes");
        var claimed = nodes.SelectMany(node => Wholes(node.GetValueOrDefault("children"))).ToHashSet();
        return Enumerable.Range(0, nodes.Count).Where(i => !claimed.Contains(i)).Select(i => i + by).ToList();
    }

    private static IEnumerable<long> Wholes(object? list) =>
        list is IReadOnlyList<object?> items ? items.Select(Whole).OfType<long>() : [];

    private static void RefuseLooseFiles(IReadOnlyDictionary<string, object?> model, string where)
    {
        var buffers = Items(model, "buffers");
        if (buffers.Count > 1)
        {
            throw new GraftException(
                $"{where} keeps its data in {buffers.Count} buffers. An export can copy "
                + "in a model that carries its bytes with it; cook it first, or save it "
                + "as a single-buffer GLB.");
        }

        if (buffers.Count > 0 && buffers[0].GetValueOrDefault("uri") is { } uri)
        {
            throw new GraftException(
                $"{where} keeps its bytes in {uri}, beside it rather "
                + "than inside it. An export can only copy in a model that is whole; save "
                + "it as a GLB and try again.");
        }

        foreach (var image in Items(model, "images"))
        {
            if (image.GetValueOrDefault("uri") is string link && !link.StartsWith("data:", StringComparison.Ordinal))
            {
                throw new GraftException(
                    $"{where} reads a texture from {link}, beside it rather than inside it. "
                    + "An export can only copy in a model that is whole; save it as a GLB "
                    + "with its textures embedded and try again.");
            }
        }
    }

    // A view with no offset starts at the front. One that states an offset that is not a whole
    // number is not a view, so it stops here rather than moving by a guess.
    private static long OffsetOf(IReadOnlyDictionary<string, object?> view) => view.GetValueOrDefault("byteOffset") switch
    {
        null => 0,
        var value => Whole(value) ?? throw new InvalidCastException($"{value.GetType().Name} is not a whole number."),
    };
}

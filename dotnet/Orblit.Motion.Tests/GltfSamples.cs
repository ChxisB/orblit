using System.Buffers.Binary;
using System.Text;
using Orblit.Json;

namespace Orblit.Motion.Tests;

/// <summary>A <c>.gltf</c> put together in memory, its data carried in the file itself.</summary>
internal sealed class Built
{
    private readonly List<byte> _data = [];
    private readonly List<object?> _views = [];
    private readonly List<object?> _accessors = [];

    public byte[] Data => [.. _data];

    /// <summary><paramref name="numbers"/> as an accessor of <paramref name="type"/>, and its index.</summary>
    public int Floats(double[] numbers, string type)
    {
        var width = type switch
        {
            "VEC3" => 3,
            "VEC4" => 4,
            _ => 1,
        };
        var bytes = new byte[numbers.Length * 4];
        for (var i = 0; i < numbers.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), (float)numbers[i]);
        }

        _views.Add(Tree.Map(("buffer", 0), ("byteOffset", _data.Count), ("byteLength", bytes.Length)));
        _data.AddRange(bytes);
        _accessors.Add(Tree.Map(
            ("bufferView", _views.Count - 1),
            ("componentType", 5126),
            ("type", type),
            ("count", numbers.Length / width)));
        return _accessors.Count - 1;
    }

    /// <summary>The file, with <paramref name="uri"/> naming its data when it is kept beside it.</summary>
    public byte[] File(IReadOnlyDictionary<string, object?> document, string? uri = null)
    {
        var data = Data;
        var buffer = Tree.Map(("byteLength", data.Length), ("uri", uri ?? $"data:application/octet-stream;base64,{Convert.ToBase64String(data)}"));
        var whole = Tree.With(
            document,
            ("asset", Tree.Map(("version", "2.0"))),
            ("buffers", Tree.List(buffer)),
            ("bufferViews", _views),
            ("accessors", _accessors));
        return Encoding.UTF8.GetBytes(JsonText.Encode(whole));
    }
}

/// <summary>GlTF documents for the import tests, and the sample models when they have been fetched.</summary>
internal static class GltfSamples
{
    /// <summary>A document that keeps no data of its own, which is all a skeleton needs.</summary>
    public static byte[] Plain(IReadOnlyDictionary<string, object?> document) =>
        Encoding.UTF8.GetBytes(JsonText.Encode(Tree.With(document, ("asset", Tree.Map(("version", "2.0"))))));

    public static OrderedDictionary<string, object?> Sampler(int input, int output, string? interpolation = null) =>
        interpolation is null
            ? Tree.Map(("input", input), ("output", output))
            : Tree.Map(("input", input), ("output", output), ("interpolation", interpolation));

    public static OrderedDictionary<string, object?> Channel(int sampler, int node, string path) =>
        Tree.Map(("sampler", sampler), ("target", Tree.Map(("node", node), ("path", path))));

    /// <summary>
    /// An armature with two joints, one of them unnamed, a door the scene gave an id, and a face with morph
    /// targets, moved by three animations.
    /// </summary>
    public static (byte[] Bytes, byte[] Data) Armature(string? uri = null)
    {
        var built = new Built();
        var half = built.Floats([0, 0.5], "SCALAR");
        var quarter = built.Floats([0, 0.25], "SCALAR");
        var frames = built.Floats([0, 1.0 / 24, 2.0 / 24], "SCALAR");
        var tenth = built.Floats([0, 0.1], "SCALAR");

        // Arriving slope, value, leaving slope, for each of two keys. The second value is twice as long as a
        // rotation should be.
        var turn = built.Floats(
            [
                0, 0, 0, 0, 0, 0, 0, 1, 0, 0.5, 0, 0,
                0, 0.25, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0,
            ],
            "VEC4");
        var lift = built.Floats([0, 0, 0, 0, 1, 0], "VEC3");
        var slide = built.Floats([0, 0, 0, 1, 0, 0], "VEC3");
        var grow = built.Floats([1, 1, 1, 2, 2, 2], "VEC3");
        var stride = built.Floats([0, 0, 0, 0, 0, 0.1, 0, 0, 0.2], "VEC3");
        var lone = built.Floats([1, 1, 1], "VEC3");

        var bytes = built.File(
            Tree.Map(
                ("nodes", Tree.List(
                    Tree.Map(("name", "Armature"), ("children", Tree.List(1, 3, 4))),
                    Tree.Map(("name", "hips"), ("children", Tree.List(2))),
                    Tree.Map(),
                    Tree.Map(("name", "Door"), ("extras", Tree.Map(("orblit", Tree.Map(("id", "house/door")))))),
                    Tree.Map(("name", "Face"), ("mesh", 0)))),
                ("meshes", Tree.List(Tree.Map(("name", "Face"), ("primitives", Tree.List())))),
                ("skins", Tree.List(Tree.Map(("joints", Tree.List(1, 2))))),
                ("animations", Tree.List(
                    Tree.Map(
                        ("name", "Walk Loop"),
                        ("samplers", Tree.List(
                            Sampler(half, turn, "CUBICSPLINE"),
                            Sampler(quarter, lift, "STEP"),
                            Sampler(half, slide),
                            Sampler(half, grow, "LINEAR"))),
                        ("channels", Tree.List(
                            Channel(0, 1, "rotation"),
                            Channel(1, 2, "translation"),
                            Channel(2, 3, "translation"),
                            Channel(3, 0, "scale"),
                            Channel(2, 4, "weights"),
                            Channel(3, 3, "translation")))),
                    Tree.Map(
                        ("name", "cycle run"),
                        ("samplers", Tree.List(Sampler(frames, stride))),
                        ("channels", Tree.List(Channel(0, 1, "translation")))),
                    Tree.Map(
                        ("samplers", Tree.List(Sampler(tenth, slide), Sampler(tenth, lone))),
                        ("channels", Tree.List(
                            Channel(0, 3, "translation"),
                            Channel(0, 9, "translation"),
                            Channel(5, 3, "rotation"),
                            Channel(0, 3, "pointer"),
                            Channel(1, 1, "scale"))))))),
            uri);
        return (bytes, built.Data);
    }

    /// <summary>
    /// A sample model found under <c>assets/samples</c> in this checkout or one above it. CI does not fetch
    /// them, so a test that needs one is skipped when it is not there.
    /// </summary>
    public static byte[] Sample(string name)
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var file = Path.Combine(folder.FullName, "assets", "samples", name + ".glb");
            if (System.IO.File.Exists(file))
            {
                return System.IO.File.ReadAllBytes(file);
            }
        }

        Assert.Skip($"{name}.glb is not in assets/samples");
        return [];
    }
}

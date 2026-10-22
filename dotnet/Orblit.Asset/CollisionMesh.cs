using System.Text;
using Orblit.Json;
using Orblit.Mesh;

namespace Orblit.Asset;

/// <summary>Static model geometry in the model's frame, independent of its materials.</summary>
public sealed class CollisionMesh
{
    public CollisionMesh(IReadOnlyList<double> vertices, IReadOnlyList<int> indices)
    {
        Vertices = Array.AsReadOnly(vertices.ToArray());
        Indices = Array.AsReadOnly(indices.ToArray());
        if (!IsShape(Vertices, Indices))
        {
            throw new ArgumentException("Expected finite positions and indexed triangles.");
        }
    }

    /// <summary>Three numbers a point.</summary>
    public IReadOnlyList<double> Vertices { get; }

    /// <summary>Three indices into <see cref="Vertices"/> a triangle.</summary>
    public IReadOnlyList<int> Indices { get; }

    public GltfMap ToJson() => new()
    {
        ["version"] = 1,
        ["vertices"] = new List<object?>(Vertices.Select(v => (object?)v)),
        ["indices"] = new List<object?>(Indices.Select(i => (object?)i)),
    };

    public byte[] Encode() => Encoding.UTF8.GetBytes(JsonText.Encode(ToJson()));

    /// <summary>
    /// Throws <see cref="FormatException"/> for bytes that are not collision mesh version 1, and
    /// <see cref="ArgumentException"/> for geometry that is not a shape.
    /// </summary>
    public static CollisionMesh Decode(ReadOnlySpan<byte> bytes)
    {
        if (JsonText.Decode(TextRules.Decode(bytes)) is not GltfMap json
            || !IsVersionOne(GltfJson.Get(json, "version"))
            || GltfJson.Get(json, "vertices") is not List<object?> positions
            || GltfJson.Get(json, "indices") is not List<object?> triangles)
        {
            throw new FormatException("Expected collision mesh version 1.");
        }

        var numbers = positions.Select(GltfJson.AsNumber).OfType<double>().ToList();
        if (numbers.Count != positions.Count || !triangles.All(GltfJson.IsInt))
        {
            throw new FormatException("Invalid collision mesh geometry.");
        }

        return new CollisionMesh(numbers, [.. triangles.Select(GltfJson.Saturated)]);
    }

    /// <summary>
    /// Reads every face of a Wavefront OBJ as one mesh, polygons cut into triangles. Groups and
    /// materials do not matter to a collider, so the <c>.mtl</c> is not read. Throws
    /// <see cref="FormatException"/> for a file that holds no face, passes <paramref name="limits"/>,
    /// or has a point too far out for a collider.
    /// </summary>
    public static CollisionMesh FromObj(byte[] bytes, ReadLimits? limits = null)
    {
        var triangles = ObjFiles.ReadObj(bytes, limits: limits).Mesh.Triangulate();
        try
        {
            // An index past 31 bits wraps below nought, which the constructor refuses as it refuses
            // one past the vertices.
            return new CollisionMesh(
                [.. triangles.Positions.Select(p => (double)p)],
                [.. triangles.Indices.Select(i => unchecked((int)i))]);
        }
        catch (ArgumentException error)
        {
            // A point past what a float holds is read as a double and narrowed to infinity here,
            // which the constructor refuses.
            throw new FormatException($"This OBJ makes no collision shape: {error.Message}");
        }
    }

    /// <summary>
    /// Reads the active scene with node transforms, strips and fans baked in. Skins and morph
    /// targets need a posed collider and are refused here.
    /// </summary>
    public static async Task<CollisionMesh> FromGltfAsync(AssetId id, byte[] bytes, IAssetSource source)
    {
        var document = await GltfDocument.ReadAsync(id, bytes, source).ConfigureAwait(false);
        return new ModelGeometry(document).Read();
    }

    // A version written as 1.0 is read as one, because 1.0 is the number 1.
    private static bool IsVersionOne(object? version) => GltfJson.AsNumber(version) == 1;

    private static bool IsShape(IReadOnlyList<double> vertices, IReadOnlyList<int> indices)
    {
        var points = vertices.Count / 3;
        return vertices.Count >= 9
            && vertices.Count % 3 == 0
            && vertices.All(double.IsFinite)
            && indices.Count > 0
            && indices.Count % 3 == 0
            && indices.All(i => i >= 0 && i < points);
    }
}

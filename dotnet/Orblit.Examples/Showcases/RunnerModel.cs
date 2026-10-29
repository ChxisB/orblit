using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>How finely a ball is cut, and how it is finished.</summary>
/// <param name="Lumpy">Pushes each corner in or out by up to this fraction, the same way every time, which is a rock.</param>
internal sealed record BallStyle(int Segments, int Rings, bool Flat = false, double Lumpy = 0, double Roughness = 0.8);

/// <summary>How finely a tube is cut, and how it is finished.</summary>
/// <param name="Top">The radius at the top as a fraction of the bottom, so zero is a cone.</param>
internal sealed record TubeStyle(double Top = 1, int Segments = 12, bool Flat = false, double Roughness = 0.8);

/// <summary>A shape's corners before it is placed, and the triangles that join them.</summary>
internal sealed class Piece
{
    private readonly List<Vector3> _points = [];
    private readonly List<Vector3> _normals = [];
    private readonly List<Vector2> _uvs = [];
    private readonly List<int> _indices = [];

    public IReadOnlyList<Vector3> Points => _points;

    /// <summary>Which way is out at each corner.</summary>
    public IReadOnlyList<Vector3> Normals => _normals;

    public IReadOnlyList<Vector2> Uvs => _uvs;

    /// <summary>Three corners to a triangle.</summary>
    public IReadOnlyList<int> Indices => _indices;

    /// <summary>A flat polygon whose corners all face one way, cut into a fan from the first corner.</summary>
    public static Piece Polygon(Vector3[] corners, Vector3 facing, Vector2[] uvs)
    {
        var piece = new Piece();
        for (var i = 0; i < corners.Length; i++)
        {
            piece.Corner(corners[i], facing, uvs[i]);
        }

        for (var i = 2; i < corners.Length; i++)
        {
            piece.Triangle(0, i - 1, i);
        }

        return piece;
    }

    public void Corner(Vector3 point, Vector3 normal, Vector2 uv)
    {
        _points.Add(point);
        _normals.Add(normal);
        _uvs.Add(uv);
    }

    public void Triangle(int a, int b, int c) => _indices.AddRange([a, b, c]);
}

/// <summary>
/// A model being made out of simple shapes, written out as a binary glTF.
/// </summary>
/// <remarks>
/// Everything of one colour is one primitive, whatever shape it came from. So
/// a house of eleven boxes is five draws rather than eleven.
///
/// Triangles are kept separate rather than sharing corners. That costs memory
/// nobody here is short of, and buys shapes that are faceted on purpose. A
/// low-poly pine is a pine. A smooth one is a green blob.
/// </remarks>
internal sealed class RunnerModel
{
    private static readonly (Vector3 N, Vector3 U, Vector3 V)[] Faces =
    [
        (new(1, 0, 0), new(0, 0, -1), new(0, 1, 0)),
        (new(-1, 0, 0), new(0, 0, 1), new(0, 1, 0)),
        (new(0, 1, 0), new(1, 0, 0), new(0, 0, -1)),
        (new(0, -1, 0), new(1, 0, 0), new(0, 0, 1)),
        (new(0, 0, 1), new(1, 0, 0), new(0, 1, 0)),
        (new(0, 0, -1), new(-1, 0, 0), new(0, 1, 0)),
    ];

    private static readonly Vector2[] Square = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)];

    private readonly List<Surface> _surfaces = [];

    /// <summary>A cube one unit across, centred on the origin, placed by <paramref name="at"/>.</summary>
    public void Box(Matrix4 at, uint colour, double roughness = 0.8)
    {
        var into = SurfaceFor(colour, roughness);
        foreach (var (n, u, v) in Faces)
        {
            var centre = n * 0.5;
            var (halfU, halfV) = (u * 0.5, v * 0.5);
            Vector3[] corners = [centre - halfU - halfV, centre + halfU - halfV, centre + halfU + halfV, centre - halfU + halfV];
            Shape(into, at, Piece.Polygon(corners, n, Square), flat: true);
        }
    }

    /// <summary>A ball of radius one about the origin, placed by <paramref name="at"/>.</summary>
    public void Ball(Matrix4 at, uint colour, BallStyle style)
    {
        var piece = new Piece();
        for (var ring = 0; ring <= style.Rings; ring++)
        {
            var polar = (double)ring / style.Rings * Math.PI;
            for (var segment = 0; segment <= style.Segments; segment++)
            {
                var around = (double)segment / style.Segments * Math.PI * 2;
                var n = new Vector3(Math.Sin(polar) * Math.Cos(around), Math.Cos(polar), Math.Sin(polar) * Math.Sin(around));
                var uv = new Vector2((double)segment / style.Segments, (double)ring / style.Rings);
                piece.Corner(n * Radius(n, style.Lumpy), n, uv);
            }
        }

        var across = style.Segments + 1;
        for (var ring = 0; ring < style.Rings; ring++)
        {
            for (var segment = 0; segment < style.Segments; segment++)
            {
                var a = (ring * across) + segment;
                var c = a + across;
                piece.Triangle(a, c, a + 1);
                piece.Triangle(a + 1, c, c + 1);
            }
        }

        Shape(SurfaceFor(colour, style.Roughness), at, piece, style.Flat);
    }

    /// <summary>A cylinder of radius one from y = 0 to y = 1, placed by <paramref name="at"/>.</summary>
    public void Tube(Matrix4 at, uint colour, TubeStyle? style = null)
    {
        style ??= new TubeStyle();
        var piece = new Piece();
        for (var segment = 0; segment <= style.Segments; segment++)
        {
            var around = (double)segment / style.Segments * Math.PI * 2;
            var (c, s) = (Math.Cos(around), Math.Sin(around));
            var side = new Vector3(c, 1 - style.Top, s);
            side.Normalize();
            var u = (double)segment / style.Segments;
            piece.Corner(new Vector3(c, 0, s), side, new Vector2(u, 1));
            piece.Corner(new Vector3(c * style.Top, 1, s * style.Top), side, new Vector2(u, 0));
        }

        for (var segment = 0; segment < style.Segments; segment++)
        {
            var a = segment * 2;
            piece.Triangle(a, a + 1, a + 2);
            piece.Triangle(a + 2, a + 1, a + 3);
        }

        Cap(piece, (0, 1, -1), style.Segments);
        Cap(piece, (1, style.Top, 1), style.Segments);
        Shape(SurfaceFor(colour, style.Roughness), at, piece, style.Flat);
    }

    /// <summary>
    /// A roof: a triangular prism one unit wide in x and deep in z, standing one
    /// unit tall from y = 0, placed by <paramref name="at"/>.
    /// </summary>
    public void Roof(Matrix4 at, uint colour)
    {
        var into = SurfaceFor(colour, 0.7);
        var (left, right, ridge) = (new Vector3(-0.5, 0, 0), new Vector3(0.5, 0, 0), new Vector3(0, 1, 0));
        var (back, front) = (new Vector3(0, 0, 0.5), new Vector3(0, 0, -0.5));
        Shape(into, at, Piece.Polygon([left + front, ridge + front, ridge + back, left + back], new Vector3(-1, 0.5, 0), Square), flat: true);
        Shape(into, at, Piece.Polygon([right + back, ridge + back, ridge + front, right + front], new Vector3(1, 0.5, 0), Square), flat: true);
        Shape(into, at, Piece.Polygon([left + back, right + back, right + front, left + front], new Vector3(0, -1, 0), Square), flat: true);
        foreach (var (end, facing) in (ReadOnlySpan<(Vector3, double)>)[(front, -1.0), (back, 1.0)])
        {
            Vector3[] corners = [left + end, right + end, ridge + end];
            Shape(into, at, Piece.Polygon(corners, new Vector3(0, 0, facing), [new(0, 1), new(1, 1), new(0.5, 0)]), flat: true);
        }
    }

    /// <summary>A flat piece laid where its corners say, in white, so a texture shows its own colours.</summary>
    public void Textured(double roughness, Piece piece) =>
        Shape(SurfaceFor(0xFFFFFFFF, roughness), Matrix4.Identity(), piece, flat: true);

    /// <summary>The model as a binary glTF: one mesh, one primitive per colour.</summary>
    public byte[] ToGlb()
    {
        var parts = new GlbParts();
        foreach (var surface in _surfaces.Where(surface => surface.Count > 0))
        {
            parts.Add(surface);
        }

        return parts.ToBytes();
    }

    /// <summary>
    /// Adds the triangles of <paramref name="piece"/>, placed by <paramref name="at"/>.
    /// </summary>
    /// <remarks>
    /// Each triangle is wound to agree with the normals. So no shape has to get
    /// its winding right, and a placement that mirrors does not turn a shape
    /// inside out. A flat shape then takes each triangle's own normal. A smooth
    /// one keeps the ones it was given.
    /// </remarks>
    private static void Shape(Surface into, Matrix4 at, Piece piece, bool flat)
    {
        var turn = new Matrix3();
        turn.CopyNormalMatrix(at);
        var placed = piece.Points.Select(at.Transformed3).ToList();
        var facing = piece.Normals.Select(normal => turn.Transformed(normal).Normalized()).ToList();
        for (var i = 0; i < piece.Indices.Count; i += 3)
        {
            var (a, b, c) = (piece.Indices[i], piece.Indices[i + 1], piece.Indices[i + 2]);
            var face = (placed[b] - placed[a]).Cross(placed[c] - placed[a]);

            // The corners at a ball's poles, which meet at a point.
            if (face.Length2 < 1e-12)
            {
                continue;
            }

            if (face.Dot(facing[a] + facing[b] + facing[c]) < 0)
            {
                (b, c) = (c, b);
                face = -face;
            }

            face.Normalize();
            foreach (var corner in (ReadOnlySpan<int>)[a, b, c])
            {
                into.Add(placed[corner], flat ? face : facing[corner], piece.Uvs[corner]);
            }
        }
    }

    /// <summary>A fan round the middle of one end of a tube, unless that end is a point.</summary>
    private static void Cap(Piece piece, (double Y, double Radius, double Facing) end, int segments)
    {
        if (end.Radius <= 0)
        {
            return;
        }

        var middle = piece.Points.Count;
        var normal = new Vector3(0, end.Facing, 0);
        piece.Corner(new Vector3(0, end.Y, 0), normal, new Vector2(0.5, 0.5));
        for (var segment = 0; segment <= segments; segment++)
        {
            var around = (double)segment / segments * Math.PI * 2;
            var point = new Vector3(Math.Cos(around) * end.Radius, end.Y, Math.Sin(around) * end.Radius);
            piece.Corner(point, normal, new Vector2(0.5 + (Math.Cos(around) / 2), 0.5 + (Math.Sin(around) / 2)));
        }

        for (var segment = 0; segment < segments; segment++)
        {
            piece.Triangle(middle, middle + 1 + segment, middle + 2 + segment);
        }
    }

    /// <summary>How far out the corner facing <paramref name="n"/> sits.</summary>
    private static double Radius(Vector3 n, double lumpy)
    {
        if (lumpy <= 0)
        {
            return 1;
        }

        // By where the corner is rather than by which one it is. So the copies at
        // the seam and at the poles move together, and the rock stays closed.
        var key = (Seventh(n.X) * 131) + (Seventh(n.Y) * 17) + (Seventh(n.Z) * 3);
        var hash = Math.Sin(key * 12.9898) * 43758.5453;
        return 1 + ((hash - Math.Floor(hash) - 0.5) * 2 * lumpy);
    }

    private static long Seventh(double value) => (long)Math.Round(value * 7, MidpointRounding.AwayFromZero);

    private Surface SurfaceFor(uint colour, double roughness)
    {
        var found = _surfaces.Find(surface => surface.Colour == colour && surface.Roughness == roughness);
        if (found is null)
        {
            found = new Surface(colour, roughness);
            _surfaces.Add(found);
        }

        return found;
    }

    /// <summary>Everything of one colour and one roughness, as the floats it is written out as.</summary>
    private sealed class Surface(uint colour, double roughness)
    {
        private readonly List<float> _positions = [];
        private readonly List<float> _normals = [];
        private readonly List<float> _uvs = [];

        public uint Colour { get; } = colour;

        public double Roughness { get; } = roughness;

        public IReadOnlyList<float> Positions => _positions;

        public IReadOnlyList<float> Normals => _normals;

        public IReadOnlyList<float> Uvs => _uvs;

        public int Count => _positions.Count / 3;

        public void Add(Vector3 point, Vector3 normal, Vector2 uv)
        {
            _positions.AddRange([(float)point.X, (float)point.Y, (float)point.Z]);
            _normals.AddRange([(float)normal.X, (float)normal.Y, (float)normal.Z]);
            _uvs.AddRange([(float)uv.X, (float)uv.Y]);
        }
    }

    /// <summary>The JSON and the binary of a glTF, gathered one surface at a time.</summary>
    private sealed class GlbParts
    {
        private readonly List<byte> _binary = [];
        private readonly List<object?> _views = [];
        private readonly List<object?> _accessors = [];
        private readonly List<object?> _materials = [];
        private readonly List<object?> _primitives = [];

        public void Add(Surface surface)
        {
            _materials.Add(new Dictionary<string, object?>
            {
                ["pbrMetallicRoughness"] = new Dictionary<string, object?>
                {
                    ["baseColorFactor"] = new List<object?>
                    {
                        Linear(surface.Colour >> 16), Linear(surface.Colour >> 8), Linear(surface.Colour), 1.0,
                    },
                    ["metallicFactor"] = 0.0,
                    ["roughnessFactor"] = surface.Roughness,
                },
            });
            var positions = Floats(surface.Positions, 3, "VEC3");
            var normals = Floats(surface.Normals, 3, "VEC3");
            var uvs = Floats(surface.Uvs, 2, "VEC2");
            _accessors.Add(Indices(surface.Count));
            _primitives.Add(new Dictionary<string, object?>
            {
                ["attributes"] = new Dictionary<string, object?>
                {
                    ["POSITION"] = positions,
                    ["NORMAL"] = normals,
                    ["TEXCOORD_0"] = uvs,
                },
                ["indices"] = _accessors.Count - 1,
                ["material"] = _materials.Count - 1,
            });
        }

        public byte[] ToBytes()
        {
            var bin = _binary.ToArray();
            var json = Encoding.UTF8.GetBytes(JsonText.Encode(new Dictionary<string, object?>
            {
                ["asset"] = new Dictionary<string, object?> { ["version"] = "2.0", ["generator"] = "Orblit runner" },
                ["scene"] = 0,
                ["scenes"] = new List<object?> { new Dictionary<string, object?> { ["nodes"] = new List<object?> { 0 } } },
                ["nodes"] = new List<object?> { new Dictionary<string, object?> { ["mesh"] = 0 } },
                ["meshes"] = new List<object?> { new Dictionary<string, object?> { ["primitives"] = _primitives } },
                ["materials"] = _materials,
                ["accessors"] = _accessors,
                ["bufferViews"] = _views,
                ["buffers"] = new List<object?> { new Dictionary<string, object?> { ["byteLength"] = bin.Length } },
            }));

            // The JSON is padded with spaces and the binary with zeros, each to a
            // four-byte boundary, as the format asks.
            var jsonLength = (json.Length + 3) & ~3;
            var binLength = (bin.Length + 3) & ~3;
            var binAt = 20 + jsonLength;
            var bytes = new byte[binAt + 8 + binLength];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x46546C67);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)bytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), (uint)jsonLength);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x4E4F534A);
            json.CopyTo(bytes, 20);
            bytes.AsSpan(20 + json.Length, jsonLength - json.Length).Fill(0x20);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(binAt), (uint)binLength);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(binAt + 4), 0x004E4942);
            bin.CopyTo(bytes, binAt + 8);
            return bytes;
        }

        private static double Linear(uint channel)
        {
            var c = (channel & 0xFF) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private Dictionary<string, object?> Indices(int count)
        {
            var order = Enumerable.Range(0, count);
            var small = count < 65536;
            var bytes = small
                ? MemoryMarshal.AsBytes<ushort>(order.Select(i => (ushort)i).ToArray()).ToArray()
                : MemoryMarshal.AsBytes<uint>(order.Select(i => (uint)i).ToArray()).ToArray();
            return new Dictionary<string, object?>
            {
                ["bufferView"] = View(bytes, 34963),
                ["componentType"] = small ? 5123 : 5125,
                ["count"] = count,
                ["type"] = "SCALAR",
            };
        }

        private int Floats(IReadOnlyList<float> values, int width, string type)
        {
            var low = Enumerable.Repeat(double.PositiveInfinity, width).ToArray();
            var high = Enumerable.Repeat(double.NegativeInfinity, width).ToArray();
            for (var i = 0; i < values.Count; i++)
            {
                low[i % width] = Math.Min(low[i % width], values[i]);
                high[i % width] = Math.Max(high[i % width], values[i]);
            }

            _accessors.Add(new Dictionary<string, object?>
            {
                ["bufferView"] = View(MemoryMarshal.AsBytes<float>(values.ToArray()).ToArray(), 34962),
                ["componentType"] = 5126,
                ["count"] = values.Count / width,
                ["type"] = type,

                // Asked for on positions, and harmless on the rest.
                ["min"] = low.Cast<object?>().ToList(),
                ["max"] = high.Cast<object?>().ToList(),
            });
            return _accessors.Count - 1;
        }

        private int View(byte[] bytes, int target)
        {
            var offset = _binary.Count;
            _binary.AddRange(bytes);

            // Every view starts on a four-byte boundary, which floats need.
            while (_binary.Count % 4 != 0)
            {
                _binary.Add(0);
            }

            _views.Add(new Dictionary<string, object?>
            {
                ["buffer"] = 0,
                ["byteOffset"] = offset,
                ["byteLength"] = bytes.Length,
                ["target"] = target,
            });
            return _views.Count - 1;
        }
    }
}

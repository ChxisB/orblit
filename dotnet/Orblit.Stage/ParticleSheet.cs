using System.Collections.ObjectModel;
using Orblit.Filament;
using Orblit.Mesh;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>
/// A grid of particles drawn as a sheet, for a cloth that something other than the renderer moves.
/// </summary>
/// <remarks>
/// The renderer has no way to take new vertex positions each frame, and a skin is the one thing it
/// takes new every frame. So the sheet has one vertex for every particle and one joint for every
/// vertex. Each joint is set to where its particle is, and the vertex goes with it.
///
/// It is made at the origin, in a layout of its own: columns along x, rows down −y, and the face
/// toward +z. <see cref="Poses"/> is in that same space, so a cloth that hangs somewhere else is drawn
/// by moving the object with a transform and handing <see cref="Poses"/> positions taken from that
/// point. That keeps the renderer's bounds, which come from the sheet at rest, near the cloth.
///
/// Particle <c>row * columns + column</c> is the vertex and the joint of the same number, which is how
/// a soft body numbers the particles of a cloth.
/// </remarks>
public sealed class ParticleSheet
{
    /// <summary>The most joints a renderer skin has. Past it a sheet would need a skin for each part.</summary>
    public const int MaxParticles = 256;

    private static readonly double[] DefaultColour = [0.8, 0.8, 0.8];

    public ParticleSheet(int columns, int rows, double spacing, IReadOnlyList<double>? colour = null)
    {
        if (columns < 2)
        {
            throw new ArgumentException("A sheet needs at least 2 by 2 particles", nameof(columns));
        }

        if (rows < 2)
        {
            throw new ArgumentException("A sheet needs at least 2 by 2 particles", nameof(rows));
        }

        if ((long)columns * rows > MaxParticles)
        {
            throw new ArgumentException(
                $"{columns} by {rows} is {(long)columns * rows} particles, and a skin holds {MaxParticles} joints",
                nameof(rows));
        }

        if (!(spacing > 0 && double.IsFinite(spacing)))
        {
            throw new ArgumentException("must be above zero", nameof(spacing));
        }

        Columns = columns;
        Rows = rows;
        Spacing = spacing;
        Colour = new ReadOnlyCollection<double>([.. colour ?? DefaultColour]);
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>The distance between neighbouring particles at rest, in metres.</summary>
    public double Spacing { get; }

    /// <summary>The colour of the cloth, linear, as red, green and blue.</summary>
    public IReadOnlyList<double> Colour { get; }

    public int Count => Columns * Rows;

    /// <summary>
    /// The sheet as a binary glTF, to hand to the renderer's resources. Both faces are drawn, since a
    /// flag has two sides.
    /// </summary>
    public byte[] ToGlb()
    {
        var buffer = new GltfBuffer();
        var position = buffer.AddPositions(Rest());
        var normal = buffer.AddFloats(Repeated([0f, 0f, 1f]), "VEC3", GltfTarget.ArrayBuffer);
        var uv = buffer.AddFloats(Uvs(), "VEC2", GltfTarget.ArrayBuffer);
        var joints = buffer.AddAccessor(
            buffer.AddView<byte>(JointIds(), GltfTarget.ArrayBuffer),
            GltfComponent.UnsignedByte,
            Count,
            "VEC4");
        var weights = buffer.AddFloats(Repeated([1f, 0f, 0f, 0f]), "VEC4", GltfTarget.ArrayBuffer);
        var bind = buffer.AddFloats(InverseBinds(), "MAT4");
        var indices = buffer.AddIndices(Triangles());

        var attributes = new OrderedDictionary<string, object?>
        {
            ["POSITION"] = (long)position,
            ["NORMAL"] = (long)normal,
            ["TEXCOORD_0"] = (long)uv,
            ["JOINTS_0"] = (long)joints,
            ["WEIGHTS_0"] = (long)weights,
        };
        var json = new OrderedDictionary<string, object?>
        {
            ["asset"] = new OrderedDictionary<string, object?> { ["version"] = "2.0", ["generator"] = "Orblit particle sheet" },
            ["scene"] = 0L,
            ["scenes"] = new List<object?> { new OrderedDictionary<string, object?> { ["nodes"] = Numbered(0, Count + 1) } },
            ["nodes"] = Nodes(),
            ["skins"] = new List<object?>
            {
                new OrderedDictionary<string, object?>
                {
                    ["joints"] = Numbered(1, Count),
                    ["inverseBindMatrices"] = (long)bind,
                },
            },
            ["meshes"] = new List<object?>
            {
                new OrderedDictionary<string, object?>
                {
                    ["primitives"] = new List<object?>
                    {
                        new OrderedDictionary<string, object?>
                        {
                            ["attributes"] = attributes,
                            ["indices"] = (long)indices,
                            ["material"] = 0L,
                        },
                    },
                },
            },
            ["materials"] = new List<object?> { Material() },
            ["accessors"] = buffer.Accessors,
            ["bufferViews"] = buffer.Views,
            ["buffers"] = buffer.Buffers,
        };
        return GltfFiles.GlbBytes(json, buffer.Bytes);
    }

    /// <summary>
    /// Every joint set to its particle, for a model's joints. <paramref name="at"/> gives where particle
    /// <c>i</c> is. A joint is moved to the particle and turned to follow the cloth there. Its rows are
    /// the way the columns run, the way the rows run, and the way the face points, found from the
    /// neighbours on each side. The turn does not change where the vertex is. It is what the light
    /// sees, because the sheet's normals are turned with the joints.
    /// </summary>
    public IReadOnlyList<JointPose> Poses(Func<int, Vector3> at)
    {
        var here = Enumerable.Range(0, Count).Select(at).ToList();
        return [.. Enumerable.Range(0, Count).Select(i => new JointPose(0, i, PoseOf(here, i)))];
    }

    private Matrix4 PoseOf(List<Vector3> here, int i)
    {
        var across = here[NeighbourOrSelf(i, 1, 0)] - here[NeighbourOrSelf(i, -1, 0)];
        var down = here[NeighbourOrSelf(i, 0, 1)] - here[NeighbourOrSelf(i, 0, -1)];

        var face = down.Cross(across);
        var at = here[i];

        // Where the neighbours are on a line, or on top of each other, there is no face to find. The
        // sheet's own turn is kept rather than a made-up one.
        if (face.Length2 < 1e-18)
        {
            return Matrix4.TranslationValues(at.X, at.Y, at.Z);
        }

        face.Normalize();
        var along = across.Normalized();
        var up = face.Cross(along);
        return Matrix4.FromList(
        [
            along.X, along.Y, along.Z, 0,
            up.X, up.Y, up.Z, 0,
            face.X, face.Y, face.Z, 0,
            at.X, at.Y, at.Z, 1,
        ]);
    }

    // The particle one step away along a column or a row, or the particle itself at the edge.
    private int NeighbourOrSelf(int i, int dx, int dy)
    {
        var column = (i % Columns) + dx;
        var row = (i / Columns) + dy;
        return column < 0 || column >= Columns || row < 0 || row >= Rows ? i : (row * Columns) + column;
    }

    private Vector3 RestAt(int i) => new((i % Columns) * Spacing, -(i / Columns) * Spacing, 0);

    private float[] Rest()
    {
        var output = new float[Count * 3];
        for (var i = 0; i < Count; i++)
        {
            var at = RestAt(i);
            output[i * 3] = (float)at.X;
            output[(i * 3) + 1] = (float)at.Y;
            output[(i * 3) + 2] = (float)at.Z;
        }

        return output;
    }

    private float[] Uvs()
    {
        var output = new float[Count * 2];
        for (var i = 0; i < Count; i++)
        {
            output[i * 2] = (float)((i % Columns) / (double)(Columns - 1));
            output[(i * 2) + 1] = (float)((i / Columns) / (double)(Rows - 1));
        }

        return output;
    }

    // Each vertex names its own joint, in the first of four slots.
    private byte[] JointIds()
    {
        var output = new byte[Count * 4];
        for (var i = 0; i < Count; i++)
        {
            output[i * 4] = (byte)i;
        }

        return output;
    }

    // The inverse of each joint's rest transform, which is only a move back from where the joint sits.
    private float[] InverseBinds()
    {
        var output = new float[Count * 16];
        for (var i = 0; i < Count; i++)
        {
            var at = RestAt(i);
            output[i * 16] = 1;
            output[(i * 16) + 5] = 1;
            output[(i * 16) + 10] = 1;
            output[(i * 16) + 12] = (float)-at.X;
            output[(i * 16) + 13] = (float)-at.Y;
            output[(i * 16) + 15] = 1;
        }

        return output;
    }

    // Two triangles to a cell, wound to face +z at rest.
    private List<int> Triangles()
    {
        var output = new List<int>();
        for (var row = 0; row < Rows - 1; row++)
        {
            for (var column = 0; column < Columns - 1; column++)
            {
                var a = (row * Columns) + column;
                var b = a + 1;
                var c = a + Columns;
                var d = c + 1;
                output.AddRange([a, c, b, b, c, d]);
            }
        }

        return output;
    }

    private float[] Repeated(float[] one) => [.. Enumerable.Range(0, Count).SelectMany(_ => one)];

    private List<object?> Nodes()
    {
        var nodes = new List<object?> { new OrderedDictionary<string, object?> { ["mesh"] = 0L, ["skin"] = 0L } };
        nodes.AddRange(Enumerable.Range(0, Count).Select(i =>
        {
            var at = RestAt(i);
            return (object?)new OrderedDictionary<string, object?>
            {
                ["translation"] = new List<object?> { at.X, at.Y, at.Z },
            };
        }));
        return nodes;
    }

    private OrderedDictionary<string, object?> Material() => new()
    {
        ["doubleSided"] = true,
        ["pbrMetallicRoughness"] = new OrderedDictionary<string, object?>
        {
            ["baseColorFactor"] = new List<object?>([.. Colour.Select(one => (object?)one), 1.0]),
            ["metallicFactor"] = 0.0,
            ["roughnessFactor"] = 0.85,
        },
    };

    // The numbers from..from+count as the integers a document names nodes by.
    private static List<object?> Numbered(int from, int count) =>
        [.. Enumerable.Range(from, count).Select(one => (object?)(long)one)];
}

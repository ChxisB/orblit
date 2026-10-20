namespace Orblit.Mesh;

/// <summary>
/// Per-vertex tangents. A normal map states which way a surface leans in the texture's own frame,
/// so reading one takes a direction across the surface that matches the way the texture was laid
/// on. That is the tangent. Without it every renderer guesses, and they guess differently.
/// </summary>
public static class MeshTangents
{
    /// <summary>
    /// Tangents for <paramref name="triangles"/>, four floats each. The fourth number is a sign,
    /// not a length: it says whether the third axis of the frame points one way or the other,
    /// which is how a mirrored UV island gets its bumps the right way up instead of inside out.
    /// Computed per triangle and averaged per vertex, the usual construction. It agrees with
    /// MikkTSpace wherever the triangles sharing a vertex agree, which is everywhere except a hard
    /// seam, and a hard seam has split the vertex already.
    /// </summary>
    public static float[] TangentsOf(Triangles triangles)
    {
        var count = triangles.Positions.Length / 3;
        var output = new float[count * 4];
        if (count == 0 || triangles.Uvs.Length != count * 2)
        {
            return output;
        }

        var along = new float[count * 3];
        var across = new float[count * 3];
        Accumulate(triangles, along, across);

        var hasNormals = triangles.Normals.Length == count * 3;
        for (var i = 0; i < count; i++)
        {
            var normal = hasNormals
                ? new Direction(triangles.Normals[i * 3], triangles.Normals[(i * 3) + 1], triangles.Normals[(i * 3) + 2])
                : new Direction(0.0, 1.0, 0.0);
            WriteFrame(output, i, normal, At(along, i), At(across, i));
        }

        return output;
    }

    private static void Accumulate(Triangles triangles, float[] along, float[] across)
    {
        var indices = triangles.Indices;
        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            var a = indices[i];
            var b = indices[i + 1];
            var c = indices[i + 2];

            var one = new Direction(Delta(triangles.Positions, 3, a, b, 0), Delta(triangles.Positions, 3, a, b, 1), Delta(triangles.Positions, 3, a, b, 2));
            var two = new Direction(Delta(triangles.Positions, 3, a, c, 0), Delta(triangles.Positions, 3, a, c, 1), Delta(triangles.Positions, 3, a, c, 2));
            var u1 = Delta(triangles.Uvs, 2, a, b, 0);
            var v1 = Delta(triangles.Uvs, 2, a, b, 1);
            var u2 = Delta(triangles.Uvs, 2, a, c, 0);
            var v2 = Delta(triangles.Uvs, 2, a, c, 1);

            // The area this triangle covers in the texture. A triangle that covers none of it,
            // every corner on the same spot or on one line, has no direction to give, and
            // dividing by it would hand every vertex it touches an infinity.
            var area = (u1 * v2) - (u2 * v1);
            if (area == 0 || !double.IsFinite(area))
            {
                continue;
            }

            var scale = 1 / area;
            var tangent = new Direction(
                ((v2 * one.X) - (v1 * two.X)) * scale,
                ((v2 * one.Y) - (v1 * two.Y)) * scale,
                ((v2 * one.Z) - (v1 * two.Z)) * scale);
            var bitangent = new Direction(
                ((u1 * two.X) - (u2 * one.X)) * scale,
                ((u1 * two.Y) - (u2 * one.Y)) * scale,
                ((u1 * two.Z) - (u2 * one.Z)) * scale);
            foreach (var at in new[] { a, b, c })
            {
                Add(along, at, tangent);
                Add(across, at, bitangent);
            }
        }
    }

    private static void WriteFrame(float[] output, int i, Direction normal, Direction along, Direction across)
    {
        // Square the frame up: take off however much of the tangent points along the normal, which
        // leaves the part that lies in the surface.
        var tangent = along.Minus(normal, normal.Dot(along));
        var length = tangent.Length;
        if (length < 1e-12 || !double.IsFinite(length))
        {
            // No usable direction: an unwrapped vertex, or one every triangle round it gave
            // nothing. Any direction lying in the surface will do, so take the normal crossed with
            // whichever axis it leans on least, which is the one choice that cannot collapse again.
            var sideways = Math.Abs(normal.X) < 0.9;
            tangent = normal.Cross(new Direction(sideways ? 1.0 : 0.0, sideways ? 0.0 : 1.0, 0.0));
            length = tangent.Length;
            if (length < 1e-12)
            {
                output[i * 4] = 1;
                output[(i * 4) + 3] = 1;
                return;
            }
        }

        output[i * 4] = (float)(tangent.X / length);
        output[(i * 4) + 1] = (float)(tangent.Y / length);
        output[(i * 4) + 2] = (float)(tangent.Z / length);

        // Which way round the third axis goes. The cross of the normal and the tangent either
        // agrees with the direction the texture runs across the surface or it is exactly opposed,
        // and that is the whole of the sign.
        output[(i * 4) + 3] = normal.Cross(tangent).Dot(across) < 0 ? -1 : 1;
    }

    private static double Delta(float[] values, int stride, uint from, uint to, int offset) =>
        (double)values[((long)to * stride) + offset] - values[((long)from * stride) + offset];

    private static Direction At(float[] values, int i) => new(values[i * 3], values[(i * 3) + 1], values[(i * 3) + 2]);

    private static void Add(float[] sums, uint at, Direction by)
    {
        var start = (long)at * 3;
        sums[start] = (float)(sums[start] + by.X);
        sums[start + 1] = (float)(sums[start + 1] + by.Y);
        sums[start + 2] = (float)(sums[start + 2] + by.Z);
    }

    /// <summary>Three numbers read left to right in the order the Dart arithmetic writes them.</summary>
    private readonly record struct Direction(double X, double Y, double Z)
    {
        internal double Length => Math.Sqrt(Dot(this));

        internal double Dot(Direction other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

        internal Direction Cross(Direction other) => new(
            (Y * other.Z) - (Z * other.Y),
            (Z * other.X) - (X * other.Z),
            (X * other.Y) - (Y * other.X));

        /// <summary>This less <paramref name="other"/> scaled by <paramref name="factor"/>.</summary>
        internal Direction Minus(Direction other, double factor) =>
            new(X - (other.X * factor), Y - (other.Y * factor), Z - (other.Z * factor));
    }
}

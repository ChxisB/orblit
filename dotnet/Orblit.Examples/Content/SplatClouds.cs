using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// The two clouds the splats example generates, as compact splat records.
/// Each is seeded, so a count gives the same cloud every run.
/// </summary>
internal static class SplatClouds
{
    /// <summary>
    /// A ring of <paramref name="count"/> flat discs on a torus.
    /// </summary>
    /// <remarks>
    /// Discs rather than balls, oriented along the surface, which is what a
    /// trained capture's splats mostly are: flattened onto whatever surface they
    /// were fitted to. Half transparent, so both sides of the ring show where
    /// they overlap, and generated in order round the ring, so that drawing
    /// them unsorted is visibly wrong rather than accidentally right.
    /// </remarks>
    public static byte[] Ring(int count)
    {
        var random = new Random(7);
        var records = new Records(count);
        for (var i = 0; i < count; i++)
        {
            // In order round the ring, jittered.
            var u = (i + random.NextDouble()) / count * 2 * Math.PI;
            var v = random.NextDouble() * 2 * Math.PI;
            var (at, frame) = OnTheRing(u, v);

            // A few centimetres across and a few millimetres thick.
            var size = 0.018 + (random.NextDouble() * 0.02);
            var scale = new Vector3(size, size * (0.6 + (random.NextDouble() * 0.4)), 0.003);

            // Stripes round the ring and bands round the tube, in two colours
            // that are easy to tell apart when one is seen through the other.
            var stripe = Math.Sin(u * 9) * Math.Sin((v * 2) + 0.4) > 0;
            var shade = 0.85 + (random.NextDouble() * 0.15);
            var light = (0.6 + (0.4 * (0.5 + (0.5 * Math.Sin(v))))) * shade;
            var colour = stripe ? new Vector3(0.95, 0.55, 0.18) : new Vector3(0.12, 0.70, 0.85);
            records.Put(new Disc(at, frame, scale, colour * light, 0.55));
        }

        return records.Pack();
    }

    /// <summary>A tree in a clearing, as a cloud of <paramref name="count"/> splats.</summary>
    /// <remarks>
    /// Nothing is downloaded for this and nothing was photographed: it is
    /// generated, like <see cref="Ring"/> is. What it borrows from a real
    /// capture is the way one is put together, because that is what makes a
    /// capture look like one. Every splat is a flattened ellipsoid lying along
    /// the surface it belongs to. Nothing has an edge. A leaf is not a leaf but
    /// four or five soft discs, and the trunk is not a cylinder but a few
    /// thousand discs lying where a cylinder was. And the light is in the
    /// colours rather than on them: a capture is radiance already, so the sun
    /// here is arithmetic done once per splat while this is built, and the
    /// scene's own light never touches it.
    /// </remarks>
    public static byte[] Garden(int count) => new Garden(count).Grown();

    /// <summary>A point on a torus tilted towards the camera, and the frame of the surface there.</summary>
    private static (Vector3 At, Matrix3 Frame) OnTheRing(double u, double v)
    {
        const double Major = 1.6;
        const double Minor = 0.55;
        var (cu, su, cv, sv) = (Math.Cos(u), Math.Sin(u), Math.Cos(v), Math.Sin(v));

        // On an untilted torus lying in the xz plane, then tilted about x so
        // the camera sees into the ring and across it at once, and lifted off
        // the floor.
        var ring = Major + (Minor * cv);
        var at = Tilted(new Vector3(ring * cu, Minor * sv, ring * su));
        at.Y += 1.0;
        var along = Tilted(new Vector3(-su, 0, cu));
        var around = Tilted(new Vector3(-sv * cu, cv, -sv * su));
        var normal = Tilted(new Vector3(cv * cu, sv, cv * su));

        // Along, around and the normal make a left-handed frame, and a
        // quaternion can only hold a right-handed one. Turning around the
        // other way fixes the hand and keeps each disc on the surface.
        return (at, Columns(along, -around, normal));
    }

    private static Vector3 Tilted(Vector3 a)
    {
        const double Tilt = 0.5;
        var (ct, st) = (Math.Cos(Tilt), Math.Sin(Tilt));
        return new Vector3(a.X, (a.Y * ct) - (a.Z * st), (a.Y * st) + (a.Z * ct));
    }

    /// <summary>A rotation whose columns are <paramref name="x"/>, <paramref name="y"/> and <paramref name="z"/>.</summary>
    internal static Matrix3 Columns(Vector3 x, Vector3 y, Vector3 z)
    {
        var frame = new Matrix3();
        Vector3[] columns = [x, y, z];
        for (var i = 0; i < 9; i++)
        {
            frame[i] = columns[i / 3][i % 3];
        }

        return frame;
    }
}

/// <summary>
/// One splat: a disc at <paramref name="At"/> whose own axes are the columns of
/// <paramref name="Frame"/>, its standard deviation along each in
/// <paramref name="Size"/>, with a colour and a peak opacity.
/// </summary>
internal sealed record Disc(Vector3 At, Matrix3 Frame, Vector3 Size, Vector3 Colour, double Alpha);

/// <summary>Splat records written one after another, for <see cref="SplatCloud.Pack"/>.</summary>
internal sealed class Records
{
    private readonly float[] _positions;
    private readonly float[] _scales;
    private readonly float[] _colours;
    private readonly float[] _rotations;
    private int _at;

    /// <summary>
    /// Room for <paramref name="count"/>. A record not written packs as the
    /// identity, because <see cref="SplatCloud.Pack"/> turns a zero quaternion into it.
    /// </summary>
    public Records(int count)
    {
        _positions = new float[count * 3];
        _scales = new float[count * 3];
        _colours = new float[count * 4];
        _rotations = new float[count * 4];
    }

    /// <summary>Writes <paramref name="disc"/> as the next record, or nothing once every record is written.</summary>
    public void Put(Disc disc)
    {
        if (_at * 3 >= _positions.Length)
        {
            return;
        }

        var q = Quaternion.FromRotation(disc.Frame);
        Write(_positions, _at * 3, disc.At.X, disc.At.Y, disc.At.Z);
        Write(_scales, _at * 3, disc.Size.X, disc.Size.Y, disc.Size.Z);
        Write(_rotations, _at * 4, q.W, q.X, q.Y, q.Z);
        Write(_colours, _at * 4, disc.Colour.X, disc.Colour.Y, disc.Colour.Z, disc.Alpha);
        _at++;
    }

    public byte[] Pack() => SplatCloud.Pack(_positions, _scales, _colours, _rotations);

    private static void Write(float[] into, int from, params ReadOnlySpan<double> values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            into[from + i] = (float)values[i];
        }
    }
}

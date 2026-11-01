using System.Runtime.InteropServices;
using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// A cloud of 3D Gaussians: a captured place, or anything generated to look like one.
/// </summary>
/// <remarks>
/// <para>
/// 3D Gaussian splatting (Kerbl, Kopanas, Leimkuehler and Drettakis, SIGGRAPH 2023) represents a
/// scene as millions of small, soft, coloured ellipsoids rather than as surfaces. Each is drawn as
/// an ellipse on screen, sized from how its ellipsoid projects, faded by a Gaussian and blended
/// over what is behind it. So they have to be drawn back to front, and the renderer re-sorts them
/// whenever the camera moves, off the render thread, leaving out the ones the camera cannot see.
/// </para>
/// <para>
/// Splats are drawn after the solid scene, test against its depth and do not write any: a wall in
/// front of a cloud hides it, and a cloud never hides a wall. They are not lit, because a capture
/// has its lighting baked into its colours, and they neither cast nor receive shadows.
/// </para>
/// <para>
/// Two ways to say what the cloud is: a <see cref="Path"/> to a <c>.ply</c> as the reference
/// trainer writes it or a compact <c>.splat</c>, read on the native side; or <see cref="Data"/> in
/// the compact layout, for a cloud made in code. <see cref="Pack"/> builds that layout from plain
/// arrays.
/// </para>
/// </remarks>
public sealed class SplatCloud
{
    /// <summary>Where the limit starts in <see cref="Flags"/>. Must match kSplatFlagLimitShift.</summary>
    public const int LimitShift = 8;

    /// <summary>The largest limit the flags have room for: twenty-four bits.</summary>
    public const int MaxLimit = 0xFFFFFF;

    /// <summary>
    /// Floats per cloud in the scene message: the transform, column major, then the opacity and the
    /// brightness. Must match kSplatParams in OrblitSplats.h.
    /// </summary>
    public const int Stride = 18;

    /// <summary>Bytes per splat in the compact layout. Must match kSplatRecordBytes.</summary>
    public const int RecordBytes = 32;

    private readonly int _harmonics = 2;
    private readonly int? _limit;

    /// <summary>
    /// A cloud from a file or from memory, never both and never neither.
    /// </summary>
    /// <param name="key">What this cloud is, across frames. The renderer keeps its textures and its sorter against it.</param>
    /// <param name="path">
    /// A capture to read: a <c>.ply</c> as the reference trainer writes one, a compact <c>.splat</c>,
    /// an <c>.spz</c>, or an <c>.osplat</c> cooked from any of those.
    /// </param>
    /// <param name="data">Splats in the compact 32-byte layout. See <see cref="Pack"/>.</param>
    public SplatCloud(int key, string? path = null, byte[]? data = null)
    {
        if ((path is null) == (data is null))
        {
            throw new ArgumentException("A cloud comes from a file or from memory, not both or neither.", nameof(path));
        }

        if (data is not null && data.Length % RecordBytes != 0)
        {
            throw new ArgumentException($"In-memory splats are whole {RecordBytes}-byte records.", nameof(data));
        }

        Key = key;
        Path = path;
        Data = data;
    }

    public int Key { get; }

    /// <summary>
    /// Read once, and again only when the path, <see cref="Harmonics"/> or <see cref="Limit"/>
    /// changes. An <c>.osplat</c> is the cloud exactly as the renderer holds it, so opening one is a
    /// read and four copies rather than a parse, an exponential and a quaternion a splat.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Sent only when <see cref="Revision"/> has moved since the renderer last took it, the same
    /// bargain a population makes: a few hundred thousand splats is megabytes, and a cloud that is
    /// only being looked at sends none of it.
    /// </summary>
    public byte[]? Data { get; }

    /// <summary>
    /// Where the cloud is, as the capture's own coordinates to the world's. Captures come out of
    /// structure from motion in whatever orientation the first photograph had, which is very often
    /// upside down, and this is where that is put right. Scale reaches every splat's size as well
    /// as its position.
    /// </summary>
    public Matrix4 Transform { get; init; } = Matrix4.Identity();

    /// <summary>Multiplies every splat's own opacity.</summary>
    public double Opacity { get; init; } = 1;

    /// <summary>Multiplies every splat's colour, in linear light.</summary>
    public double Brightness { get; init; } = 1;

    /// <summary>
    /// Whether the splats are sorted back to front. On, always, for a picture: this exists so that
    /// what the sort is worth can be measured, and a cloud that is not sorted is drawn whole, every
    /// splat, in the order given.
    /// </summary>
    public bool Sorted { get; init; } = true;

    /// <summary>
    /// The degree of the spherical harmonics to read, 0 to 3. Each degree is another band of
    /// view-dependent colour, and costs sixteen bytes a splat. Only a <c>.ply</c> carries any: the
    /// compact record has no room for them, so a cloud from <see cref="Data"/> or from a
    /// <c>.splat</c> is flat whatever this says.
    /// </summary>
    public int Harmonics
    {
        get => _harmonics;
        init => _harmonics = value is >= 0 and <= 3
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "A capture carries spherical harmonics of degree 0, 1, 2 or 3.");
    }

    /// <summary>
    /// The most splats to draw, or null for every one. A cloud is ranked as it is read, by how
    /// opaque each splat is and how much of the screen it can cover, and a limit keeps the ones
    /// that add most. Applied as the cloud is read, so a new limit reads a file again, and a cloud
    /// from <see cref="Data"/> takes it with its next <see cref="Revision"/>.
    /// </summary>
    public int? Limit
    {
        get => _limit;
        init => _limit = value is null or (> 0 and <= MaxLimit)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"A limit is at least one splat and at most {MaxLimit}.");
    }

    /// <summary>
    /// Whether to sort on sixteen bits of depth rather than thirty-two. Half the passes, for splats
    /// within a 65 536th of the visible splats' depth of one another coming out in either order.
    /// </summary>
    public bool CoarseOrder { get; init; }

    /// <summary>Bumped by whoever writes into <see cref="Data"/>.</summary>
    public int Revision { get; init; }

    /// <summary>How many there are, for a cloud held in memory. Zero for a file, whose size the renderer finds out when it reads it.</summary>
    public int Count => Data is null ? 0 : Data.Length / RecordBytes;

    /// <summary>
    /// Bit flags in the order the renderer reads them: whether to sort; the spherical harmonic
    /// degree in the two bits above that; whether the sort is coarse in bit three; and the limit
    /// from bit eight up, nought for none. A limit near <see cref="MaxLimit"/> sets the top bit, so
    /// the flags of such a cloud are a negative 32-bit integer on the wire. The renderer reads them
    /// unsigned.
    /// </summary>
    public int Flags =>
        (Sorted ? 1 : 0)
        | (Harmonics << 1)
        | (CoarseOrder ? 8 : 0)
        | ((Limit ?? 0) << LimitShift);

    /// <summary>Writes this cloud's <see cref="Stride"/> floats at <paramref name="at"/>.</summary>
    internal void PackParams(float[] into, int at)
    {
        for (var i = 0; i < 16; i++)
        {
            into[at + i] = (float)Transform[i];
        }

        into[at + 16] = (float)Opacity;
        into[at + 17] = (float)Brightness;
    }

    /// <summary>
    /// The compact layout, from plain arrays of splats. <paramref name="positions"/> and
    /// <paramref name="scales"/> are three floats each, and scales are standard deviations in
    /// metres, not the logs a <c>.ply</c> stores. <paramref name="colours"/> is four floats each:
    /// red, green and blue as display values from nought to one, and alpha as the splat's peak
    /// opacity. <paramref name="rotations"/> is four each, a quaternion as (w, x, y, z), or null for
    /// none. Colour and rotation are kept to a byte a channel, which is what a <c>.splat</c> file
    /// does and is below what anybody can see in a soft blob.
    /// </summary>
    public static byte[] Pack(float[] positions, float[] scales, float[] colours, float[]? rotations = null)
    {
        var count = positions.Length / 3;
        if (scales.Length != count * 3)
        {
            throw new ArgumentException("Three scales a splat.", nameof(scales));
        }

        if (colours.Length != count * 4)
        {
            throw new ArgumentException("Four colour channels a splat.", nameof(colours));
        }

        if (rotations is not null && rotations.Length != count * 4)
        {
            throw new ArgumentException("Four quaternion components a splat.", nameof(rotations));
        }

        var bytes = new byte[count * RecordBytes];
        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan());
        for (var i = 0; i < count; i++)
        {
            var f = i * (RecordBytes / 4);
            positions.AsSpan(i * 3, 3).CopyTo(floats[f..]);
            scales.AsSpan(i * 3, 3).CopyTo(floats[(f + 3)..]);

            var b = (i * RecordBytes) + 24;
            for (var c = 0; c < 4; c++)
            {
                bytes[b + c] = (byte)Math.Round(Math.Clamp((double)colours[(i * 4) + c], 0.0, 1.0) * 255, MidpointRounding.AwayFromZero);
            }

            WriteRotation(rotations, i, bytes.AsSpan(b + 4, 4));
        }

        return bytes;
    }

    /// <summary>The splat's rotation as four bytes, normalised first. The identity when there is none.</summary>
    private static void WriteRotation(float[]? rotations, int index, Span<byte> into)
    {
        double w = 1, x = 0, y = 0, z = 0;
        if (rotations is not null)
        {
            w = rotations[index * 4];
            x = rotations[(index * 4) + 1];
            y = rotations[(index * 4) + 2];
            z = rotations[(index * 4) + 3];
            var length = Math.Sqrt((w * w) + (x * x) + (y * y) + (z * z));
            if (length > 0)
            {
                w /= length;
                x /= length;
                y /= length;
                z /= length;
            }
            else
            {
                w = 1;
            }
        }

        into[0] = Quantise(w);
        into[1] = Quantise(x);
        into[2] = Quantise(y);
        into[3] = Quantise(z);
    }

    /// <summary>
    /// A unit component to a byte, as (v * 128 + 128), which is the <c>.splat</c> convention. One is
    /// 256 and so saturates at 255, a hair under one, which the renderer's normalisation takes back out.
    /// </summary>
    private static byte Quantise(double value) =>
        (byte)Math.Clamp((int)Math.Round((value * 128) + 128, MidpointRounding.AwayFromZero), 0, 255);
}

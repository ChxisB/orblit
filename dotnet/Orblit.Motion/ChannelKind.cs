using System.Diagnostics.CodeAnalysis;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// What a channel carries: how its values mix, and how they are written.
/// </summary>
/// <remarks>
/// Four of them, and the channel says which rather than the value, because a file's
/// <c>[0, 0, 0, 1]</c> is a rotation or a colour only by what it was written as. A rotation is kept as
/// a rotation, four numbers in glTF's order, so it turns the short way between keys instead of
/// spinning about three axes one after another. A vector on a transform's <c>rotation</c> is also
/// allowed, and is three angles in degrees, because that is the only way to say "spin twice": a
/// rotation has no idea how many times it has been round.
/// </remarks>
public abstract class ChannelKind
{
    /// <summary>A single number: an intensity, a field of view, a weight.</summary>
    public static readonly ChannelKind<double> Number = new NumberKind();

    /// <summary>Three numbers: a position, a scale, or angles in degrees.</summary>
    public static readonly ChannelKind<Vector3> Vector = new VectorKind();

    /// <summary>A rotation, which turns the short way between keys.</summary>
    public static readonly ChannelKind<Quaternion> Rotation = new RotationKind();

    /// <summary>On or off, and nothing in between.</summary>
    public static readonly ChannelKind<bool> Flag = new FlagKind();

    private protected ChannelKind(string name) => Name = name;

    public static IReadOnlyList<ChannelKind> All { get; } = [Number, Vector, Rotation, Flag];

    /// <summary>What a file calls it.</summary>
    public string Name { get; }

    /// <summary>The kind called <paramref name="name"/> in a file, or null when it is not one.</summary>
    public static ChannelKind? Named(object? name) => All.FirstOrDefault(kind => kind.Name.Equals(name));

    /// <summary>A channel of this kind, with keys typed as anything. Throws if a value is not what the kind carries.</summary>
    internal abstract ClipChannel Make(ChannelAddress address, IEnumerable<Key<object>> keys);

    /// <summary>The channel a file's <paramref name="raw"/> describes, or null with a note when it has no key that can be read.</summary>
    internal abstract ClipChannel? Read(JsonMap raw, ChannelAddress address, List<string> problems);

    /// <summary>
    /// Exactly <paramref name="count"/> finite numbers, or null.
    /// </summary>
    /// <remarks>
    /// All or nothing, unlike a scene's vectors, which fall back per component. A key is one value at one
    /// moment, and a key with a made-up component is a value nobody chose. Dropping the key leaves the
    /// curve running through the ones either side of it, which is nearer what was meant.
    /// </remarks>
    private protected static double[]? Numbers(object? raw, int count)
    {
        if (raw is not JsonList list || list.Count != count)
        {
            return null;
        }

        var numbers = new double[count];
        for (var i = 0; i < count; i++)
        {
            if (!Finite(list[i], out numbers[i]))
            {
                return null;
            }
        }

        return numbers;
    }

    private protected static bool Finite(object? raw, out double number)
    {
        number = raw switch
        {
            double value => value,
            long value => value,
            _ => double.NaN,
        };
        return double.IsFinite(number);
    }

    private sealed class NumberKind() : ChannelKind<double>("number")
    {
        public override IMixer<double> Mixer => Mixers.OfDouble;

        public override bool TryRead(object? raw, out double value) => Finite(raw, out value);

        public override object Write(double value) => value;
    }

    private sealed class VectorKind() : ChannelKind<Vector3>("vector")
    {
        public override IMixer<Vector3> Mixer => Mixers.OfVector3;

        public override bool TryRead(object? raw, [MaybeNullWhen(false)] out Vector3 value)
        {
            var numbers = Numbers(raw, 3);
            value = numbers is null ? null : new Vector3(numbers[0], numbers[1], numbers[2]);
            return value is not null;
        }

        public override object Write(Vector3 value) => new List<object?> { value.X, value.Y, value.Z };
    }

    private sealed class RotationKind() : ChannelKind<Quaternion>("rotation")
    {
        public override IMixer<Quaternion> Mixer => Mixers.OfQuaternion;

        /// <summary>
        /// Four numbers, x, y, z and then w, as glTF writes them.
        /// </summary>
        /// <remarks>
        /// Made unit length on the way in: a hand-edited file is rarely exactly one long, and a rotation that
        /// is not one also scales. One already as near as a float gets is left as written, so reading a file
        /// and writing it back changes nothing. Four noughts are no rotation at all rather than the rotation
        /// that does nothing, so they are refused rather than guessed at.
        /// </remarks>
        public override bool TryRead(object? raw, [MaybeNullWhen(false)] out Quaternion value)
        {
            value = null;
            if (Numbers(raw, 4) is not { } n)
            {
                return false;
            }

            var length = Math.Sqrt(n.Aggregate(0.0, static (sum, one) => sum + (one * one)));
            if (length < 1e-9)
            {
                return false;
            }

            var scale = Math.Abs(length - 1) < 1e-6 ? 1.0 : 1 / length;
            value = new Quaternion(n[0] * scale, n[1] * scale, n[2] * scale, n[3] * scale);
            return true;
        }

        public override bool TryReadSlope(object? raw, [MaybeNullWhen(false)] out Quaternion value)
        {
            var numbers = Numbers(raw, 4);
            value = numbers is null ? null : new Quaternion(numbers[0], numbers[1], numbers[2], numbers[3]);
            return value is not null;
        }

        public override object Write(Quaternion value) => new List<object?> { value.X, value.Y, value.Z, value.W };
    }

    private sealed class FlagKind() : ChannelKind<bool>("flag")
    {
        public override IMixer<bool> Mixer => Mixers.OfBool;

        public override bool TryRead(object? raw, out bool value)
        {
            value = raw is true;
            return raw is bool;
        }

        public override object Write(bool value) => value;
    }
}

/// <summary>A <see cref="ChannelKind"/> that knows what it carries.</summary>
public abstract class ChannelKind<T> : ChannelKind
    where T : notnull
{
    private protected ChannelKind(string name)
        : base(name)
    {
    }

    public abstract IMixer<T> Mixer { get; }

    /// <summary>A value out of decoded JSON, or false when what is there is not one.</summary>
    public abstract bool TryRead(object? raw, [MaybeNullWhen(false)] out T value);

    /// <summary>
    /// A slope out of decoded JSON: how fast a value is changing, which is read like a value but is not one.
    /// The same as <see cref="TryRead"/> for all but a rotation, whose slope is four numbers of any length.
    /// </summary>
    public virtual bool TryReadSlope(object? raw, [MaybeNullWhen(false)] out T value) => TryRead(raw, out value);

    /// <summary>A value as decoded JSON, the way <see cref="TryRead"/> reads it back.</summary>
    public abstract object Write(T value);

    internal override ClipChannel Make(ChannelAddress address, IEnumerable<Key<object>> keys) =>
        new ClipChannel<T>(address.Target, address.Property, this, ClipChannel.Typed<T>(keys), address.Bone);

    internal override ClipChannel? Read(JsonMap raw, ChannelAddress address, List<string> problems) =>
        ClipChannel<T>.Read(raw, address, this, problems);
}

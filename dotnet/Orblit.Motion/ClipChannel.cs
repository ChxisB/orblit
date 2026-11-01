using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>What a channel moves, named three ways at once. See <see cref="ClipChannel"/>.</summary>
internal readonly record struct ChannelAddress(string Target, string? Bone, string Property)
{
    /// <summary>
    /// The address as a message says it: the target when there is one, then the bone, then the property.
    /// Each is optional because the message is also for an address that is not yet whole.
    /// </summary>
    internal static string Describe(string target, string? bone, string? property)
    {
        var parts = new List<string>();
        if (target.Length > 0)
        {
            parts.Add(target);
        }

        parts.AddRange(new[] { bone, property }.OfType<string>());
        return string.Join(" › ", parts);
    }

    internal string Where => Describe(Target, Bone, Property);
}

/// <summary>
/// One thing a clip moves, and the keys it moves it through.
/// </summary>
/// <remarks>
/// <para>What it moves is named three ways at once:</para>
/// <list type="bullet">
/// <item><see cref="Target"/> is an entity, by its path from whoever plays the clip. Empty is that entity
/// itself. The path is a scene's id with the player's own part taken off, so the same clip plays on every
/// lamp of a street, and a reparent inside the lamp does not orphan it.</item>
/// <item><see cref="Bone"/> is one of the target's bones, for a model with a skeleton, or null for the
/// entity itself.</item>
/// <item><see cref="Property"/> is what about it: <c>transform.position</c>, a component and one of its
/// fields, on an entity; <c>position</c>, <c>rotation</c> or <c>scale</c> on a bone.</item>
/// </list>
/// <para>A bone is not an entity because a skeleton is the model's, not the scene's: a character from a
/// file has sixty joints and the outliner should not.</para>
/// <para>
/// This is the channel as code that does not know its kind holds it. <see cref="ClipChannel{T}"/> is the
/// one that does.
/// </para>
/// </remarks>
public abstract class ClipChannel
{
    private protected ClipChannel(string target, string? bone, string property)
    {
        Target = target;
        Bone = bone;
        Property = property;
    }

    /// <summary>Bone properties, and the kinds they carry.</summary>
    public static IReadOnlyDictionary<string, ChannelKind> BoneProperties { get; } = new Dictionary<string, ChannelKind>
    {
        ["position"] = ChannelKind.Vector,
        ["rotation"] = ChannelKind.Rotation,
        ["scale"] = ChannelKind.Vector,
    };

    public string Target { get; }

    public string? Bone { get; }

    public string Property { get; }

    public abstract ChannelKind Kind { get; }

    /// <summary>The time of the first key.</summary>
    public double Start { get; private protected init; }

    /// <summary>The time of the last key.</summary>
    public double End { get; private protected init; }

    /// <summary>
    /// The component an entity property belongs to, <c>transform</c> of <c>transform.position</c>, or null
    /// for a bone's.
    /// </summary>
    public string? Component
    {
        get
        {
            if (Bone is not null)
            {
                return null;
            }

            var dot = Property.IndexOf('.');
            return dot <= 0 ? null : Property[..dot];
        }
    }

    /// <summary>The field within <see cref="Component"/>, or the whole property on a bone.</summary>
    public string Field => Bone is not null ? Property : Property[(Property.IndexOf('.') + 1)..];

    /// <summary>
    /// A channel of <paramref name="kind"/>, with keys typed as anything.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="Rekeyed"/>: made from a kind chosen at run time, the way a channel keyed
    /// from an editor's field is. Throws <see cref="InvalidCastException"/> if a value is not what the kind
    /// carries.
    /// </remarks>
    // F2: five parameters, the last called by name where it is wanted.
    public static ClipChannel OfKind(
        string target,
        string property,
        ChannelKind kind,
        IEnumerable<Key<object>> keys,
        string? bone = null) =>
        kind.Make(new ChannelAddress(target, bone, property), keys);

    /// <summary>The keys with their values boxed, for code that holds the channel without knowing its kind.</summary>
    public abstract IReadOnlyList<Key<object>> BoxedKeys { get; }

    /// <summary>The value at <paramref name="at"/> seconds into the clip.</summary>
    public abstract object ValueAt(double at);

    /// <summary>Whether <paramref name="other"/> moves the same thing. A clip has one channel for each.</summary>
    public bool SameAddress(ClipChannel other) =>
        Target == other.Target && Bone == other.Bone && Property == other.Property;

    /// <summary>
    /// <paramref name="keys"/> as this channel's, whatever they are typed as on the way in.
    /// </summary>
    /// <remarks>
    /// For a caller holding the channel as a <see cref="ClipChannel"/>, which is how a list of channels of
    /// every kind is held: <see cref="ClipChannel{T}.WithKeys"/> wants keys of what the channel really
    /// carries, and that caller has no name for it. Throws <see cref="InvalidCastException"/> if a value is
    /// not one.
    /// </remarks>
    public abstract ClipChannel Rekeyed(IEnumerable<Key<object>> keys);

    public abstract OrderedDictionary<string, object?> ToJson();

    /// <summary>A channel out of a file, or null with a note when it cannot be one.</summary>
    public static ClipChannel? FromJson(object? raw, List<string> problems)
    {
        if (raw is not JsonMap map || Name(map, problems) is not { } named)
        {
            return null;
        }

        return named.Kind.Read(map, named.Address, problems);
    }

    internal static List<Key<T>> Typed<T>(IEnumerable<Key<object>> keys)
        where T : notnull =>
        [.. keys.Select(key => new Key<T>(
            key.At,
            (T)key.Value,
            key.Hold,
            key.Shape,
            SlopeAs<T>(key.SlopeIn),
            SlopeAs<T>(key.SlopeOut)))];

    private protected static Key<object> Box<TValue>(Key<TValue> key)
        where TValue : notnull => new(
        key.At,
        key.Value,
        key.Hold,
        key.Shape,
        BoxSlope(key.SlopeIn),
        BoxSlope(key.SlopeOut));

    // A method rather than a conditional: a null beside a Slope<object> converts through Slope's implicit
    // operator from any value, which turns the null into a slope of nothing.
    private static Slope<object>? BoxSlope<TValue>(Slope<TValue>? slope)
        where TValue : notnull
    {
        if (slope is not { } one)
        {
            return null;
        }

        return new Slope<object>(one.Value);
    }

    private static Slope<T>? SlopeAs<T>(Slope<object>? slope) => slope is { } one ? new Slope<T>((T)one.Value) : null;

    private static (ChannelAddress Address, ChannelKind Kind)? Name(JsonMap map, List<string> problems)
    {
        var target = Values.Text(map, "target") ?? string.Empty;
        var bone = Values.Text(map, "bone");
        var property = Values.Text(map, "property");
        var kind = ChannelKind.Named(map.GetValueOrDefault("kind"));
        var place = ChannelAddress.Describe(target, bone, property);

        if (string.IsNullOrEmpty(property))
        {
            problems.Add($"A channel on \"{place}\" says nothing about what it moves.");
            return null;
        }

        if (kind is null)
        {
            problems.Add($"The channel \"{place}\" has a kind this Orblit does not know.");
            return null;
        }

        if (Misplaced(place, bone, property, kind) is { } message)
        {
            problems.Add(message);
            return null;
        }

        return (new ChannelAddress(target, bone, property), kind);
    }

    /// <summary>Why a property cannot be on a bone, or on an entity, or null when it can.</summary>
    private static string? Misplaced(string place, string? bone, string property, ChannelKind kind)
    {
        if (bone is not null && BoneProperties.GetValueOrDefault(property) != kind)
        {
            return $"The channel \"{place}\" is not something a bone has; bones move by position, rotation and scale.";
        }

        return bone is null && property.IndexOf('.') <= 0
            ? $"The channel \"{place}\" names no component; a property on an entity is written as component.field."
            : null;
    }
}

/// <summary>A <see cref="ClipChannel"/> that knows what it carries.</summary>
public sealed class ClipChannel<T> : ClipChannel
    where T : notnull
{
    private readonly ChannelKind<T> _kind;
    private IReadOnlyList<Key<object>>? _boxed;

    // F2: five parameters, called by name, as the channel's address is spelled in a file.
    public ClipChannel(string target, string property, ChannelKind<T> kind, IReadOnlyList<Key<T>> keys, string? bone = null)
        : base(target, bone, property)
    {
        if (keys.Count == 0)
        {
            throw new ArgumentException("A channel needs a key.", nameof(keys));
        }

        _kind = kind;
        Keys = InOrder(keys).AsReadOnly();
        Curve = new Channel<T>(Keys, kind.Mixer);
        Start = Keys[0].At;
        End = Keys[^1].At;
    }

    public override ChannelKind<T> Kind => _kind;

    /// <summary>
    /// In order of time. Two keys at one moment are a cut, and the later one in the list is what the channel
    /// says from then on.
    /// </summary>
    public IReadOnlyList<Key<T>> Keys { get; }

    /// <summary>The curve through <see cref="Keys"/>, for anything that wants the slopes too.</summary>
    public Channel<T> Curve { get; }

    public override IReadOnlyList<Key<object>> BoxedKeys => _boxed ??= [.. Keys.Select(Box)];

    public override object ValueAt(double at) => Curve.At(at);

    public ClipChannel<T> WithKeys(IReadOnlyList<Key<T>> keys) => new(Target, Property, _kind, keys, Bone);

    public override ClipChannel Rekeyed(IEnumerable<Key<object>> keys) => WithKeys(Typed<T>(keys));

    public override OrderedDictionary<string, object?> ToJson()
    {
        var usual = UsualHold();
        return Values.Pruned(new OrderedDictionary<string, object?>
        {
            ["target"] = Target,
            ["bone"] = Bone,
            ["property"] = Property,
            ["kind"] = _kind.Name,
            ["hold"] = usual == Hold.Smooth ? null : FileNames.Of(usual),
            ["keys"] = Keys.Select(key => (object?)KeyToJson(key, usual)).ToList(),
        });
    }

    /// <summary>
    /// The channel a file's <paramref name="raw"/> describes, or null with a note when none of its keys can
    /// be read.
    /// </summary>
    internal static ClipChannel<T>? Read(JsonMap raw, ChannelAddress address, ChannelKind<T> kind, List<string> problems)
    {
        var usual = FileNames.Named<Hold>(raw.GetValueOrDefault("hold")) ?? Hold.Smooth;
        var keys = new List<Key<T>>();
        var dropped = 0;
        foreach (var entry in raw.GetValueOrDefault("keys") as JsonList ?? Array.Empty<object?>())
        {
            if (ReadKey(entry, kind, usual) is { } key)
            {
                keys.Add(key);
            }
            else
            {
                dropped++;
            }
        }

        if (dropped > 0)
        {
            var one = dropped == 1;
            problems.Add($"{dropped} key{(one ? string.Empty : "s")} on \"{address.Where}\" could not be read and {(one ? "was" : "were")} left out.");
        }

        if (keys.Count == 0)
        {
            problems.Add($"The channel \"{address.Where}\" has no keys, so it was left out.");
            return null;
        }

        return new ClipChannel<T>(address.Target, address.Property, kind, keys, address.Bone);
    }

    private static Key<T>? ReadKey(object? entry, ChannelKind<T> kind, Hold usual)
    {
        if (entry is not JsonMap map
            || Values.MaybeNumber(map, "at") is not { } at
            || !double.IsFinite(at)
            || !kind.TryRead(map.GetValueOrDefault("value"), out var value))
        {
            return null;
        }

        return new Key<T>(
            at,
            value,
            FileNames.Named<Hold>(map.GetValueOrDefault("hold")) ?? usual,
            FileNames.Named<Easing>(map.GetValueOrDefault("shape")),
            SlopeOf(kind, map.GetValueOrDefault("in")),
            SlopeOf(kind, map.GetValueOrDefault("out")));
    }

    private static Slope<T>? SlopeOf(ChannelKind<T> kind, object? raw) =>
        kind.TryReadSlope(raw, out var slope) ? new Slope<T>(slope) : null;

    /// <summary>
    /// The hold most keys use, which is said once on the channel so that only the keys that differ say their
    /// own: an imported clip is a thousand linear keys, and a thousand <c>"hold": "linear"</c>s are noise in
    /// every diff. A tie goes to the hold that came first.
    /// </summary>
    private Hold UsualHold() => Keys.GroupBy(static key => key.Hold).Aggregate((a, b) => b.Count() > a.Count() ? b : a).Key;

    private OrderedDictionary<string, object?> KeyToJson(Key<T> key, Hold usual) => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["at"] = key.At,
        ["value"] = _kind.Write(key.Value),
        ["hold"] = key.Hold == usual ? null : FileNames.Of(key.Hold),
        ["shape"] = key.Shape is { } shape ? FileNames.Of(shape) : null,
        ["in"] = key.SlopeIn is { } slopeIn ? _kind.Write(slopeIn.Value) : null,
        ["out"] = key.SlopeOut is { } slopeOut ? _kind.Write(slopeOut.Value) : null,
    });

    /// <summary>
    /// <paramref name="keys"/> in order of time, keeping the order of any at the same moment.
    /// </summary>
    /// <remarks>
    /// Sorted here, once, because a channel is sampled constantly and assumes its keys are in order, and a
    /// file edited by hand is not always.
    /// </remarks>
    private static List<Key<T>> InOrder(IReadOnlyList<Key<T>> keys)
    {
        for (var i = 1; i < keys.Count; i++)
        {
            if (keys[i].At < keys[i - 1].At)
            {
                return [.. keys.OrderBy(static key => key.At)];
            }
        }

        return [.. keys];
    }
}

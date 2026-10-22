using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Orblit.Rig;

/// <summary>
/// A named group of bones, shown or hidden together. A generated rig runs to hundreds of bones and
/// an animator wants a dozen of them at a time. Collections are how the rest get out of the way:
/// the face while animating a walk, the machinery always.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The rigging term. It holds a name and a colour, not bones.")]
public sealed class BoneCollection(string name, bool visible = true, int? colour = null)
{
    public string Name { get; } = name;

    /// <summary>
    /// Whether the collection starts shown. Machinery starts hidden. It is not an animator's to
    /// touch, and a rig that opens showing every mechanism bone reads as broken.
    /// </summary>
    public bool Visible { get; } = visible;

    /// <summary>
    /// A colour for the controls in this collection, as 0xRRGGBB. Left and right get different
    /// colours in every rig anyone actually uses, because telling them apart in a mirrored pose is
    /// otherwise guesswork.
    /// </summary>
    public int? Colour { get; } = colour;

    public override string ToString() => $"BoneCollection({Name})";
}

/// <summary>
/// Which bones are in which collections. Many to many: a left arm's inverse controls belong to
/// both "Arm.L" and "IK", and being able to hide either is the point.
/// </summary>
public sealed class BoneCollections
{
    /// <summary>The colours sides are drawn in. Warm on the left, cool on the right, as nearly every rig does.</summary>
    public const int LeftColour = 0xE5893F;

    public const int RightColour = 0x4F86C6;

    public const int CentreColour = 0x8FB84F;

    private readonly OrderedDictionary<string, BoneCollection> _collections = [];

    // Each set is an OrderedDictionary used for its keys, because .NET has no ordered set.
    private readonly OrderedDictionary<string, OrderedDictionary<string, bool>> _members = [];

    public IEnumerable<BoneCollection> All => _collections.Values;

    public BoneCollection? this[string name] => _collections.GetValueOrDefault(name);

    public bool Contains(string name) => _collections.ContainsKey(name);

    /// <summary>
    /// Declares a collection, or leaves an existing one alone. Idempotent because rig types declare
    /// the collections they use as they generate, and both arms declare "IK". The second one saying
    /// so is not a mistake worth reporting.
    /// </summary>
    public BoneCollection Declare(string name, bool visible = true, int? colour = null)
    {
        if (!_collections.TryGetValue(name, out var found))
        {
            found = new BoneCollection(name, visible, colour);
            _collections[name] = found;
        }

        return found;
    }

    /// <summary>Puts a bone in a collection, declaring the collection if it is new.</summary>
    public void Add(string bone, string collection, bool visible = true, int? colour = null)
    {
        Declare(collection, visible, colour);
        if (!_members.TryGetValue(collection, out var members))
        {
            members = [];
            _members[collection] = members;
        }

        members[bone] = true;
    }

    public IReadOnlyList<string> MembersOf(string collection) =>
        _members.TryGetValue(collection, out var members) ? new ReadOnlyCollection<string>([.. members.Keys]) : [];

    /// <summary>Every collection a bone is in, sorted so the answer is stable.</summary>
    public IReadOnlyList<string> CollectionsOf(string bone)
    {
        var found = new List<string>();
        foreach (var (name, members) in _members)
        {
            if (members.ContainsKey(bone))
            {
                found.Add(name);
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>Collections in a stable order, so a rig generated twice reads the same.</summary>
    public IReadOnlyList<BoneCollection> Sorted
    {
        get
        {
            var list = new List<BoneCollection>(_collections.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return list;
        }
    }

    /// <summary>
    /// The name a limb's controls go under: "Arm.L (IK)" and the like. Built here rather than
    /// spelled out at each call site so every rig type produces the same shape of name, which is
    /// what makes the list readable once there are twenty of them.
    /// </summary>
    public static string LimbCollection(string baseName, Side? side, string? kind)
    {
        var sided = side is null ? baseName : $"{baseName}{side.Value.Suffix()}";
        return kind is null ? sided : $"{sided} ({kind})";
    }

    public static int ColourFor(Side? side) => side switch
    {
        Side.Left => LeftColour,
        Side.Right => RightColour,
        null => CentreColour,
        _ => throw new InvalidOperationException($"{side} is not a side."),
    };
}

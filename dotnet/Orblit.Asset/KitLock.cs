using System.Collections.ObjectModel;

namespace Orblit.Asset;

/// <summary><c>kits.lock</c>: every kit installed in one project, sorted by publisher and slug.</summary>
public sealed class KitLock
{
    /// <summary>The format of <c>kits.lock</c> this code writes, and the newest it reads.</summary>
    public const int Format = 1;

    private const int LockLimit = 8 * 1024 * 1024;
    private const int KitsLimit = 500;

    /// <summary>Throws a <see cref="FormatException"/> when a kit is listed twice or there are too many.</summary>
    public KitLock(IReadOnlyList<LockedKit> kits)
    {
        var names = kits.Select(kit => $"{kit.Publisher}/{kit.Slug}").Distinct().Count();
        if (names != kits.Count || kits.Count > KitsLimit)
        {
            throw new FormatException($"A project lists up to {KitsLimit} kits and no kit twice.");
        }

        var sorted = kits.ToList();
        sorted.Sort(ByName);
        Kits = new ReadOnlyCollection<LockedKit>(sorted);
    }

    /// <summary>A project with no kit installed.</summary>
    public static KitLock Empty { get; } = new([]);

    public IReadOnlyList<LockedKit> Kits { get; }

    /// <summary>Reads <c>kits.lock</c>. Keys it does not know are ignored, and a newer format is refused.</summary>
    public static KitLock Parse(byte[] bytes)
    {
        var json = KitJsonObject.Parse(bytes, KitRules.LockName, LockLimit);
        json.CheckFormat(Format);
        return new KitLock([.. json.Objects("kits").Select(LockedKit.FromJson)]);
    }

    /// <summary>The installed kit of that name, or null.</summary>
    public LockedKit? Find(string publisher, string slug) =>
        Kits.FirstOrDefault(kit => kit.Publisher == publisher && kit.Slug == slug);

    /// <summary>This lock with <paramref name="kit"/> in it, replacing the earlier install of that kit.</summary>
    public KitLock Adding(LockedKit kit) =>
        new([.. Kits.Where(held => held.Publisher != kit.Publisher || held.Slug != kit.Slug), kit]);

    public byte[] Encode() =>
        KitJsonObject.Encode(
            KitRules.LockName,
            LockLimit,
            new OrderedDictionary<string, object?>
            {
                ["formatVersion"] = (long)Format,
                ["kits"] = new List<object?>(Kits.Select(kit => kit.ToJson())),
            });

    private static int ByName(LockedKit a, LockedKit b)
    {
        var publishers = string.CompareOrdinal(a.Publisher, b.Publisher);
        return publishers != 0 ? publishers : string.CompareOrdinal(a.Slug, b.Slug);
    }
}

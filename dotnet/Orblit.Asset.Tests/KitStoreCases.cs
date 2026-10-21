using System.Text;
using System.Text.Json.Nodes;
using Orblit.Parity;

namespace Orblit.Asset.Tests;

// Writes what the port gives for the entry, index, release list and lock specs, in the shape the exporter used.
internal static class KitStoreCases
{
    public static JsonObject EntryOut(byte[] bytes, string publisher, string slug)
    {
        var entry = KitEntry.Parse(bytes, publisher, slug);
        return new JsonObject
        {
            ["hex"] = Convert.ToHexStringLower(entry.Encode()),
            ["versions"] = Dump.Arr(entry.Releases, release => Dump.Text(release.Version)),
            ["offer"] = Dump.Text(entry.Latest.Offer.Url.ToString()),
            ["origin"] = Dump.Text(entry.Latest.Origin.Base.ToString()),
            ["max"] = Dump.Big(entry.Latest.Origin.Policy.MaxBytes),
        };
    }

    // A valid entry followed by spaces up to length bytes, so the size limit is met at its edge.
    public static byte[] PaddedEntry(int length) =>
        Encoding.UTF8.GetBytes(EntryJson().PadRight(length));

    public static string EntryJson(string version = "1.0.0") =>
        "{\"releases\":[{\"version\":\"" + version + "\",\"url\":\"https://example.com/crates-" + version
        + ".opack\",\"hash\":\"" + ContentHash.Of([1, 2, 3]).Hex
        + "\",\"bytes\":1234,\"released\":\"2026-10-19\",\"notes\":\"Changes.\"}]}";

    public static JsonObject IndexOut(byte[] bytes)
    {
        var index = KitIndex.Parse(bytes);
        return new JsonObject
        {
            ["hex"] = Convert.ToHexStringLower(index.Encode()),
            ["rows"] = Dump.Arr(index.Rows, row => Dump.Text($"{row.Manifest.Publisher}/{row.Manifest.Slug}")),
        };
    }

    public static JsonObject ReleasesOut(byte[] bytes)
    {
        var releases = KitReleases.Parse(bytes);
        return new JsonObject
        {
            ["hex"] = Convert.ToHexStringLower(releases.Encode()),
            ["previews"] = Dump.Arr(releases.Previews, Dump.Text),
            ["versions"] = Dump.Arr(releases.Releases, item => Dump.Text(item.Release.Version)),
        };
    }

    public static JsonObject LockOut(byte[] bytes)
    {
        var kitLock = KitLock.Parse(bytes);
        return new JsonObject
        {
            ["hex"] = Convert.ToHexStringLower(kitLock.Encode()),
            ["kits"] = Dump.Arr(kitLock.Kits, kit => Dump.Text($"{kit.Publisher}/{kit.Slug}")),
        };
    }
}

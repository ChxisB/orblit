using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;

namespace Orblit.Asset.Tests;

// Builds the worlds the fixture's pack exporter described, and writes what the port gives in the shape it used.
internal static class PackCases
{
    private static readonly Regex Placeholder = new(@"\{h(\d)\}", RegexOptions.CultureInvariant);

    /// <summary>
    /// Packs each spec of a world in order. A spec names its files and the earlier packs it is a patch on,
    /// and may name a file to overwrite after the manifest is written.
    /// </summary>
    public static async Task<List<BuiltPack>> BuildWorldAsync(PackFixture fixture, JsonElement world)
    {
        var built = new List<BuiltPack>();
        foreach (var spec in world.EnumerateArray())
        {
            var files = spec.GetProperty("files").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
            var folder = await fixture.BundleAsync($"bundle{built.Count}", files);
            if (spec.TryGetProperty("stale", out var stale))
            {
                await File.WriteAllTextAsync(
                    Path.Combine(folder, stale.GetString()!), "stale", TestContext.Current.CancellationToken);
            }

            var @base = spec.GetProperty("base").EnumerateArray().Select(b => built[b.GetInt32()].Pack).ToList();
            built.Add(await fixture.PackFolderAsync(
                folder, $"pack{built.Count}", @base, spec.GetProperty("target").GetString()!));
        }

        return built;
    }

    /// <summary>Runs <paramref name="run"/> over every case of an op, with each case in a world of its own.</summary>
    public static async Task CheckAsync(Fixture golden, string op, Func<JsonElement, Task<JsonNode?>> run)
    {
        var cases = golden.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.NotEmpty(cases);
        for (var i = 0; i < cases.Count; i++)
        {
            var (got, error) = await Attempt(cases[i], run);
            var difference = JsonCompare.FirstDifference(
                cases[i].GetProperty("expect"), JsonSerializer.SerializeToElement(got));
            Assert.True(
                difference is null,
                $"{op} case {i}: {difference} {error} (from fixture commit {golden.SourceCommit})");
        }
    }

    private static async Task<(JsonObject Got, string? Error)> Attempt(
        JsonElement c, Func<JsonElement, Task<JsonNode?>> run)
    {
        try
        {
            return (new JsonObject { ["value"] = await run(c) }, null);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return (new JsonObject { ["throws"] = true }, $"C# threw {e.GetType().Name}: {e.Message}");
        }
    }

    private static JsonArray Ids(IEnumerable<AssetId> ids) => Dump.Arr(ids, id => Dump.Text(id.ToString()));

    private static JsonArray Hexes(IEnumerable<AssetPack> packs) => Dump.Arr(packs, p => Dump.Text(p.Hash.Hex));

    private static List<AssetPack> Picked(List<BuiltPack> built, JsonElement indexes) =>
        [.. indexes.EnumerateArray().Select(i => built[i.GetInt32()].Pack)];

    public static JsonNode SummaryOut(PackSummary s) => new JsonObject
    {
        ["hash"] = s.Hash.Hex,
        ["bytes"] = Dump.Big(s.Bytes),
        ["assets"] = s.Assets,
        ["added"] = s.Added,
        ["replaced"] = s.Replaced,
        ["blobs"] = s.Blobs,
        ["sharedBytes"] = Dump.Big(s.SharedBytes),
        ["removed"] = s.Removed,
    };

    public static async Task<JsonNode?> PackOut(JsonElement c)
    {
        using var fixture = new PackFixture();
        var built = await BuildWorldAsync(fixture, In(c, 0));
        return Dump.Arr(built, b => new JsonObject
        {
            ["bytes"] = Convert.ToBase64String(b.Bytes),
            ["hash"] = b.Hash.Hex,
            ["summary"] = SummaryOut(b.Summary),
        });
    }

    public static async Task<JsonNode?> OpenOut(JsonElement c)
    {
        var pack = await AssetPack.OpenAsync(new MemoryPackBytes(Convert.FromBase64String(Text(c, 0))));
        var reads = new JsonObject();
        foreach (var id in pack.Manifest.Entries.Keys)
        {
            reads[id.ToString()] = ContentHash.Of(await pack.ReadAsync(id)).Hex;
        }

        return new JsonObject
        {
            ["hash"] = pack.Hash.Hex,
            ["target"] = pack.Target,
            ["base"] = pack.Base?.Hex,
            ["removed"] = Ids(pack.Removed),
            ["manifest"] = pack.Manifest.Encode(),
            ["corrupt"] = Ids(await pack.FindCorruptAsync()),
            ["reads"] = reads,
        };
    }

    public static async Task<JsonNode?> DiffOut(JsonElement c)
    {
        using var fixture = new PackFixture();
        var built = await BuildWorldAsync(fixture, In(c, 0));
        var pool = Picked(built, In(c, 3));
        var result = await PackDiff.BetweenAsync(
            MountedPacks.WithBases(built[In(c, 1).GetInt32()].Pack, pool),
            MountedPacks.WithBases(built[In(c, 2).GetInt32()].Pack, pool));
        return new JsonObject
        {
            ["added"] = Ids(result.Added),
            ["removed"] = Ids(result.Removed),
            ["changed"] = Dump.Arr(result.Changed, x => new JsonArray(x.Id.ToString(), x.Before, x.After)),
            ["dangling"] = Dump.Arr(result.Dangling, d => new JsonArray(d.Document.ToString(), d.Missing.ToString())),
            ["unreadable"] = Ids(result.Unreadable),
            ["broken"] = result.IsBroken,
        };
    }

    public static async Task<JsonNode?> ChainOut(JsonElement c)
    {
        using var fixture = new PackFixture();
        var built = await BuildWorldAsync(fixture, In(c, 0));
        var chain = PatchChain.Of(built[In(c, 1).GetInt32()].Pack, Picked(built, In(c, 2)));
        return new JsonObject
        {
            ["ordered"] = Hexes(chain.Ordered),
            ["standalone"] = Hexes(chain.Standalone),
            ["skipped"] = Dump.Arr(chain.Skipped, s => new JsonArray(s.Pack.Hash.Hex, s.Reason)),
        };
    }

    public static async Task<JsonNode?> MountOut(JsonElement c)
    {
        using var fixture = new PackFixture();
        var built = await BuildWorldAsync(fixture, In(c, 1));
        var mounted = new MountedPacks(CookTargets.Find(Text(c, 0))!);
        var outcomes = new JsonArray();
        foreach (var step in In(c, 2).EnumerateArray())
        {
            outcomes.Add(Step(mounted, step[0].GetString()!, built[step[1].GetInt32()].Pack));
        }

        var reads = new JsonObject();
        var owners = new JsonObject();
        foreach (var text in In(c, 3).EnumerateArray().Select(t => t.GetString()!))
        {
            var id = AssetId.Parse(text);
            reads[text] = await mounted.ExistsAsync(id) ? Encoding.UTF8.GetString(await mounted.ReadAsync(id)) : null;
            owners[text] = mounted.OwnerOf(id)?.Hash.Hex;
        }

        return new JsonObject
        {
            ["steps"] = outcomes,
            ["target"] = mounted.Target,
            ["chainHash"] = mounted.ChainHash?.Hex,
            ["manifest"] = mounted.Manifest.Encode(),
            ["reads"] = reads,
            ["owners"] = owners,
        };
    }

    private static string Step(MountedPacks mounted, string kind, AssetPack pack)
    {
        try
        {
            switch (kind)
            {
                case "start":
                    mounted.Start(pack);
                    break;
                case "patch":
                    mounted.Patch(pack);
                    break;
                default:
                    mounted.AddMod(pack);
                    break;
            }

            return "ok";
        }
        catch (PackMountRefusedException)
        {
            return "throws";
        }
    }

    public static async Task<JsonNode?> FolderOut(JsonElement c)
    {
        using var fixture = new PackFixture();
        var built = await BuildWorldAsync(fixture, In(c, 0));
        var folder = fixture.At("downloads");
        foreach (var file in In(c, 1).EnumerateObject())
        {
            var target = Path.Combine(folder, Named(file.Name, built));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, FileBytes(file.Value, built), TestContext.Current.CancellationToken);
        }

        var directory = new PackFolder(folder);
        var listing = await directory.ListAsync();
        var removed = new List<bool>();
        foreach (var index in In(c, 2).EnumerateArray())
        {
            removed.Add(await directory.RemoveAsync(built[index.GetInt32()].Hash));
        }

        return new JsonObject
        {
            ["packs"] = Hexes(listing.Packs),
            ["unreadable"] = Names(folder, listing.Unreadable.Select(u => u.Path)),
            ["unfinished"] = Dump.Arr(listing.Unfinished, h => Dump.Text(h.Hex)),
            ["removed"] = Dump.Arr(removed, r => (JsonNode)r),
            ["left"] = Names(folder, Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                : []),
        };
    }

    private static string Named(string key, List<BuiltPack> built) =>
        Placeholder.Replace(key, m => built[int.Parse(m.Groups[1].Value)].Hash.Hex);

    private static byte[] FileBytes(JsonElement spec, List<BuiltPack> built)
    {
        if (spec.TryGetProperty("pack", out var pack))
        {
            return built[pack.GetInt32()].Bytes;
        }

        if (spec.TryGetProperty("cut", out var cut))
        {
            return built[cut[0].GetInt32()].Bytes[..cut[1].GetInt32()];
        }

        return spec.TryGetProperty("fill", out var fill)
            ? [.. Enumerable.Repeat((byte)fill[1].GetInt32(), fill[0].GetInt32())]
            : Encoding.UTF8.GetBytes(spec.GetProperty("text").GetString()!);
    }

    // Paths relative to the folder with forward slashes, in code unit order.
    private static JsonArray Names(string folder, IEnumerable<string> paths) =>
        Dump.Arr(
            paths.Select(p => Path.GetRelativePath(folder, p).Replace('\\', '/')).Order(StringComparer.Ordinal),
            Dump.Text);

    public static JsonNode? TargetOut(string name) =>
        CookTargets.Find(name) is { } target ? new JsonObject { ["recipe"] = Dump.Tree(target.Recipe) } : null;

    public static JsonNode PackNameOut(JsonElement c)
    {
        var target = CookTargets.Find(Text(c, 0))!;
        return Dump.Text(In(c, 1).ValueKind == JsonValueKind.Null
            ? PackName.FileName(target)
            : PackName.FileName(target, Text(c, 1)));
    }
}

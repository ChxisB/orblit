using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;

namespace Orblit.Asset.Tests;

public class AssetParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_asset", "asset");

    [Fact]
    public void IdsMatchTheDartTwin() => Dart.Check("id", c => Dump.Attempt(() => IdParts(Text(c, 0))));

    [Fact]
    public void ResolvedIdsMatchTheDartTwin() =>
        Dart.Check("resolve", c => Dump.Attempt(() =>
            JsonValue.Create(AssetId.Parse(Text(c, 0)).Resolve(Text(c, 1))?.ToString())));

    [Fact]
    public void HashesOfBytesMatchTheDartTwin() =>
        Dart.Check("hashOf", c => Dump.Attempt(() =>
            JsonValue.Create(ContentHash.Of(Convert.FromBase64String(Text(c, 0))).Hex)));

    [Fact]
    public void ParsedHashesMatchTheDartTwin() =>
        Dart.Check("hashParse", c => Dump.Attempt(() =>
        {
            var hash = ContentHash.Parse(Text(c, 0));
            return new JsonObject { ["hex"] = hash.Hex, ["shard"] = hash.Shard };
        }));

    [Fact]
    public void ManifestsMatchTheDartTwin() => Dart.Check("manifest", c => Dump.Attempt(() => ManifestOut(Text(c, 0))));

    [Fact]
    public void ImportSettingsMatchTheDartTwin() => Dart.Check("settings", c => Dump.Attempt(() => SettingsOut(Text(c, 0))));

    [Fact]
    public void SettingsFileNamesMatchTheDartTwin() =>
        Dart.Check("settingsNames", c => Dump.Attempt(() => SettingsNames(Text(c, 0))));

    [Fact]
    public void CookKeysMatchTheDartTwin() => Dart.Check("cookKey", c => Dump.Attempt(() => KeyOut(In(c, 0))));

    // Both caches write the same directory byte for byte, so each reads what the other wrote.
    [Fact]
    public async Task CookCacheDirectoriesMatchTheDartTwin()
    {
        var cases = Dart.Cases.Where(c => c.GetProperty("op").GetString() == "cacheRun").ToList();
        Assert.NotEmpty(cases);
        for (var i = 0; i < cases.Count; i++)
        {
            var expected = cases[i].GetProperty("expect").GetProperty("value");
            var got = JsonSerializer.SerializeToElement(await CacheRun(cases[i]));
            var difference = JsonCompare.FirstDifference(expected, got);
            Assert.True(difference is null, $"cacheRun case {i}: {difference} (from Dart commit {Dart.DartCommit})");
        }
    }

    // The Dart files, opened by the C# cache: the same hits, the same bytes and the same failures.
    [Fact]
    public async Task CookCacheDirectoriesDartWroteAreReadByTheCSharpCache()
    {
        foreach (var c in Dart.Cases.Where(c => c.GetProperty("op").GetString() == "cacheRun"))
        {
            var expected = c.GetProperty("expect").GetProperty("value");
            var root = Directory.CreateTempSubdirectory("orblit_parity_read_");
            try
            {
                var path = Path.Combine(root.FullName, "cache");
                foreach (var file in expected.GetProperty("files").EnumerateObject())
                {
                    var target = Path.Combine(path, file.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllBytes(target, Convert.FromBase64String(file.Value.GetString()!));
                }

                await Reads(new DirectoryCookCache(path), expected);
            }
            finally
            {
                root.Delete(recursive: true);
            }
        }
    }

    private static async Task Reads(DirectoryCookCache cache, JsonElement expected)
    {
        foreach (var hit in expected.GetProperty("hits").EnumerateObject())
        {
            var at = hit.Name.LastIndexOf('/');
            var key = CacheKey(hit.Name[..at], hit.Name[(at + 1)..]);
            var found = await cache.LookUpAsync(key);
            Assert.Equal(hit.Value.GetRawText(), HitOut(found)?.ToJsonString() ?? "null");
            foreach (var output in found?.Outputs ?? [])
            {
                Assert.Equal(output.Bytes, (await cache.ReadAsync(output.Hash))!.Length);
            }

            var reason = expected.GetProperty("failures").GetProperty(hit.Name);
            Assert.Equal(reason.GetString(), (await cache.LookUpFailureAsync(key))?.Reason);
        }
    }
}

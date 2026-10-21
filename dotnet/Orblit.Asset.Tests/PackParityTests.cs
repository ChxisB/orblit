using System.Text.Json;
using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.PackCases;

namespace Orblit.Asset.Tests;

public class PackParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_asset", "pack");

    // Both writers give the same file byte for byte, so each reads what the other wrote.
    [Fact]
    public Task PacksMatchTheDartTwinByteForByte() => CheckAsync(Dart, "pack", PackOut);

    // The Dart files, opened from disk by the C# reader: the same hashes, no corrupt blob, and every patch
    // chain mounts and reads back what its manifest says.
    [Fact]
    public async Task PacksAndPatchesDartWroteAreReadFromFilesByTheCSharpReader()
    {
        using var fixture = new PackFixture();
        var held = new List<FilePackBytes>();
        var patches = 0;
        try
        {
            foreach (var written in DartPacks())
            {
                var packs = new List<AssetPack>();
                foreach (var pack in written)
                {
                    var path = fixture.At($"dart{held.Count}.opack");
                    await File.WriteAllBytesAsync(
                        path, Convert.FromBase64String(pack.GetProperty("bytes").GetString()!), TestContext.Current.CancellationToken);
                    held.Add(new FilePackBytes(path));
                    packs.Add(await AssetPack.OpenAsync(held[^1]));
                    Assert.Equal(pack.GetProperty("hash").GetString(), packs[^1].Hash.Hex);
                    Assert.Empty(await packs[^1].FindCorruptAsync());
                }

                patches += await ReadsTheChain(packs);
            }
        }
        finally
        {
            foreach (var file in held)
            {
                await file.DisposeAsync();
            }
        }

        Assert.True(patches >= 3, $"only {patches} patches were mounted");
    }

    private static IEnumerable<JsonElement.ArrayEnumerator> DartPacks() =>
        Dart.Cases
            .Where(c => c.GetProperty("op").GetString() == "pack" && c.GetProperty("expect").TryGetProperty("value", out _))
            .Select(c => c.GetProperty("expect").GetProperty("value").EnumerateArray());

    private static async Task<int> ReadsTheChain(List<AssetPack> packs)
    {
        var chain = PatchChain.Of(packs[0], packs.Skip(1));
        var mounted = new MountedPacks(CookTargets.Find(packs[0].Target)!);
        mounted.Start(packs[0]);
        foreach (var patch in chain.Ordered)
        {
            mounted.Patch(patch);
        }

        Assert.Equal((chain.Ordered.Count > 0 ? chain.Ordered[^1] : packs[0]).Hash, mounted.ChainHash);
        foreach (var (id, entry) in mounted.Manifest.Entries)
        {
            Assert.Equal(entry.Hash, ContentHash.Of(await mounted.ReadAsync(id)));
        }

        return chain.Ordered.Count;
    }

    [Fact]
    public Task OpenedPacksMatchTheDartTwin() => CheckAsync(Dart, "open", OpenOut);

    [Fact]
    public Task DiffsMatchTheDartTwin() => CheckAsync(Dart, "diff", DiffOut);

    [Fact]
    public Task PatchChainsMatchTheDartTwin() => CheckAsync(Dart, "chain", ChainOut);

    [Fact]
    public Task MountedPacksMatchTheDartTwin() => CheckAsync(Dart, "mount", MountOut);

    [Fact]
    public Task PackFoldersMatchTheDartTwin() => CheckAsync(Dart, "folder", FolderOut);

    [Fact]
    public void CookTargetsMatchTheDartTwin() => Dart.Check("target", c => Dump.Attempt(() => TargetOut(Text(c, 0))));

    [Fact]
    public void PackFileNamesMatchTheDartTwin() => Dart.Check("packName", c => Dump.Attempt(() => PackNameOut(c)));
}

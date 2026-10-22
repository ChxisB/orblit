using System.Text.Json;
using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.PackCases;

namespace Orblit.Asset.Tests;

public class PackParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_asset", "pack");

    // Both writers give the same file byte for byte, so each reads what the other wrote.
    [Fact]
    public Task PacksMatchTheFixtureByteForByte() => CheckAsync(Golden, "pack", PackOut);

    // The fixture's files, opened from disk by the reader: the same hashes, no corrupt blob, and every patch
    // chain mounts and reads back what its manifest says.
    [Fact]
    public async Task PacksAndPatchesInTheFixtureAreReadFromFilesByTheReader()
    {
        using var fixture = new PackFixture();
        var held = new List<FilePackBytes>();
        var patches = 0;
        try
        {
            foreach (var written in FixturePacks())
            {
                var packs = new List<AssetPack>();
                foreach (var pack in written)
                {
                    var path = fixture.At($"pack{held.Count}.opack");
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

    private static IEnumerable<JsonElement.ArrayEnumerator> FixturePacks() =>
        Golden.Cases
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
    public Task OpenedPacksMatchTheFixture() => CheckAsync(Golden, "open", OpenOut);

    [Fact]
    public Task DiffsMatchTheFixture() => CheckAsync(Golden, "diff", DiffOut);

    [Fact]
    public Task PatchChainsMatchTheFixture() => CheckAsync(Golden, "chain", ChainOut);

    [Fact]
    public Task MountedPacksMatchTheFixture() => CheckAsync(Golden, "mount", MountOut);

    [Fact]
    public Task PackFoldersMatchTheFixture() => CheckAsync(Golden, "folder", FolderOut);

    [Fact]
    public void CookTargetsMatchTheFixture() => Golden.Check("target", c => Dump.Attempt(() => TargetOut(Text(c, 0))));

    [Fact]
    public void PackFileNamesMatchTheFixture() => Golden.Check("packName", c => Dump.Attempt(() => PackNameOut(c)));
}

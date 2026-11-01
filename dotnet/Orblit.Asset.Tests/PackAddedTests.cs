using System.Buffers.Binary;

namespace Orblit.Asset.Tests;

// What a fixture cannot be asked: the messages it does not compare, the read range of
// FilePackBytes, a pack read through a cache, a pack header whose
// directory sits past 4 GiB, a bundle file changed to other bytes of the same length, and lists a caller
// cannot change.
public sealed class PackAddedTests : IDisposable
{
    private readonly PackFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    private static readonly Dictionary<string, string> OneFile = new() { ["a/x.glb"] = "one one one one" };

    private async Task<string> HundredBytes()
    {
        var path = _fixture.At("data.bin");
        await File.WriteAllBytesAsync(path, [.. Enumerable.Range(0, 100).Select(i => (byte)i)], TestContext.Current.CancellationToken);
        return path;
    }

    // FilePackBytes

    [Fact]
    public async Task AFileRangeIsReadFromItsStart()
    {
        await using var bytes = new FilePackBytes(await HundredBytes(), start: 10, bytes: 50);

        Assert.Equal(50, await bytes.LengthAsync());
        Assert.Equal<byte[]>([10, 11, 12, 13, 14], await bytes.ReadAsync(0, 5));
        Assert.Equal<byte[]>([59], await bytes.ReadAsync(49, 1));
    }

    [Fact]
    public async Task ALengthPastTheEndOfTheFileIsTheRestOfIt()
    {
        var path = await HundredBytes();
        await using var long_ = new FilePackBytes(path, start: 10, bytes: 500);
        await using var whole = new FilePackBytes(path);
        await using var beyond = new FilePackBytes(path, start: 200);

        Assert.Equal(90, await long_.LengthAsync());
        Assert.Equal(100, await whole.LengthAsync());
        Assert.Equal(0, await beyond.LengthAsync());
    }

    [Fact]
    public async Task AReadMayRunPastTheRangeButNotPastTheFile()
    {
        await using var bytes = new FilePackBytes(await HundredBytes(), start: 10, bytes: 20);

        Assert.Equal(50, (await bytes.ReadAsync(0, 50)).Length);
        Assert.Equal(90, (await bytes.ReadAsync(0, 90)).Length);
        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bytes.ReadAsync(0, 91));
        Assert.Contains("which is 100 bytes long", error.Message);
        Assert.Contains("Cannot read 95 bytes at 0", (await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bytes.ReadAsync(0, 95))).Message);
    }

    [Fact]
    public async Task ARangeThatCannotBeReadIsRefusedBeforeAnythingIsAllocated()
    {
        await using var bytes = new FilePackBytes(await HundredBytes());

        Assert.Contains("Cannot read 1 bytes at -1", (await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bytes.ReadAsync(-1, 1))).Message);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bytes.ReadAsync(0, -1));
        // The sum of the two wraps, so only the size itself says it cannot be read.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => bytes.ReadAsync(1, long.MaxValue));
    }

    [Fact]
    public async Task AClosedFileIsOpenedAgainByTheNextRead()
    {
        var bytes = new FilePackBytes(await HundredBytes());
        Assert.Equal<byte[]>([3, 4], await bytes.ReadAsync(3, 2));

        await bytes.CloseAsync();
        Assert.Equal<byte[]>([5, 6], await bytes.ReadAsync(5, 2));

        await bytes.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => bytes.ReadAsync(0, 1));
    }

    [Fact]
    public void ARangeBelowNothingIsRefusedByTheConstructor()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FilePackBytes("x", start: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FilePackBytes("x", bytes: -1));
        Assert.Equal(0, new FilePackBytes("x", bytes: 0).Bytes);
    }

    // MemoryPackBytes

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(-1, 0)]
    [InlineData(0, 6)]
    [InlineData(5, 1)]
    [InlineData(long.MaxValue, 2)]
    [InlineData(3, long.MaxValue)]
    public async Task AMemoryRangeThatIsNotInsideTheBytesIsRefused(long at, long bytes)
    {
        var memory = new MemoryPackBytes([1, 2, 3, 4, 5]);

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => memory.ReadAsync(at, bytes));

        Assert.Contains($"Cannot read {bytes} bytes at {at} from a pack of 5", error.Message);
    }

    [Fact]
    public async Task AMemoryReadIsACopy()
    {
        var data = new byte[] { 1, 2, 3 };
        var memory = new MemoryPackBytes(data);

        (await memory.ReadAsync(0, 3))[0] = 9;

        Assert.Equal<byte[]>([1, 2, 3], await memory.ReadAsync(0, 3));
        Assert.Empty(await memory.ReadAsync(3, 0));
    }

    // opening

    [Fact]
    public async Task APackHeaderAloneIsShortByTheRestOfIt()
    {
        var good = await _fixture.BuildAsync("game", OneFile);

        var error = await Assert.ThrowsAsync<FormatException>(
            () => AssetPack.OpenAsync(new MemoryPackBytes(good.Bytes[..PackHeader.Size])));

        Assert.Contains("This pack is short by", error.Message);
        Assert.Contains("not an Orblit pack", (await Assert.ThrowsAsync<FormatException>(
            () => AssetPack.OpenAsync(new MemoryPackBytes(good.Bytes[..(PackHeader.Size - 1)])))).Message);
    }

    [Fact]
    public async Task ADirectoryPastFourGibIsShortNotInsideTheHeader()
    {
        var good = await _fixture.BuildAsync("game", OneFile);
        var far = (byte[])good.Bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(far.AsSpan(12), 1);

        var error = await Assert.ThrowsAsync<FormatException>(() => AssetPack.OpenAsync(new MemoryPackBytes(far)));

        Assert.Contains("This pack is short by", error.Message);
    }

    [Fact]
    public async Task ADirectorySizePastSixtyThreeBitsIsRefusedWhole()
    {
        var good = await _fixture.BuildAsync("game", OneFile);
        var huge = (byte[])good.Bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(huge.AsSpan(20), 0x80000000);

        var error = await Assert.ThrowsAsync<FormatException>(() => AssetPack.OpenAsync(new MemoryPackBytes(huge)));

        Assert.Contains("past what any file can hold", error.Message);
    }

    [Fact]
    public async Task ADirectoryWhoseStartAndSizeAddUpPastSixtyFourBitsIsShort()
    {
        var good = await _fixture.BuildAsync("game", OneFile);
        var wraps = (byte[])good.Bytes.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(wraps.AsSpan(12), 0x40000000);
        BinaryPrimitives.WriteUInt32LittleEndian(wraps.AsSpan(20), 0x40000000);

        var error = await Assert.ThrowsAsync<FormatException>(() => AssetPack.OpenAsync(new MemoryPackBytes(wraps)));

        Assert.Contains("This pack is short by", error.Message);
    }

    [Fact]
    public async Task ABlobSizeNearSixtyThreeBitsRunsIntoTheDirectory()
    {
        var good = await _fixture.BuildAsync("game", OneFile);
        var wrong = AssetPackTests.WithDirectory(
            good.Bytes, directory => ((OrderedDictionary<string, object?>)((OrderedDictionary<string, object?>)directory["blobs"]!).Values.First()!)["bytes"] = long.MaxValue - 8);

        var error = await Assert.ThrowsAsync<FormatException>(() => AssetPack.OpenAsync(new MemoryPackBytes(wrong)));

        Assert.Contains("inside the directory", error.Message);
    }

    [Fact]
    public async Task ABlobAtTheStartOfTheFileIsInsideTheHeader()
    {
        var good = await _fixture.BuildAsync("game", OneFile);
        var wrong = AssetPackTests.WithDirectory(
            good.Bytes, directory => ((OrderedDictionary<string, object?>)((OrderedDictionary<string, object?>)directory["blobs"]!).Values.First()!)["at"] = 0L);

        var error = await Assert.ThrowsAsync<FormatException>(() => AssetPack.OpenAsync(new MemoryPackBytes(wrong)));

        Assert.Contains("inside the header", error.Message);
    }

    // reading through a cache

    [Fact]
    public async Task AReadThroughACacheKeepsTheBlobAndAnotherReadNeedsNoBytes()
    {
        var good = await _fixture.BuildAsync("game", OneFile);
        var counted = new CountingBytes(new MemoryPackBytes(good.Bytes));
        var cache = new MemoryContentStore();
        var pack = await AssetPack.OpenAsync(counted, cache);
        var id = AssetId.Parse("a/x.glb");

        var first = await pack.ReadAsync(id);
        var reads = counted.Reads;
        var second = await pack.ReadAsync(id);

        Assert.Equal("one one one one", System.Text.Encoding.UTF8.GetString(first));
        Assert.Equal(first, second);
        Assert.Equal(reads, counted.Reads);
        Assert.NotNull(await cache.GetAsync(ContentHash.Of(first)));
    }

    [Fact]
    public async Task ABlobThatIsNotWhatItsHashSaysIsRefusedWhenReadThroughACache()
    {
        var good = await _fixture.BuildAsync("game", OneFile);
        var blob = AssetPackTests.DirectoryOf(good.Bytes).Blobs.Values.Single();
        var flipped = (byte[])good.Bytes.Clone();
        flipped[blob.At] ^= 1;
        var pack = await AssetPack.OpenAsync(new MemoryPackBytes(flipped), new MemoryContentStore());

        var error = await Assert.ThrowsAsync<FormatException>(() => pack.ReadAsync(AssetId.Parse("a/x.glb")));

        Assert.Contains("do not match the hash in this pack", error.Message);
    }

    private sealed class CountingBytes(IPackBytes inner) : IPackBytes
    {
        public int Reads { get; private set; }

        public Task<long> LengthAsync() => inner.LengthAsync();

        public Task<byte[]> ReadAsync(long at, long bytes)
        {
            Reads++;
            return inner.ReadAsync(at, bytes);
        }
    }

    // packing

    [Fact]
    public async Task AFileChangedToOtherBytesOfTheSameLengthIsRefused()
    {
        var folder = await _fixture.BundleAsync("bundle", new Dictionary<string, string> { ["a/x.glb"] = "one" });
        await File.WriteAllTextAsync(Path.Combine(folder, "a", "x.glb"), "two", TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<FormatException>(
            () => PackBundle.PackAsync(folder, _fixture.At("out.opack"), CookTargets.Android));

        Assert.Contains("\"a/x.glb\" is not what its manifest says", error.Message);
    }

    // mounting

    [Fact]
    public async Task AMissingAssetSaysHowManyPacksWereAskedOrThatNoneIsMounted()
    {
        var shipped = await _fixture.PackAsync("game", OneFile);
        var patch = await _fixture.PackAsync("patch", new Dictionary<string, string> { ["a/x.glb"] = "two" }, [shipped]);
        var mod = await _fixture.PackAsync("mod", new Dictionary<string, string> { ["b/y.glb"] = "mod" });
        var packs = new MountedPacks(CookTargets.Android);
        var missing = AssetId.Parse("c/z.glb");

        Assert.Contains("no pack is mounted", (await Assert.ThrowsAsync<AssetNotFoundException>(() => packs.ReadAsync(missing))).Message);

        packs.Start(shipped);
        packs.Patch(patch);
        packs.AddMod(mod);
        Assert.Contains("none of the 3 mounted packs has it", (await Assert.ThrowsAsync<AssetNotFoundException>(() => packs.ReadAsync(missing))).Message);
    }

    // lists

    [Fact]
    public async Task TheListsOfADiffCannotBeChanged()
    {
        var shipped = await _fixture.PackAsync("game", OneFile);
        var packs = new MountedPacks(CookTargets.Android);
        packs.Start(shipped);
        var diff = await PackDiff.BetweenAsync(packs, packs);

        Assert.Throws<NotSupportedException>(() => ((IList<AssetId>)diff.Added).Add(AssetId.Parse("a/x.glb")));
        Assert.Throws<NotSupportedException>(() => ((IList<AssetId>)diff.Unreadable).Add(AssetId.Parse("a/x.glb")));
    }

    [Theory]
    [InlineData("macos", "macos")]
    [InlineData("MacOS", "macos")]
    [InlineData("WEB", "web")]
    public void ATargetIsFoundWhateverTheCase(string name, string found) =>
        Assert.Equal(found, CookTargets.Find(name)?.Name);

    [Fact]
    public void ANameThatIsNoTargetIsNotFound() => Assert.Null(CookTargets.Find("any"));
}

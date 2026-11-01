using System.Buffers.Binary;
using System.Text;
using Orblit.Json;

namespace Orblit.Asset.Tests;

using JsonMap = OrderedDictionary<string, object?>;

public sealed class AssetPackTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_pack_");

    public void Dispose() => _temp.Delete(recursive: true);

    private string Temp(string relative) => At(_temp.FullName, relative);

    private static string At(string basePath, string relative) =>
        Path.Combine([basePath, .. relative.Split('/')]);

    // Bytes for content number n. Number 0 is empty and every other number is a different length or starts
    // with a different pair of bytes.
    private static byte[] Content(int n) => n == 0
        ? []
        : [(byte)(n & 0xff), (byte)(n >> 8), .. Enumerable.Range(0, (n * 13) % 200).Select(i => (byte)((i * 7) + n))];

    // A bundle of count assets where the last duplicates have the same bytes as the first duplicates.
    private static async Task<Dictionary<AssetId, byte[]>> BundleAsync(string folder, int count, int duplicates = 0)
    {
        var token = TestContext.Current.CancellationToken;
        var assets = new Dictionary<AssetId, byte[]>();
        var entries = new Dictionary<AssetId, AssetEntry>();
        for (var i = 0; i < count; i++)
        {
            var id = AssetId.Parse($"models/m{i}.glb");
            var bytes = Content(i >= count - duplicates ? i - (count - duplicates) : i);
            assets[id] = bytes;
            entries[id] = new AssetEntry(ContentHash.Of(bytes), bytes.Length);
            var file = At(folder, $"{id}");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, bytes, token);
        }

        await File.WriteAllTextAsync(At(folder, "manifest.json"), new AssetManifest(entries).Encode(), token);
        return assets;
    }

    private static Task<PackSummary> PackAsync(string folder, string outPath, string target = "macos") =>
        PackBundle.PackAsync(folder, outPath, CookTargets.Find(target)!);

    internal static PackDirectory DirectoryOf(byte[] pack)
    {
        var header = PackHeader.Read(pack);
        return PackDirectory.Decode(pack.AsSpan((int)header.DirectoryAt, (int)header.DirectoryBytes), header.DirectoryAt);
    }

    // The pack with its directory changed by edit, and the header following.
    internal static byte[] WithDirectory(byte[] pack, Action<JsonMap> edit)
    {
        var header = PackHeader.Read(pack);
        var directory = (JsonMap)JsonText.Decode(
            Encoding.UTF8.GetString(pack, (int)header.DirectoryAt, (int)header.DirectoryBytes))!;
        edit(directory);
        var text = Encoding.UTF8.GetBytes(JsonText.Encode(directory));
        var body = pack[..(int)header.DirectoryAt];
        new PackHeader(header.DirectoryAt, text.Length).Encode().CopyTo(body, 0);
        return [.. body, .. text];
    }

    private static List<JsonMap> BlobsByPlace(JsonMap directory) =>
        [.. ((JsonMap)directory["blobs"]!).Values.Cast<JsonMap>().OrderBy(blob => (long)blob["at"]!)];

    private static async Task Refused(Task<AssetPack> opening, string text) => await Refused(() => opening, text);

    private static async Task Refused<T>(Func<Task<T>> doing, string text)
    {
        var error = await Assert.ThrowsAnyAsync<FormatException>(doing);
        Assert.Contains(text, error.Message);
    }

    private static Task<AssetPack> Open(byte[] bytes) => AssetPack.OpenAsync(new MemoryPackBytes(bytes));

    private async Task<byte[]> GoodAsync()
    {
        var folder = Temp("bundle");
        await BundleAsync(folder, 3);
        var outPath = Temp("game.opack");
        await PackAsync(folder, outPath);
        return await File.ReadAllBytesAsync(outPath, TestContext.Current.CancellationToken);
    }

    // packing a bundle

    [Fact]
    public async Task StoresDuplicatesOnceAndReadsEveryAssetBack()
    {
        var token = TestContext.Current.CancellationToken;
        var folder = Temp("bundle");
        var assets = await BundleAsync(folder, 1000, duplicates: 200);
        var outPath = Temp("game.opack");

        var summary = await PackAsync(folder, outPath);

        Assert.Equal(1000, summary.Assets);
        Assert.Equal(800, summary.Blobs);
        Assert.Equal(new FileInfo(outPath).Length, summary.Bytes);
        long shared = 0;
        for (var i = 800; i < 1000; i++)
        {
            shared += Content(i - 800).Length;
        }

        Assert.Equal(shared, summary.SharedBytes);

        var fromMemory = await Open(await File.ReadAllBytesAsync(outPath, token));
        await using var file = new FilePackBytes(outPath);
        var fromFile = await AssetPack.OpenAsync(file);
        Assert.Equal(summary.Hash, fromMemory.Hash);
        Assert.Equal(summary.Hash, fromFile.Hash);
        Assert.Equal("macos", fromFile.Target);
        Assert.Null(fromFile.Base);
        Assert.Empty(fromFile.Removed);
        foreach (var (key, value) in assets)
        {
            var memoryRead = await fromMemory.ReadAsync(key);
            var fileRead = await fromFile.ReadAsync(key);
            Assert.True(value.AsSpan().SequenceEqual(memoryRead), $"{key} from memory");
            Assert.True(value.AsSpan().SequenceEqual(fileRead), $"{key} from file");
        }
    }

    [Fact]
    public async Task SaysWhatIsInItAsAManifest()
    {
        var folder = Temp("bundle");
        var assets = await BundleAsync(folder, 20);
        var outPath = Temp("game.opack");
        await PackAsync(folder, outPath);

        await using var file = new FilePackBytes(outPath);
        var opened = await AssetPack.OpenAsync(file);

        Assert.Equal(20, opened.Manifest.Entries.Count);
        foreach (var (key, value) in assets)
        {
            Assert.Equal(new AssetEntry(ContentHash.Of(value), value.Length), opened.Manifest.Entries[key]);
        }
    }

    [Fact]
    public async Task TwoPacksOfOneBundleAreTheSameBytes()
    {
        var token = TestContext.Current.CancellationToken;
        var folder = Temp("bundle");
        await BundleAsync(folder, 300, duplicates: 50);
        var first = Temp("a.opack");
        var second = Temp("b.opack");

        await PackAsync(folder, first);
        await PackAsync(folder, second);

        Assert.Equal(await File.ReadAllBytesAsync(first, token), await File.ReadAllBytesAsync(second, token));
    }

    [Fact]
    public async Task PutsEveryBlobOnA16ByteBoundary()
    {
        var folder = Temp("bundle");
        await BundleAsync(folder, 100);
        var outPath = Temp("game.opack");
        await PackAsync(folder, outPath);

        var directory = DirectoryOf(await File.ReadAllBytesAsync(outPath, TestContext.Current.CancellationToken));

        Assert.NotEmpty(directory.Blobs);
        foreach (var blob in directory.Blobs.Values)
        {
            Assert.Equal(0, blob.At % 16);
        }
    }

    [Fact]
    public async Task HoldsAnEmptyAsset()
    {
        var folder = Temp("bundle");
        await BundleAsync(folder, 1);
        var outPath = Temp("game.opack");
        await PackAsync(folder, outPath);

        await using var file = new FilePackBytes(outPath);
        var opened = await AssetPack.OpenAsync(file);

        Assert.Empty(await opened.ReadAsync(AssetId.Parse("models/m0.glb")));
    }

    [Fact]
    public async Task RefusesAFileThatIsNotWhatTheManifestSays()
    {
        var folder = Temp("bundle");
        await BundleAsync(folder, 10);
        await File.WriteAllBytesAsync(At(folder, "models/m5.glb"), [9, 9, 9], TestContext.Current.CancellationToken);
        var outPath = Temp("game.opack");

        await Assert.ThrowsAnyAsync<FormatException>(() => PackAsync(folder, outPath));

        Assert.False(File.Exists(outPath));
        Assert.False(File.Exists($"{outPath}.partial"));
    }

    [Fact]
    public async Task RefusesAFileTheManifestListsAndTheBundleLacks()
    {
        var folder = Temp("bundle");
        await BundleAsync(folder, 10);
        File.Delete(At(folder, "models/m5.glb"));

        await Refused(() => PackAsync(folder, Temp("game.opack")), "models/m5.glb");
    }

    [Fact]
    public async Task RefusesAFolderWithNoManifest()
    {
        var folder = Temp("bundle");
        Directory.CreateDirectory(folder);

        await Refused(() => PackAsync(folder, Temp("game.opack")), "no manifest.json");
    }

    // opening a pack

    [Fact]
    public async Task RefusesATruncatedPackAndSaysByHowMuch()
    {
        var good = await GoodAsync();

        await Refused(Open(good[..^10]), "short by 10 bytes");
    }

    [Fact]
    public async Task RefusesAFileTooSmallToHaveAHeader() => await Refused(Open(new byte[5]), "not an Orblit pack");

    [Fact]
    public async Task RefusesAFileThatDoesNotStartWithOpak()
    {
        var wrong = await GoodAsync();
        wrong[0] = 0x50;

        await Refused(Open(wrong), "OPAK");
    }

    [Fact]
    public async Task RefusesANewerFormatAndNamesBothNumbers()
    {
        var newer = await GoodAsync();
        BinaryPrimitives.WriteUInt32LittleEndian(newer.AsSpan(4), 2);

        await Refused(Open(newer), "format 2; this one reads up to 1");
    }

    [Fact]
    public async Task RefusesADirectoryThatIsNotJson()
    {
        var broken = await GoodAsync();
        broken[PackHeader.Read(broken).DirectoryAt] = 0x78;

        await Refused(Open(broken), "not JSON");
    }

    [Fact]
    public async Task RefusesADirectoryOfTheWrongKind()
    {
        var wrong = WithDirectory(await GoodAsync(), json => json["kind"] = "orblit.scene");

        await Refused(Open(wrong), "orblit.scene");
    }

    [Fact]
    public async Task RefusesAnAssetWhoseBlobIsNotThere()
    {
        var wrong = WithDirectory(await GoodAsync(), json => ((JsonMap)json["assets"]!)["models/m0.glb"] = new string('a', 64));

        await Refused(Open(wrong), "does not have");
    }

    [Fact]
    public async Task RefusesBlobsThatOverlap()
    {
        var wrong = WithDirectory(await GoodAsync(), json =>
        {
            var places = BlobsByPlace(json);
            places[^1]["at"] = places[^2]["at"];
        });

        await Refused(Open(wrong), "overlap");
    }

    [Fact]
    public async Task RefusesRemovalsWhenThereIsNoBase()
    {
        var wrong = WithDirectory(await GoodAsync(), json => json["removed"] = new List<object?> { "models/gone.glb" });

        await Refused(Open(wrong), "no base");
    }

    [Fact]
    public async Task RefusesAnIdThatIsBothRemovedAndHeld()
    {
        var wrong = WithDirectory(await GoodAsync(), json =>
        {
            json["base"] = ContentHash.Of(new byte[] { 1 }).Hex;
            json["removed"] = new List<object?> { "models/m1.glb" };
        });

        await Refused(Open(wrong), "both removed and held");
    }

    [Fact]
    public async Task RefusesAnIdRemovedTwice()
    {
        var wrong = WithDirectory(await GoodAsync(), json =>
        {
            json["base"] = ContentHash.Of(new byte[] { 1 }).Hex;
            json["removed"] = new List<object?> { "models/gone.glb", "models/gone.glb" };
        });

        await Refused(Open(wrong), "removed twice");
    }

    [Fact]
    public async Task RefusesABlobInsideTheHeader()
    {
        var wrong = WithDirectory(await GoodAsync(), json => ((JsonMap)((JsonMap)json["blobs"]!).Values.First()!)["at"] = 16L);

        await Refused(Open(wrong), "inside the header");
    }

    [Fact]
    public async Task RefusesABlobThatIsNotOnABoundary()
    {
        var wrong = WithDirectory(await GoodAsync(), json => ((JsonMap)((JsonMap)json["blobs"]!).Values.First()!)["at"] = 33L);

        await Refused(Open(wrong), "multiple of 16");
    }

    [Fact]
    public async Task RefusesABlobThatRunsIntoTheDirectory()
    {
        var wrong = WithDirectory(await GoodAsync(), json => BlobsByPlace(json)[^1]["bytes"] = 1L << 20);

        await Refused(Open(wrong), "inside the directory");
    }

    // a pack that was changed

    [Fact]
    public async Task CheckNamesTheAssetsWhoseBytesChanged()
    {
        var folder = Temp("bundle");
        await BundleAsync(folder, 60, duplicates: 10);
        var outPath = Temp("game.opack");
        await PackAsync(folder, outPath);
        var bytes = await File.ReadAllBytesAsync(outPath, TestContext.Current.CancellationToken);
        var intact = await Open(bytes);
        Assert.Empty(await intact.FindCorruptAsync());

        // m3 is a blob that m53 shares, so both are reported.
        var directory = DirectoryOf(bytes);
        var hash = directory.Assets[AssetId.Parse("models/m3.glb")];
        var flipped = (byte[])bytes.Clone();
        flipped[directory.Blobs[hash].At + 1] ^= 0xff;

        var damaged = await Open(flipped);

        Assert.Equal(["models/m3.glb", "models/m53.glb"], (await damaged.FindCorruptAsync()).Select(id => id.ToString()));
    }

    // reading by id

    [Fact]
    public async Task AnAssetThatIsNotThereIsNotFound()
    {
        var folder = Temp("bundle");
        await BundleAsync(folder, 2);
        var outPath = Temp("game.opack");
        await PackAsync(folder, outPath);
        await using var file = new FilePackBytes(outPath);
        var opened = await AssetPack.OpenAsync(file);
        var missing = AssetId.Parse("models/missing.glb");

        await Assert.ThrowsAsync<AssetNotFoundException>(() => opened.ReadAsync(missing));
        Assert.False(await opened.ExistsAsync(missing));
        Assert.True(await opened.ExistsAsync(AssetId.Parse("models/m1.glb")));
    }

    // FilePackBytes

    [Fact]
    public async Task AnswersTwentyReadsStartedTogether()
    {
        var data = Enumerable.Range(0, 4096).Select(i => (byte)(i & 0xff)).ToArray();
        var path = Temp("data.bin");
        await File.WriteAllBytesAsync(path, data, TestContext.Current.CancellationToken);
        await using var source = new FilePackBytes(path);

        var reads = Enumerable.Range(0, 20).Select(i => source.ReadAsync(i * 100, 50)).ToList();
        var length = source.LengthAsync();
        var results = await Task.WhenAll(reads);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(data[(i * 100)..((i * 100) + 50)], results[i]);
        }

        Assert.Equal(4096, await length);
    }

    [Fact]
    public async Task AFailedReadDoesNotStopTheOnesBehindIt()
    {
        var path = Temp("data.bin");
        await File.WriteAllBytesAsync(path, new byte[100], TestContext.Current.CancellationToken);
        await using var source = new FilePackBytes(path);

        var tooFar = source.ReadAsync(90, 50);
        var fine = source.ReadAsync(0, 10);

        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() => tooFar);
        Assert.Equal(10, (await fine).Length);
    }

    [Fact]
    public async Task OpensAPackThatSitsInsideAnotherFile()
    {
        var folder = Temp("bundle");
        var assets = await BundleAsync(folder, 12);
        var outPath = Temp("game.opack");
        await PackAsync(folder, outPath);
        var token = TestContext.Current.CancellationToken;
        var inner = await File.ReadAllBytesAsync(outPath, token);
        var wrapped = Temp("app.apk");
        await File.WriteAllBytesAsync(
            wrapped, [.. Enumerable.Repeat((byte)0xaa, 1000), .. inner, .. Enumerable.Repeat((byte)0xbb, 77)], token);

        await using var file = new FilePackBytes(wrapped, start: 1000, bytes: inner.Length);
        var opened = await AssetPack.OpenAsync(file);

        foreach (var (key, value) in assets)
        {
            Assert.Equal(value, await opened.ReadAsync(key));
        }
    }
}

using System.Text;

namespace Orblit.Asset.Tests;

// What every content store has to do, whatever it keeps its bytes in.
public abstract class ContentStoreContract
{
    protected abstract IContentStore Make();

    [Fact]
    public async Task WhatIsPutCanBeGotBackByTheHashPutGives()
    {
        var store = Make();
        var hash = await store.PutAsync(Encoding.UTF8.GetBytes("abc"));
        Assert.Equal(ContentHash.Of(Encoding.UTF8.GetBytes("abc")), hash);
        Assert.Equal(Encoding.UTF8.GetBytes("abc"), await store.GetAsync(hash));
        Assert.True(await store.ContainsAsync(hash));
    }

    [Fact]
    public async Task AHashNothingWasPutUnderIsNotThere()
    {
        var store = Make();
        var hash = ContentHash.Of([1, 2, 3]);
        Assert.Null(await store.GetAsync(hash));
        Assert.False(await store.ContainsAsync(hash));
    }

    [Fact]
    public async Task NothingAtAllCanBePutAndGotBack()
    {
        var store = Make();
        var hash = await store.PutAsync(ReadOnlyMemory<byte>.Empty);
        Assert.Empty((await store.GetAsync(hash))!);
        Assert.True(await store.ContainsAsync(hash));
    }

    [Fact]
    public async Task ARemovedEntryIsGone()
    {
        var store = Make();
        var hash = await store.PutAsync(new byte[] { 1, 2, 3 });
        await store.RemoveAsync(hash);
        Assert.Null(await store.GetAsync(hash));
        Assert.False(await store.ContainsAsync(hash));
    }

    [Fact]
    public async Task RemovingWhatIsNotThereIsNotAnError() => await Make().RemoveAsync(ContentHash.Of([9]));

    [Fact]
    public async Task PuttingTheSameBytesTwiceGivesTheSameHash()
    {
        var store = Make();
        var first = await store.PutAsync(new byte[] { 1, 2, 3 });
        var second = await store.PutAsync(new byte[] { 1, 2, 3 });
        Assert.Equal(first, second);
        Assert.Equal(new byte[] { 1, 2, 3 }, await store.GetAsync(first));
    }

    [Fact]
    public async Task PuttingTheSameBytesManyTimesAtOnceIsSafe()
    {
        var store = Make();
        var bytes = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
        var hashes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.PutAsync(bytes)));
        Assert.Equal([ContentHash.Of(bytes)], hashes.ToHashSet());
        Assert.Equal(bytes, await store.GetAsync(hashes[0]));
    }

    [Fact]
    public async Task ChangingBytesAfterPuttingOrGettingThemChangesNothing()
    {
        var store = Make();
        var given = new byte[] { 1, 2, 3 };
        var hash = await store.PutAsync(given);
        given[0] = 9;

        var got = (await store.GetAsync(hash))!;
        got[1] = 9;
        Assert.Equal(new byte[] { 1, 2, 3 }, await store.GetAsync(hash));
    }
}

public class MemoryContentStoreTests : ContentStoreContract
{
    protected override IContentStore Make() => new MemoryContentStore();
}

public sealed class DirectoryContentStoreTests : ContentStoreContract, IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_asset_store_");

    public void Dispose() => _temp.Delete(recursive: true);

    protected override IContentStore Make() => new DirectoryContentStore(_temp.FullName);

    private string PathOf(ContentHash hash) => Path.Combine(_temp.FullName, hash.Shard, hash.Hex);

    private static List<string> FilesUnder(string path) =>
        Directory.Exists(path) ? [.. Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)] : [];

    [Fact]
    public async Task FilesAnEntryUnderItsShardNamedByItsHash()
    {
        var hash = await new DirectoryContentStore(_temp.FullName).PutAsync(Encoding.UTF8.GetBytes("abc"));
        var file = PathOf(hash);
        Assert.True(File.Exists(file));
        Assert.EndsWith($"{Path.DirectorySeparatorChar}ba{Path.DirectorySeparatorChar}{hash.Hex}", file);
        Assert.Equal(Encoding.UTF8.GetBytes("abc"), File.ReadAllBytes(file));
    }

    [Fact]
    public async Task AStoreWhoseDirectoryIsNotThereYetIsEmptyAndPutMakesIt()
    {
        var store = new DirectoryContentStore(Path.Combine(_temp.FullName, "not", "yet"));
        Assert.Null(await store.GetAsync(ContentHash.Of([1])));

        var hash = await store.PutAsync(new byte[] { 1 });
        Assert.Equal(new byte[] { 1 }, await store.GetAsync(hash));
    }

    [Fact]
    public async Task ManyPutsOfTheSameBytesAtOnceLeaveOneEntryAndNothingHalfWritten()
    {
        var store = new DirectoryContentStore(_temp.FullName);
        var bytes = Enumerable.Range(0, 65536).Select(i => (byte)(i % 253)).ToArray();
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => store.PutAsync(bytes)));

        Assert.Empty(FilesUnder(Path.Combine(_temp.FullName, ".incoming")));
        var stored = FilesUnder(_temp.FullName);
        Assert.Equal(bytes, File.ReadAllBytes(Assert.Single(stored)));
    }

    [Fact]
    public async Task TwoStoresOverOneDirectorySeeEachOthersEntries()
    {
        var writer = new DirectoryContentStore(_temp.FullName);
        var reader = new DirectoryContentStore(_temp.FullName);
        var hash = await writer.PutAsync(new byte[] { 4, 5, 6 });
        Assert.Equal(new byte[] { 4, 5, 6 }, await reader.GetAsync(hash));
    }

    [Fact]
    public async Task BytesDamagedOnDiskAreNoticedRemovedAndNotHandedBack()
    {
        var store = new DirectoryContentStore(_temp.FullName);
        var hash = await store.PutAsync(Encoding.UTF8.GetBytes("the right bytes"));
        File.WriteAllBytes(PathOf(hash), Encoding.UTF8.GetBytes("the wrong bytes"));

        Assert.True(await store.ContainsAsync(hash), "not read, so not checked");
        Assert.Null(await store.GetAsync(hash));
        Assert.False(File.Exists(PathOf(hash)));
        Assert.False(await store.ContainsAsync(hash));

        // And the next put of the right bytes puts them back.
        await store.PutAsync(Encoding.UTF8.GetBytes("the right bytes"));
        Assert.Equal(Encoding.UTF8.GetBytes("the right bytes"), await store.GetAsync(hash));
    }

    [Fact]
    public async Task AFileCutShortOnDiskIsNoticedToo()
    {
        var store = new DirectoryContentStore(_temp.FullName);
        var hash = await store.PutAsync(Enumerable.Range(0, 1000).Select(i => (byte)(i % 256)).ToArray());
        File.WriteAllBytes(PathOf(hash), Enumerable.Range(0, 500).Select(i => (byte)(i % 256)).ToArray());
        Assert.Null(await store.GetAsync(hash));
    }
}

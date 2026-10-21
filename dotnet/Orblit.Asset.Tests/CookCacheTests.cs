using System.Text;
using Orblit.Json;

namespace Orblit.Asset.Tests;

internal static class CookCases
{
    public static ContentHash Hash(string text) => ContentHash.Of(Encoding.UTF8.GetBytes(text));

    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    public static CookKey Key(string source = "wall.png", Dictionary<string, object?>? target = null) =>
        new("texture", 1, Hash(source), target: target);

    public static Dictionary<string, byte[]> Outputs(string name, string text) => new() { [name] = Utf8(text) };
}

// What every cook cache has to do, whatever it keeps its bytes in.
public abstract class CookCacheContract
{
    protected abstract ICookCache Make();

    [Fact]
    public async Task AKeyNothingWasCookedForIsAMiss() => Assert.Null(await Make().LookUpAsync(CookCases.Key()));

    [Fact]
    public async Task WhatWasStoredComesBackUnderTheSameKey()
    {
        var cache = Make();
        await cache.StoreAsync(CookCases.Key(), CookCases.Outputs("wall.ktx2", "cooked"));

        var found = await cache.LookUpAsync(CookCases.Key());
        Assert.NotNull(found);
        var output = Assert.Single(found.Outputs);
        Assert.Equal("wall.ktx2", output.Name);
        Assert.Equal(CookCases.Utf8("cooked"), await cache.ReadAsync(output.Hash));
    }

    // The point of the cache, said as a test.
    [Fact]
    public async Task ARebuildWithNothingChangedCooksNothing()
    {
        var cache = Make();
        var cooks = 0;

        async Task Build()
        {
            foreach (var name in new[] { "wall.png", "floor.png", "roof.png" })
            {
                var key = CookCases.Key(source: name);
                if (await cache.LookUpAsync(key) is not null)
                {
                    continue;
                }

                cooks++;
                await cache.StoreAsync(key, CookCases.Outputs($"{name}.ktx2", $"cooked {name}"));
            }
        }

        await Build();
        Assert.Equal(3, cooks);
        await Build();
        Assert.True(cooks == 3, "the second build should have cooked nothing");
    }

    [Fact]
    public async Task ChangingOneTextureRecooksOnlyThatTexture()
    {
        var cache = Make();
        foreach (var name in new[] { "wall.png", "floor.png" })
        {
            await cache.StoreAsync(CookCases.Key(source: name), CookCases.Outputs("out", name));
        }

        // wall.png was edited; floor.png was not.
        Assert.Null(await cache.LookUpAsync(CookCases.Key(source: "wall.png edited")));
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "floor.png")));
    }

    [Fact]
    public async Task EveryOutputOfOneCookTravelsWithTheOthers()
    {
        var cache = Make();
        await cache.StoreAsync(CookCases.Key(), new Dictionary<string, byte[]>
        {
            ["wall.astc.ktx2"] = CookCases.Utf8("astc"),
            ["wall.bc.ktx2"] = CookCases.Utf8("bc"),
            ["wall.etc2.ktx2"] = CookCases.Utf8("etc2"),
        });

        var found = (await cache.LookUpAsync(CookCases.Key()))!;
        Assert.Equal(["wall.astc.ktx2", "wall.bc.ktx2", "wall.etc2.ktx2"], found.Outputs.Select(output => output.Name));
        Assert.NotNull(found["wall.bc.ktx2"]);
        Assert.Null(found["wall.pvrtc.ktx2"]);
    }

    [Fact]
    public async Task OutputsComeBackInNameOrderHoweverTheyWereCooked()
    {
        var stored = await Make().StoreAsync(CookCases.Key(), new Dictionary<string, byte[]>
        {
            ["z"] = CookCases.Utf8("z"),
            ["a"] = CookCases.Utf8("a"),
        });
        Assert.Equal(["a", "z"], stored.Outputs.Select(output => output.Name));
    }

    [Fact]
    public async Task AnOutputKnowsHowBigItIsWithoutBeingRead()
    {
        var stored = await Make().StoreAsync(CookCases.Key(), CookCases.Outputs("out", "twelve bytes"));
        Assert.Equal(12, Assert.Single(stored.Outputs).Bytes);
        Assert.Equal(12, stored.Bytes);
    }

    // Two builds started at once, or an entry evicted between the look up and the cook. Neither
    // should fail on the way out.
    [Fact]
    public async Task StoringTheSameKeyTwiceIsNotAnError()
    {
        var cache = Make();
        await cache.StoreAsync(CookCases.Key(), CookCases.Outputs("out", "first"));
        await cache.StoreAsync(CookCases.Key(), CookCases.Outputs("out", "second"));
        var found = (await cache.LookUpAsync(CookCases.Key()))!;
        Assert.Equal(CookCases.Utf8("second"), await cache.ReadAsync(Assert.Single(found.Outputs).Hash));
    }

    [Fact]
    public async Task TwoCooksThatAgreeShareOneCopyOfTheBytes()
    {
        var cache = Make();
        var one = await cache.StoreAsync(CookCases.Key(target: new() { ["os"] = "macos" }), CookCases.Outputs("out", "the same cooked bytes"));
        var two = await cache.StoreAsync(CookCases.Key(target: new() { ["os"] = "ios" }), CookCases.Outputs("out", "the same cooked bytes"));
        Assert.Equal(Assert.Single(one.Outputs).Hash, Assert.Single(two.Outputs).Hash);
    }

    [Fact]
    public async Task ReadingBytesNothingStoredGivesNothing() =>
        Assert.Null(await Make().ReadAsync(CookCases.Hash("never stored")));

    // An importer can legitimately produce nothing, a scene with no textures in it. Running that
    // every build is the bug.
    [Fact]
    public async Task ACookWithNoOutputsAtAllIsStillAHit()
    {
        var cache = Make();
        await cache.StoreAsync(CookCases.Key(), new Dictionary<string, byte[]>());
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key()));
        Assert.Empty((await cache.LookUpAsync(CookCases.Key()))!.Outputs);
    }

    // remembering what would not cook

    [Fact]
    public async Task AKeyNothingFailedOnHasNothingAgainstIt() =>
        Assert.Null(await Make().LookUpFailureAsync(CookCases.Key()));

    [Fact]
    public async Task WhatWasRecordedComesBackWithWhy()
    {
        var cache = Make();
        await cache.RecordFailureAsync(CookCases.Key(), "not a png at all");

        var found = await cache.LookUpFailureAsync(CookCases.Key());
        Assert.NotNull(found);
        Assert.Equal("not a png at all", found.Reason);
    }

    [Fact]
    public async Task ItIsRememberedAgainstThatKeyAndNoOther()
    {
        var cache = Make();
        await cache.RecordFailureAsync(CookCases.Key(target: new() { ["os"] = "macos" }), "no good");
        Assert.Null(await cache.LookUpFailureAsync(CookCases.Key(target: new() { ["os"] = "ios" })));
        Assert.Null(await cache.LookUpFailureAsync(CookCases.Key(source: "other.png")));
    }

    [Fact]
    public async Task RecordingOneDoesNotMakeItAHit()
    {
        var cache = Make();
        await cache.RecordFailureAsync(CookCases.Key(), "no good");
        Assert.Null(await cache.LookUpAsync(CookCases.Key()));
    }

    [Fact]
    public async Task CookingItClearsTheFailure()
    {
        var cache = Make();
        await cache.RecordFailureAsync(CookCases.Key(), "the encoder was not installed");
        await cache.StoreAsync(CookCases.Key(), CookCases.Outputs("out", "cooked"));
        Assert.Null(await cache.LookUpFailureAsync(CookCases.Key()));
    }

    [Fact]
    public async Task RecordingTwiceKeepsTheNewerReason()
    {
        var cache = Make();
        await cache.RecordFailureAsync(CookCases.Key(), "first");
        await cache.RecordFailureAsync(CookCases.Key(), "second");
        Assert.Equal("second", (await cache.LookUpFailureAsync(CookCases.Key()))!.Reason);
    }
}

// in memory
public class MemoryCookCacheTests : CookCacheContract
{
    protected override ICookCache Make() => new MemoryCookCache();
}

// in a directory
public sealed class DirectoryCookCacheTests : CookCacheContract, IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_cook_cache_");
    private readonly string _root;

    public DirectoryCookCacheTests() => _root = Path.Combine(_temp.FullName, "cache");

    public void Dispose() => _temp.Delete(recursive: true);

    protected override ICookCache Make() => new DirectoryCookCache(_root);

    private string IndexPath => Path.Combine(_root, "index.json");

    private DirectoryCookCache Open() => new(_root);

    [Fact]
    public async Task WhatOneCacheStoredAnotherInTheSamePlaceFinds()
    {
        // The build after this one is a different process.
        await Open().StoreAsync(CookCases.Key(), CookCases.Outputs("out", "cooked"));
        var found = await Open().LookUpAsync(CookCases.Key());
        Assert.NotNull(found);
        Assert.Equal(CookCases.Utf8("cooked"), await Open().ReadAsync(Assert.Single(found.Outputs).Hash));
    }

    [Fact]
    public async Task TheDirectoryIsMadeWhenItIsFirstWrittenTo()
    {
        Assert.False(Directory.Exists(_root));
        await Open().StoreAsync(CookCases.Key(), CookCases.Outputs("out", "x"));
        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public async Task ALookUpInADirectoryThatDoesNotExistIsAMiss() => Assert.Null(await Open().LookUpAsync(CookCases.Key()));

    [Fact]
    public async Task AnIndexNobodyCanReadIsAnEmptyCacheNotABrokenOne()
    {
        await Open().StoreAsync(CookCases.Key(), CookCases.Outputs("out", "cooked"));

        await File.WriteAllTextAsync(IndexPath, "{ this is not json", TestContext.Current.CancellationToken);

        Assert.Null(await Open().LookUpAsync(CookCases.Key()));
        // And it recovers: the next store writes a readable index again.
        await Open().StoreAsync(CookCases.Key(), CookCases.Outputs("out", "c"));
        Assert.NotNull(await Open().LookUpAsync(CookCases.Key()));
    }

    [Fact]
    public async Task AnIndexFromANewerBuildIsAnEmptyCache()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(IndexPath, """{"formatVersion":99,"entries":{}}""", TestContext.Current.CancellationToken);
        Assert.Null(await Open().LookUpAsync(CookCases.Key()));
    }

    [Fact]
    public async Task OneUnreadableEntryDoesNotCostTheOthers()
    {
        var cache = Open();
        await cache.StoreAsync(CookCases.Key(source: "a"), CookCases.Outputs("out", "a"));
        await cache.StoreAsync(CookCases.Key(source: "b"), CookCases.Outputs("out", "b"));

        var index = (OrderedDictionary<string, object?>)DartJson.Decode(await File.ReadAllTextAsync(IndexPath, TestContext.Current.CancellationToken))!;
        var entries = (OrderedDictionary<string, object?>)index["entries"]!;
        entries[CookCases.Key(source: "a").Hash.Hex] = new Dictionary<string, object?>
        {
            ["usedAt"] = "not a time",
            ["outputs"] = new List<object?>(),
        };
        await File.WriteAllTextAsync(IndexPath, DartJson.Encode(index), TestContext.Current.CancellationToken);

        var reopened = Open();
        Assert.Null(await reopened.LookUpAsync(CookCases.Key(source: "a")));
        Assert.NotNull(await reopened.LookUpAsync(CookCases.Key(source: "b")));
    }

    [Fact]
    public async Task AnEntryWhoseBytesHaveGoneIsAMissNotABrokenHit()
    {
        var cache = Open();
        var stored = await cache.StoreAsync(CookCases.Key(), CookCases.Outputs("out", "cooked"));

        var hash = Assert.Single(stored.Outputs).Hash;
        File.Delete(Path.Combine(_root, "content", hash.Shard, hash.Hex));

        Assert.Null(await cache.LookUpAsync(CookCases.Key()));
    }

    [Fact]
    public async Task TheIndexIsWrittenWholeOrNotAtAll()
    {
        await Open().StoreAsync(CookCases.Key(), CookCases.Outputs("out", "cooked"));
        var text = await File.ReadAllTextAsync(IndexPath, TestContext.Current.CancellationToken);
        DartJson.Decode(text);
        Assert.EndsWith("\n", text);
        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(_root),
            entry => entry.EndsWith(".incoming", StringComparison.Ordinal));
    }

    // Whether they are two objects here or two processes, the index is read and written under one lock.
    [Fact]
    public async Task WritersInTheSameDirectoryDoNotLoseEachOthersEntries()
    {
        var caches = Enumerable.Range(0, 4).Select(_ => Open()).ToList();
        await Task.WhenAll(caches.Select((cache, i) =>
            cache.StoreAsync(CookCases.Key(source: $"file{i}"), CookCases.Outputs("out", $"cooked {i}"))));
        for (var i = 0; i < 4; i++)
        {
            Assert.True(
                await Open().LookUpAsync(CookCases.Key(source: $"file{i}")) is not null,
                $"file{i} was written and then lost");
        }
    }

    // Really three processes, because that is the claim the lock file makes and the one the
    // in-process queue cannot stand in for. Each runs this assembly with one worker fact selected.
    [Fact]
    public async Task TwoBuildsCookingAtOnceDoNotLoseEachOther()
    {
        var names = new[] { "first", "second", "third" };
        var runs = await Task.WhenAll(names.Select(name => CookCacheWorker.RunAsync(_root, name)));
        foreach (var (code, output) in runs)
        {
            Assert.True(code == 0, output);
        }

        foreach (var name in names)
        {
            Assert.True(
                await Open().LookUpAsync(CookCases.Key(source: name)) is not null,
                $"{name} was cooked by another process and then lost");
        }
    }

    // eviction

    private DateTimeOffset _now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    private DirectoryCookCache CacheOf(long limit) => new(_root, limit, () => _now);

    private static Task<CookedAsset> StoreOf(DirectoryCookCache cache, string name, int size) =>
        cache.StoreAsync(CookCases.Key(source: name), CookCases.Outputs("out", name.PadRight(size, '.')[..size]));

    [Fact]
    public async Task TheOldestEntryGoesFirst()
    {
        var cache = CacheOf(250);
        foreach (var name in new[] { "first", "second", "third" })
        {
            await StoreOf(cache, name, 100);
            _now = _now.AddDays(1);
        }

        Assert.Null(await cache.LookUpAsync(CookCases.Key(source: "first")));
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "second")));
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "third")));
    }

    [Fact]
    public async Task AnEntryUsedRecentlyOutlivesAnOlderOne()
    {
        var cache = CacheOf(250);
        await StoreOf(cache, "first", 100);
        _now = _now.AddDays(1);
        await StoreOf(cache, "second", 100);

        // A build asks for the first one again, which is what keeps it.
        _now = _now.AddDays(1);
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "first")));

        _now = _now.AddDays(1);
        await StoreOf(cache, "third", 100);
        Assert.Null(await cache.LookUpAsync(CookCases.Key(source: "second")));
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "first")));
    }

    // A single cook larger than the whole limit is still usable by the build that just made it.
    [Fact]
    public async Task TheEntryJustCookedIsNeverTheOneEvicted()
    {
        var cache = CacheOf(50);
        await StoreOf(cache, "enormous", 500);
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "enormous")));
    }

    [Fact]
    public async Task EvictedBytesAreTakenOffTheDisk()
    {
        var cache = CacheOf(150);
        var first = await cache.StoreAsync(CookCases.Key(source: "first"), CookCases.Outputs("out", new string('x', 100)));
        _now = _now.AddDays(1);
        await cache.StoreAsync(CookCases.Key(source: "second"), CookCases.Outputs("out", new string('y', 100)));

        var hash = Assert.Single(first.Outputs).Hash;
        Assert.False(File.Exists(Path.Combine(_root, "content", hash.Shard, hash.Hex)));
    }

    // Two targets cooked to the same bytes. Evicting one must not take the file out from under the
    // other, which would turn its entry into a hit that reads back nothing.
    [Fact]
    public async Task BytesAnotherEntryStillPointsAtAreKept()
    {
        var cache = CacheOf(150);
        var shared = CookCases.Utf8(new string('z', 100));
        await cache.StoreAsync(
            CookCases.Key(source: "first", target: new() { ["os"] = "macos" }),
            new Dictionary<string, byte[]> { ["out"] = shared });
        _now = _now.AddDays(1);
        await cache.StoreAsync(
            CookCases.Key(source: "first", target: new() { ["os"] = "ios" }),
            new Dictionary<string, byte[]> { ["out"] = shared });

        var kept = await cache.LookUpAsync(CookCases.Key(source: "first", target: new() { ["os"] = "ios" }));
        Assert.NotNull(kept);
        Assert.Equal(shared, await cache.ReadAsync(Assert.Single(kept.Outputs).Hash));
    }

    // Both entries fit in the limit only because they are one file on disk. Counting the bytes
    // twice would evict a cache never over it.
    [Fact]
    public async Task SharedBytesAreCountedOnceNotOncePerEntry()
    {
        var cache = CacheOf(150);
        var shared = new Dictionary<string, byte[]> { ["out"] = CookCases.Utf8(new string('z', 100)) };
        await cache.StoreAsync(CookCases.Key(source: "first"), shared);
        _now = _now.AddDays(1);
        await cache.StoreAsync(CookCases.Key(source: "second"), shared);
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "first")));
        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "second")));
    }

    [Fact]
    public async Task NothingIsEvictedWithoutALimit()
    {
        var cache = new DirectoryCookCache(_root, clock: () => _now);
        for (var i = 0; i < 20; i++)
        {
            await StoreOf(cache, $"file{i}", 1000);
            _now = _now.AddDays(1);
        }

        Assert.NotNull(await cache.LookUpAsync(CookCases.Key(source: "file0")));
    }

    [Fact]
    public void ALimitOfNothingIsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DirectoryCookCache(_root, 0));
        Assert.ThrowsAny<ArgumentException>(() => new DirectoryCookCache(_root, -1));
    }
}

// The other process of the cross-process test. A normal run has nothing for it to do.
public class CookCacheWorker
{
    private const string RootVariable = "ORBLIT_COOK_ROOT";
    private const string NameVariable = "ORBLIT_COOK_NAME";

    [Fact]
    public async Task StoresOneKeyForTheProcessThatStartedIt()
    {
        var root = Environment.GetEnvironmentVariable(RootVariable);
        var name = Environment.GetEnvironmentVariable(NameVariable);
        Assert.SkipUnless(root is not null && name is not null, "it only runs when another test starts it");
        await new DirectoryCookCache(root!).StoreAsync(CookCases.Key(source: name!), CookCases.Outputs("out", name!));
    }

    public static async Task<(int Code, string Output)> RunAsync(string root, string name)
    {
        var start = new System.Diagnostics.ProcessStartInfo(
            Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Orblit.Asset.Tests.exe" : "Orblit.Asset.Tests"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add($"{typeof(CookCacheWorker).FullName}.{nameof(StoresOneKeyForTheProcessThatStartedIt)}");
        start.Environment[RootVariable] = root;
        start.Environment[NameVariable] = name;
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await output + await error);
    }
}

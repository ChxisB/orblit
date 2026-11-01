namespace Orblit.Save.Tests;

public sealed class HistoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = Support.At(100L * 86_400_000);

    private readonly TempDir _dir = new();
    private readonly SaveHistory _history;

    public HistoryTests() => _history = new SaveHistory(_dir.Sub("history"));

    public void Dispose() => _dir.Dispose();

    private string[] Blobs()
    {
        var folder = Path.Combine(_dir.Path, "history", "blobs");
        return Directory.GetFiles(folder, "*.gz");
    }

    private string OnlyBlob() => Assert.Single(Blobs());

    private string OnlyEntry(string key) => Assert.Single(Directory.GetFiles($"{_dir.Sub("history")}/entries/{key}"));

    // A blob under a minute old is kept, so a test that expects one gone ages it.
    private void AgeBlobs()
    {
        foreach (var blob in Blobs())
        {
            File.SetLastWriteTimeUtc(blob, DateTime.UnixEpoch);
        }
    }

    private async Task SaveAt(string key, TimeSpan age, int value) =>
        await _history.RecordAsync(key, Support.RecordOf([(byte)value], at: Now.ToUnixTimeMilliseconds() - (long)age.TotalMilliseconds));

    private async Task<List<TimeSpan>> Ages(string key) =>
        [.. (await _history.ListAsync(key)).Select(v => Now - v.At)];

    // record and list

    [Fact]
    public async Task ListsTheVersionsOfAKeyNewestFirst()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1], at: 1000));
        await _history.RecordAsync("ana", Support.RecordOf([2], new Custom("pre-restore"), 3000));
        await _history.RecordAsync("ana", Support.RecordOf([3], new Leave(), 2000));

        var versions = await _history.ListAsync("ana");

        Assert.Equal([3000L, 2000L, 1000L], versions.Select(v => v.At.ToUnixTimeMilliseconds()));
        Assert.Equal<SaveReason>([new Custom("pre-restore"), new Leave(), new Autosave()], versions.Select(v => v.Reason));
        Assert.All(versions, v => Assert.Equal("ana", v.Key));
    }

    [Fact]
    public async Task LoadsTheBytesReasonAndTimeOfAVersion()
    {
        await _history.RecordAsync("ana", Support.RecordOf([4, 5, 6], new Shutdown(), 777));

        var record = await _history.LoadAsync(Assert.Single(await _history.ListAsync("ana")));

        Assert.Equal(new byte[] { 4, 5, 6 }, record.Bytes.ToArray());
        Assert.Equal(new Shutdown(), record.Reason);
        Assert.Equal(777, record.At.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task AnIdenticalSaveAddsAVersionAndNoCopy()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1, 2], at: 1000));
        AgeBlobs();
        await _history.RecordAsync("ana", Support.RecordOf([1, 2], at: 2000));
        await _history.RecordAsync("bo", Support.RecordOf([1, 2], at: 3000));

        Assert.True(
            new DateTimeOffset(File.GetLastWriteTimeUtc(OnlyBlob())).ToUnixTimeMilliseconds() == 0,
            "the copy was not written again");
        Assert.Equal(2, (await _history.ListAsync("ana")).Count);
        Assert.Single(Blobs());

        await _history.RecordAsync("ana", Support.RecordOf([1, 3], at: 4000));

        Assert.Equal(2, Blobs().Length);
    }

    [Fact]
    public async Task TwoSavesInOneMillisecondWithOtherBytesBothStay()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1], at: 1000));
        await _history.RecordAsync("ana", Support.RecordOf([2], at: 1000));

        Assert.Equal(2, (await _history.ListAsync("ana")).Count);
    }

    [Fact]
    public async Task VersionsOfOneMillisecondComeBackInOneFixedOrder()
    {
        for (byte i = 0; i < 8; i++)
        {
            await _history.RecordAsync("ana", Support.RecordOf([i], at: 1000));
        }

        var hashes = (await _history.ListAsync("ana")).Select(v => v.Hash).ToList();

        Assert.Equal(hashes.Order(StringComparer.Ordinal), hashes);
    }

    [Fact]
    public async Task KeysAreTheOnesWithAVersion()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1]));
        await _history.RecordAsync("bo", Support.RecordOf([2]));

        Assert.Equal(["ana", "bo"], (await _history.KeysAsync()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AnEmptyHistoryHasNoKeysAndAKeyWithoutVersionsHasNone()
    {
        Assert.Empty(await _history.KeysAsync());
        Assert.Empty(await _history.ListAsync("ana"));
    }

    [Fact]
    public async Task AKeyThatIsNotASaveKeyIsRefused()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _history.ListAsync("../x"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _history.RecordAsync("a/b", Support.RecordOf([1])));
    }

    // damage

    [Fact]
    public async Task ADamagedEntryNamesItsFile()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1], at: 1000));
        var entry = OnlyEntry("ana");
        await File.WriteAllTextAsync(entry, "{\"format\":1}", TestContext.Current.CancellationToken);

        var thrown = await Assert.ThrowsAnyAsync<FormatException>(() => _history.ListAsync("ana"));

        Assert.Contains(entry, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("damaged", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEntryFromALaterFormatIsRefused()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1], at: 1000));
        var entry = OnlyEntry("ana");
        var text = await File.ReadAllTextAsync(entry, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            entry,
            text.Replace("\"format\":1", "\"format\":2", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<FormatException>(() => _history.ListAsync("ana"));
    }

    [Fact]
    public async Task AMissingCopyIsReported()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1], at: 1000));
        File.Delete(OnlyBlob());

        var thrown = await Assert.ThrowsAnyAsync<FormatException>(
            async () => await _history.LoadAsync(Assert.Single(await _history.ListAsync("ana"))));

        Assert.Contains("missing", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACopyThatIsNotGzipIsReported()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1], at: 1000));
        await File.WriteAllBytesAsync(OnlyBlob(), [1, 2, 3], TestContext.Current.CancellationToken);

        var thrown = await Assert.ThrowsAnyAsync<FormatException>(
            async () => await _history.LoadAsync(Assert.Single(await _history.ListAsync("ana"))));

        Assert.Contains("damaged", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACopyThatHoldsOtherBytesIsReported()
    {
        await _history.RecordAsync("ana", Support.RecordOf([1], at: 1000));
        await _history.RecordAsync("bo", Support.RecordOf([2], at: 1000));
        var ana = Assert.Single(await _history.ListAsync("ana"));
        var bo = Assert.Single(await _history.ListAsync("bo"));
        var blobs = Path.Combine(_dir.Path, "history", "blobs");
        await File.WriteAllBytesAsync(
            Path.Combine(blobs, $"{ana.Hash}.gz"),
            await File.ReadAllBytesAsync(Path.Combine(blobs, $"{bo.Hash}.gz"), TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        var thrown = await Assert.ThrowsAnyAsync<FormatException>(() => _history.LoadAsync(ana));

        Assert.Contains("wrong hash", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultSpacingChangesAtADayAWeekAndAMonth()
    {
        Assert.Equal(TimeSpan.Zero, Spacings.Default(new TimeSpan(23, 59, 0)));
        Assert.Equal(TimeSpan.FromHours(1), Spacings.Default(TimeSpan.FromDays(1)));
        Assert.Equal(TimeSpan.FromHours(1), Spacings.Default(new TimeSpan(6, 23, 0, 0)));
        Assert.Equal(TimeSpan.FromDays(1), Spacings.Default(TimeSpan.FromDays(7)));
        Assert.Equal(TimeSpan.FromDays(1), Spacings.Default(new TimeSpan(29, 23, 0, 0)));
        Assert.Null(Spacings.Default(TimeSpan.FromDays(30)));
    }

    // prune

    [Fact]
    public async Task KeepsWhatTheDefaultSaysAndDropsTheRest()
    {
        TimeSpan[] kept =
        [
            TimeSpan.Zero,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(23),
            TimeSpan.FromHours(25),
            new(1, 2, 30, 0),
            TimeSpan.FromDays(8),
            new(9, 1, 0, 0),
        ];
        TimeSpan[] dropped =
        [
            new(1, 1, 30, 0),
            new(8, 2, 0, 0),
            TimeSpan.FromDays(31),
            TimeSpan.FromDays(40),
        ];
        var value = 0;
        foreach (var age in kept.Concat(dropped))
        {
            await SaveAt("ana", age, value++);
        }

        var removed = await _history.PruneAsync(Spacings.Default, Now);

        Assert.Equal(dropped.Length, removed);
        Assert.Equal(kept, await Ages("ana"));
    }

    [Fact]
    public async Task KeepsVersionsThatAreExactlyTheSpacingApart()
    {
        await SaveAt("ana", TimeSpan.FromHours(25), 1);
        await SaveAt("ana", TimeSpan.FromHours(26), 2);
        await SaveAt("ana", TimeSpan.FromHours(27), 3);

        Assert.Equal(0, await _history.PruneAsync(Spacings.Default, Now));
    }

    [Fact]
    public async Task LeavesAFileThatIsNotACopyAlone()
    {
        await SaveAt("ana", TimeSpan.Zero, 1);
        var stray = Path.Combine(_dir.Path, "history", "blobs", "notes.txt");
        await File.WriteAllTextAsync(stray, "keep me", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(stray, DateTime.UnixEpoch);

        await _history.PruneAsync(Spacings.Default, Now);

        Assert.True(File.Exists(stray));
    }

    [Fact]
    public async Task KeepsEveryVersionOfOneKeyForADay()
    {
        for (var minute = 0; minute < 50; minute++)
        {
            await SaveAt("ana", TimeSpan.FromMinutes(minute * 20), minute);
        }

        var inDay = (await Ages("ana")).Where(a => a < TimeSpan.FromDays(1)).ToList();

        await _history.PruneAsync(Spacings.Default, Now);

        Assert.Equal(inDay, (await Ages("ana")).Where(a => a < TimeSpan.FromDays(1)));
    }

    [Fact]
    public async Task TreatsEachKeyOnItsOwn()
    {
        await SaveAt("ana", TimeSpan.FromDays(8), 1);
        await SaveAt("bo", new TimeSpan(8, 2, 0, 0), 2);

        await _history.PruneAsync(Spacings.Default, Now);

        Assert.Single(await Ages("ana"));
        Assert.Single(await Ages("bo"));
    }

    [Fact]
    public async Task ASpacingThatDropsEverythingEmptiesTheHistory()
    {
        await SaveAt("ana", TimeSpan.Zero, 1);
        await SaveAt("bo", TimeSpan.FromHours(1), 2);
        AgeBlobs();

        var removed = await _history.PruneAsync(_ => null, Now);

        Assert.Equal(2, removed);
        Assert.Empty(await _history.ListAsync("ana"));
        Assert.Empty(Blobs());
    }

    [Fact]
    public async Task RemovesACopyOnceNoVersionNamesIt()
    {
        await SaveAt("ana", TimeSpan.FromDays(40), 1);
        await SaveAt("ana", TimeSpan.Zero, 2);
        AgeBlobs();

        await _history.PruneAsync(Spacings.Default, Now);

        Assert.Single(Blobs());
        var record = await _history.LoadAsync(Assert.Single(await _history.ListAsync("ana")));
        Assert.Equal(new byte[] { 2 }, record.Bytes.ToArray());
    }

    [Fact]
    public async Task KeepsACopyThatAKeptVersionOfAnotherKeyShares()
    {
        await SaveAt("ana", TimeSpan.FromDays(40), 1);
        await SaveAt("bo", TimeSpan.Zero, 1);
        AgeBlobs();

        await _history.PruneAsync(Spacings.Default, Now);

        Assert.Single(Blobs());
        var record = await _history.LoadAsync(Assert.Single(await _history.ListAsync("bo")));
        Assert.Equal(new byte[] { 1 }, record.Bytes.ToArray());
    }

    [Fact]
    public async Task KeepsAYoungCopyThatNoVersionNamesYet()
    {
        await SaveAt("ana", TimeSpan.Zero, 1);
        var blobs = Path.Combine(_dir.Path, "history", "blobs");
        var young = Path.Combine(blobs, $"{new string('a', 64)}.gz");
        var old = Path.Combine(blobs, $"{new string('b', 64)}.gz");
        await File.WriteAllBytesAsync(young, [0], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(old, [0], TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(old, (Now - TimeSpan.FromMinutes(5)).UtcDateTime);
        File.SetLastWriteTimeUtc(young, (Now - TimeSpan.FromSeconds(10)).UtcDateTime);

        await _history.PruneAsync(Spacings.Default, Now);

        Assert.True(File.Exists(young));
        Assert.False(File.Exists(old));
    }

    [Fact]
    public async Task AnEmptyHistoryPrunesNothing()
    {
        Assert.Equal(0, await _history.PruneAsync(Spacings.Default, Now));
    }
}

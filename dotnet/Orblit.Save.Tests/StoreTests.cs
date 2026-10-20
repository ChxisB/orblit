using System.Text;

namespace Orblit.Save.Tests;

public sealed class StoreTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private ISaveStore Make(string name) => name switch
    {
        "MemorySaveStore" => new MemorySaveStore(),
        "FileSaveStore" => new FileSaveStore(_dir.Sub("saves")),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "is not a store"),
    };

    private string PathOf(string key) => Path.Combine(_dir.Path, "saves", $"{key}.save");

    [Theory]
    [InlineData("MemorySaveStore")]
    [InlineData("FileSaveStore")]
    public async Task ReadsBackWhatWasWrittenWithItsReasonAndTime(string name)
    {
        var store = Make(name);
        await store.WriteAsync("ana", Support.RecordOf([1, 2, 3], new Custom("pre-restore"), 4321));

        var record = await store.ReadAsync("ana");

        Assert.NotNull(record);
        Assert.Equal(new byte[] { 1, 2, 3 }, record.Bytes.ToArray());
        Assert.Equal(new Custom("pre-restore"), record.Reason);
        Assert.Equal(4321, record.At.ToUnixTimeMilliseconds());
        Assert.Equal(TimeSpan.Zero, record.At.Offset);
    }

    [Theory]
    [InlineData("MemorySaveStore")]
    [InlineData("FileSaveStore")]
    public async Task AMissingKeyReadsAsNullAndDoesNotExist(string name)
    {
        var store = Make(name);

        Assert.Null(await store.ReadAsync("ana"));
        Assert.False(await store.ExistsAsync("ana"));
        Assert.Empty(await store.KeysAsync());
    }

    [Theory]
    [InlineData("MemorySaveStore")]
    [InlineData("FileSaveStore")]
    public async Task ASecondWriteReplacesTheFirst(string name)
    {
        var store = Make(name);
        await store.WriteAsync("ana", Support.RecordOf([1]));
        await store.WriteAsync("ana", Support.RecordOf([2, 2]));

        var record = await store.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 2, 2 }, record.Bytes.ToArray());
        Assert.True(await store.ExistsAsync("ana"));
        Assert.Equal(["ana"], await store.KeysAsync());
    }

    [Theory]
    [InlineData("MemorySaveStore")]
    [InlineData("FileSaveStore")]
    public async Task AnEmptySaveIsASave(string name)
    {
        var store = Make(name);
        await store.WriteAsync("ana", Support.RecordOf([]));

        var record = await store.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.True(record.Bytes.IsEmpty);
    }

    [Theory]
    [InlineData("MemorySaveStore")]
    [InlineData("FileSaveStore")]
    public async Task ListsEveryKey(string name)
    {
        var store = Make(name);
        await store.WriteAsync("ana", Support.RecordOf([1]));
        await store.WriteAsync("bo.2", Support.RecordOf([2]));

        Assert.Equal(["ana", "bo.2"], (await store.KeysAsync()).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("MemorySaveStore")]
    [InlineData("FileSaveStore")]
    public async Task AKeyThatIsNotAPlainNameIsRefused(string name)
    {
        var store = Make(name);
        string[] bad = ["", ".hidden", "../x", "a/b", @"a\b", "a b", new string('x', 129)];

        foreach (var key in bad)
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(() => store.WriteAsync(key, Support.RecordOf([1])));
        }

        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.ReadAsync("../x"));
    }

    [Fact]
    public async Task AWriteCutOffHalfwayLeavesTheOldSaveReadable()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        await store.WriteAsync("ana", Support.RecordOf([1, 2, 3]));
        // What a crash between the first byte and the rename leaves behind.
        await File.WriteAllBytesAsync($"{PathOf("ana")}.tmp", [0x4F, 0x53, 0x41], TestContext.Current.CancellationToken);

        var record = await store.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 1, 2, 3 }, record.Bytes.ToArray());
        Assert.Equal(["ana"], await store.KeysAsync());
    }

    [Fact]
    public async Task AWriteThatCannotStartLeavesTheOldSaveAsItWas()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        await store.WriteAsync("ana", Support.RecordOf([1, 2, 3]));
        Directory.CreateDirectory($"{PathOf("ana")}.tmp");

        var thrown = await Assert.ThrowsAnyAsync<Exception>(() => store.WriteAsync("ana", Support.RecordOf([9])));
        // Dart throws FileSystemException. .NET says UnauthorizedAccessException for a folder.
        Assert.True(thrown is IOException or UnauthorizedAccessException, thrown.ToString());

        var record = await store.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 1, 2, 3 }, record.Bytes.ToArray());
    }

    [Fact]
    public async Task AWriteAfterACutOffOneReplacesTheLeftover()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        await store.WriteAsync("ana", Support.RecordOf([1]));
        await File.WriteAllBytesAsync(
            $"{PathOf("ana")}.tmp",
            Enumerable.Repeat((byte)7, 500).ToArray(),
            TestContext.Current.CancellationToken);

        await store.WriteAsync("ana", Support.RecordOf([9]));

        var record = await store.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 9 }, record.Bytes.ToArray());
        Assert.False(File.Exists($"{PathOf("ana")}.tmp"));
    }

    [Fact]
    public async Task AWriteLeavesNoTemporaryFile()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        await store.WriteAsync("ana", Support.RecordOf([1]));

        Assert.Equal(
            ["ana.save"],
            Directory.GetFileSystemEntries(_dir.Sub("saves")).Select(p => Path.GetFileName(p)));
    }

    [Fact]
    public async Task ADamagedFileThrowsAndNamesTheKey()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        await store.WriteAsync("ana", Support.RecordOf([1, 2, 3]));
        byte[][] damaged =
        [
            [],
            [1, 2, 3],
            [0x4F, 0x53, 0x41, 0x56, 255, 0, 0, 0],
            [0x4F, 0x53, 0x41, 0x56, 2, 0, 0, 0, 0x7B, 0x7D],
            [0x58, 0x53, 0x41, 0x56, 0, 0, 0, 0],
        ];

        foreach (var bad in damaged)
        {
            await File.WriteAllBytesAsync(PathOf("ana"), bad, TestContext.Current.CancellationToken);

            var thrown = await Assert.ThrowsAnyAsync<FormatException>(() => store.ReadAsync("ana"));
            Assert.Contains("\"ana\"", thrown.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AFileThatIsWholeButHasTheWrongMagicIsRefused()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        await store.WriteAsync("ana", Support.RecordOf([1, 2, 3]));
        var bytes = await File.ReadAllBytesAsync(PathOf("ana"), TestContext.Current.CancellationToken);
        bytes[0] ^= 0xFF;
        await File.WriteAllBytesAsync(PathOf("ana"), bytes, TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<FormatException>(() => store.ReadAsync("ana"));
    }

    [Fact]
    public async Task AFileOfALaterFormatIsRefused()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        await store.WriteAsync("ana", Support.RecordOf([1]));
        var bytes = await File.ReadAllBytesAsync(PathOf("ana"), TestContext.Current.CancellationToken);
        var text = Encoding.Latin1.GetString(bytes).Replace("\"format\":1", "\"format\":2", StringComparison.Ordinal);
        await File.WriteAllBytesAsync(PathOf("ana"), Encoding.Latin1.GetBytes(text), TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<FormatException>(() => store.ReadAsync("ana"));
    }
}

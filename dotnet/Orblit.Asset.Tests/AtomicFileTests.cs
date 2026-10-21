namespace Orblit.Asset.Tests;

// A write through a temporary file removes only a file it made itself.
public sealed class AtomicFileTests : IDisposable
{
    private readonly KitWorkspace _work = new();

    public void Dispose() => _work.Dispose();

    [Fact]
    public async Task AFileAlreadyAtTheTemporaryNameIsRefusedAndKept()
    {
        var temporary = _work.At("target.partial");
        await File.WriteAllTextAsync(temporary, "not ours", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(
            async () => await AtomicFile.WriteAsync(temporary, _work.At("target"), new byte[] { 1 }));

        Assert.Equal("not ours", await File.ReadAllTextAsync(temporary, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(_work.At("target")));
    }

    [Fact]
    public async Task ATemporaryFileThatCannotBeMovedIsRemoved()
    {
        var temporary = _work.At("target.partial");
        Directory.CreateDirectory(_work.At("target"));

        await Assert.ThrowsAnyAsync<IOException>(
            async () => await AtomicFile.WriteAsync(temporary, _work.At("target"), new byte[] { 1 }));

        Assert.False(File.Exists(temporary));
    }
}

namespace Orblit.Save.Tests;

public sealed class HistoryCliTests : IAsyncLifetime
{
    private const string Early = "1970-01-01T00:00:02.000Z";
    private const string First = "1970-01-01T00:00:01.000Z";

    private readonly TempDir _dir = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();
    private HistoryStore? _store;

    private HistoryStore Store => _store ?? throw new InvalidOperationException("not set up");

    public async ValueTask InitializeAsync()
    {
        _store = HistoryStore.Files(_dir.Path);
        await _store.WriteAsync("ana", Support.RecordOf([1], at: 1000));
        await _store.WriteAsync("ana", Support.RecordOf([2], new Leave(), 3000));
    }

    public ValueTask DisposeAsync()
    {
        _dir.Dispose();
        _out.Dispose();
        _err.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<int> Run(params string[] args) =>
        HistoryCli.RunAsync([_dir.Path, .. args], _out, _err, Support.FixedClock);

    private async Task<byte[]> Current()
    {
        var record = await Store.ReadAsync("ana");
        Assert.NotNull(record);
        return record.Bytes.ToArray();
    }

    [Fact]
    public async Task ListWithNoKeyPrintsTheKeys()
    {
        Assert.Equal(0, await Run("list"));
        Assert.Equal("ana\n", _out.ToString());
    }

    [Fact]
    public async Task ListWithAKeyPrintsEachVersionNewestFirst()
    {
        Assert.Equal(0, await Run("list", "ana"));

        var lines = _out.ToString().Trim().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("1970-01-01T00:00:03.000Z  leave  ", lines[0], StringComparison.Ordinal);
        Assert.StartsWith($"{First}  autosave  ", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreWritesTheVersionWhenToldTheServerIsStopped()
    {
        Assert.Equal(0, await Run("restore", "ana", First, "--server-stopped"));

        Assert.Equal(new byte[] { 1 }, await Current());
        Assert.Equal("Restored ana.\n", _out.ToString());
    }

    [Fact]
    public async Task RestoreWithoutServerStoppedPrintsUsageAndWritesNothing()
    {
        Assert.Equal(64, await Run("restore", "ana", First));

        Assert.Equal(new byte[] { 2 }, await Current());
        Assert.Contains("--server-stopped", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreOfATimeWithNoVersionFailsWithItsMessage()
    {
        Assert.Equal(1, await Run("restore", "ana", Early, "--server-stopped"));

        Assert.Contains("has no version", _err.ToString(), StringComparison.Ordinal);
        Assert.Equal(new byte[] { 2 }, await Current());
    }

    [Fact]
    public async Task ATimeThatIsNotATimeFails()
    {
        Assert.Equal(1, await Run("restore", "ana", "soon", "--server-stopped"));
        Assert.NotEmpty(_err.ToString());
    }

    [Fact]
    public async Task RestoreAllListsWhatItWouldChangeAndWritesNothing()
    {
        Assert.Equal(0, await Run("restore-all", Early));

        Assert.Contains($"ana  {First}  autosave", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("1 to restore. Add --apply to write.", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal(new byte[] { 2 }, await Current());
    }

    [Fact]
    public async Task RestoreAllApplyWrites()
    {
        Assert.Equal(0, await Run("restore-all", Early, "--apply", "--server-stopped"));

        Assert.Equal(new byte[] { 1 }, await Current());
        Assert.Contains("Restored 1.", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreAllApplyWithoutServerStoppedIsRefused()
    {
        Assert.Equal(64, await Run("restore-all", Early, "--apply"));
        Assert.Equal(new byte[] { 2 }, await Current());
    }

    [Fact]
    public async Task WrongArgumentsPrintUsage()
    {
        Assert.Equal(64, await Run());
        Assert.Equal(64, await Run("dance"));
        Assert.Equal(64, await Run("list", "a", "b"));
        Assert.Equal(64, await Run("restore", "ana", "--server-stopped"));
        Assert.Equal(64, await Run("list", "--force"));
        // The tool is a dotnet tool, so its name replaces `dart run orblit_save:history`.
        Assert.Contains("orblit-save-history", _err.ToString(), StringComparison.Ordinal);
    }
}

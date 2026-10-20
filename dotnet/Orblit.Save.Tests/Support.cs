namespace Orblit.Save.Tests;

internal static class Support
{
    public static byte[] Bytes(params byte[] values) => values;

    public static SaveRecord RecordOf(byte[] values, SaveReason? reason = null, long at = 1000) =>
        new(values, reason ?? new Autosave(), At(at));

    public static DateTimeOffset At(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms);

    public static DateTimeOffset FixedClock() => At(5000);

    public static byte[] BytesOf(SaveRecord record) => record.Bytes.ToArray();

    public static bool Same(IEnumerable<string> expected, IReadOnlySet<string> actual) => actual.SetEquals(expected);

    /// <summary>Waits for a condition that another thread brings about.</summary>
    public static async Task Until(Func<bool> ready, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!ready())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"timed out waiting for {what}");
            }

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    public static async Task UntilAsync(Func<Task<bool>> ready, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!await ready())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"timed out waiting for {what}");
            }

            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}

/// <summary>A folder that goes when the test ends.</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir() => Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "orblit_save_test_" + Guid.NewGuid().ToString("N"));

    public string Path { get; }

    public string Sub(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

/// <summary>A store that holds each write until the test lets it through, or fails it.</summary>
internal sealed class Gated : ISaveStore
{
    private readonly Lock _lock = new();
    private readonly List<TaskCompletionSource> _gates = [];
    private bool _open;
    private bool _fail;
    private int _written;

    public MemorySaveStore Inner { get; } = new();

    public bool Fail
    {
        get { lock (_lock) { return _fail; } }
        set { lock (_lock) { _fail = value; } }
    }

    public int GateCount
    {
        get { lock (_lock) { return _gates.Count; } }
    }

    public int Written
    {
        get { lock (_lock) { return _written; } }
    }

    /// <summary>Lets the first write through. Later ones still wait.</summary>
    public void ReleaseFirst()
    {
        lock (_lock)
        {
            _gates[0].TrySetResult();
        }
    }

    /// <summary>Lets every held write through, and every later one.</summary>
    public void Open()
    {
        lock (_lock)
        {
            _open = true;
            foreach (var gate in _gates)
            {
                gate.TrySetResult();
            }

            _gates.Clear();
        }
    }

    public async Task WriteAsync(string key, SaveRecord record)
    {
        Task? wait = null;
        lock (_lock)
        {
            if (!_open)
            {
                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _gates.Add(gate);
                wait = gate.Task;
            }
        }

        if (wait is not null)
        {
            await wait;
        }

        if (Fail)
        {
            throw new InvalidOperationException("disk full");
        }

        await Inner.WriteAsync(key, record);
        lock (_lock)
        {
            _written++;
        }
    }

    public Task<SaveRecord?> ReadAsync(string key) => Inner.ReadAsync(key);

    public Task<bool> ExistsAsync(string key) => Inner.ExistsAsync(key);

    public Task<IReadOnlyList<string>> KeysAsync() => Inner.KeysAsync();
}

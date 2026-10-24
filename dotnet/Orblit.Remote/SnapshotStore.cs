namespace Orblit.Remote;

/// <summary>The last few snapshots, by the id each was given.</summary>
public sealed class SnapshotStore
{
    private readonly OrderedDictionary<string, IReadOnlyDictionary<string, object?>> _byId = [];
    private int _count;

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> for a capacity below one.</summary>
    public SnapshotStore(int capacity = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    public int Capacity { get; }

    /// <summary>The id of the snapshot added last. Throws <see cref="InvalidOperationException"/> before the first.</summary>
    public string LastId => _byId.Count == 0
        ? throw new InvalidOperationException("No snapshot has been added.")
        : _byId.GetAt(_byId.Count - 1).Key;

    /// <summary>Keeps <paramref name="snapshot"/> under a new id, and drops the oldest when full.</summary>
    public void Add(IReadOnlyDictionary<string, object?> snapshot)
    {
        _count++;
        _byId[$"s{_count}"] = snapshot;
        if (_byId.Count > Capacity)
        {
            _byId.RemoveAt(0);
        }
    }

    /// <summary>The snapshot kept under <paramref name="id"/>, or null when there is none.</summary>
    public IReadOnlyDictionary<string, object?>? Find(string id) => _byId.GetValueOrDefault(id);
}

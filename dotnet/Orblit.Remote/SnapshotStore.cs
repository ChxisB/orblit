namespace Orblit.Remote;

/// <summary>The last few snapshots, by the id each was given.</summary>
public sealed class SnapshotStore
{
    private const int MaxLabelLength = 40;

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

    /// <summary>
    /// Keeps <paramref name="snapshot"/> under <paramref name="label"/>, or under a new id when
    /// there is no label, and drops the oldest when full. A label taken again replaces the older
    /// snapshot and counts as the newest. Throws <see cref="ArgumentException"/> for a blank
    /// label, one over forty characters or one shaped like a new id, and keeps nothing.
    /// </summary>
    public void Add(IReadOnlyDictionary<string, object?> snapshot, string? label = null)
    {
        var id = label is null ? $"s{++_count}" : Named(label);
        _byId.Remove(id);
        _byId[id] = snapshot;
        if (_byId.Count > Capacity)
        {
            _byId.RemoveAt(0);
        }
    }

    /// <summary>The snapshot kept under <paramref name="id"/>, or null when there is none.</summary>
    public IReadOnlyDictionary<string, object?>? Find(string id) => _byId.GetValueOrDefault(id);

    // A label shaped like s3 could name a snapshot that a later automatic id has to take.
    private static string Named(string label)
    {
        var automatic = label.Length > 1 && label[0] == 's' && label[1..].All(char.IsAsciiDigit);
        return string.IsNullOrWhiteSpace(label) || label.Length > MaxLabelLength || automatic
            ? throw new ArgumentException($"label is text of 1 to {MaxLabelLength} characters that does not look like s3")
            : label;
    }
}

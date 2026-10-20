namespace Orblit.Save;

/// <summary>
/// What an operator does to saves: restore a version, restore many, edit one. A
/// save that <c>isLive</c> says a player is using is never touched, because the
/// game would write over it.
/// </summary>
public sealed class SaveTools(HistoryStore store, Func<string, bool> isLive, Func<DateTimeOffset> clock)
{
    /// <summary>The store the tools write through.</summary>
    public HistoryStore Store { get; } = store;

    /// <summary>
    /// Makes the version of <paramref name="key"/> saved at <paramref name="at"/> the current
    /// save. The save it replaces stays in the history as <c>pre-restore</c>.
    /// </summary>
    public async Task RestoreAsync(string key, DateTimeOffset at)
    {
        RefuseLive(key);
        var versions = await Store.History.ListAsync(key).ConfigureAwait(false);
        var entry = versions.FirstOrDefault(e => e.At == at)
            ?? throw new InvalidOperationException($"\"{key}\" has no version at {Times.Plain(at)}");
        await RestoreEntryAsync(entry).ConfigureAwait(false);
    }

    /// <summary>
    /// What restoring every save to its newest version at or before <paramref name="before"/>
    /// would change. Writes nothing.
    /// </summary>
    public async Task<RestorePlan> RestorePlanAsync(DateTimeOffset before)
    {
        var changes = new List<HistoryEntry>();
        var live = new List<string>();
        foreach (var key in await Store.History.KeysAsync().ConfigureAwait(false))
        {
            var versions = await Store.History.ListAsync(key).ConfigureAwait(false);
            var target = versions.FirstOrDefault(e => e.At <= before);
            if (target is null)
            {
                continue;
            }

            var current = await Store.ReadAsync(key).ConfigureAwait(false);
            if (current is not null && SaveHistory.HashOf(current.Bytes.Span) == target.Hash)
            {
                continue;
            }

            if (isLive(key))
            {
                live.Add(key);
            }
            else
            {
                changes.Add(target);
            }
        }

        return new RestorePlan(changes, live);
    }

    /// <summary>
    /// Writes every version in <paramref name="plan"/>. A key that has gone live since the plan
    /// was made stops it with an <see cref="InvalidOperationException"/>, and the keys before
    /// it stay done.
    /// </summary>
    public async Task ApplyRestoreAsync(RestorePlan plan)
    {
        foreach (var entry in plan.Changes)
        {
            RefuseLive(entry.Key);
            await RestoreEntryAsync(entry).ConfigureAwait(false);
        }
    }

    /// <summary>Saves what <paramref name="change"/> makes of the current bytes of <paramref name="key"/>, for <paramref name="reason"/>.</summary>
    public async Task EditAsync(string key, Func<byte[], byte[]> change, SaveReason reason)
    {
        RefuseLive(key);
        var current = await Store.ReadAsync(key).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"There is no save \"{key}\"");
        await Store.WriteAsync(key, new SaveRecord(change(current.Bytes.ToArray()), reason, clock())).ConfigureAwait(false);
    }

    private void RefuseLive(string key)
    {
        if (isLive(key))
        {
            throw new InvalidOperationException($"The save \"{key}\" is in use");
        }
    }

    private async Task RestoreEntryAsync(HistoryEntry entry)
    {
        var current = await Store.ReadAsync(entry.Key).ConfigureAwait(false);
        if (current is not null)
        {
            await Store.History
                .RecordAsync(entry.Key, new SaveRecord(current.Bytes.Span, new Custom("pre-restore"), clock()))
                .ConfigureAwait(false);
        }

        var version = await Store.History.LoadAsync(entry).ConfigureAwait(false);
        await Store.WriteAsync(entry.Key, new SaveRecord(version.Bytes.Span, new Custom("restore"), clock()))
            .ConfigureAwait(false);
    }
}

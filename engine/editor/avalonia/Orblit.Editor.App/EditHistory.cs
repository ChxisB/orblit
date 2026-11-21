namespace Orblit.Editor.App;

/// <summary>
/// One change the history can take back and make again. Forget runs when the history drops the change, and is told
/// whether the change was made at that point, so a kept entity that nothing can bring back is freed.
/// </summary>
internal sealed record Edit(Action Undo, Action Redo, Action<bool>? Forget = null);

/// <summary>The edits of one action, what the status line called it and when it was made.</summary>
internal sealed record EditStep(string Label, DateTimeOffset At, IReadOnlyList<Edit> Edits);

/// <summary>
/// The scene's edits since it opened. Edits gather until <see cref="Commit"/> makes them one step. Undo takes the last
/// done step back and redo makes the next undone one again. A new step drops the undone ones.
/// </summary>
internal sealed class EditHistory(TimeProvider time)
{
    readonly List<Edit> _pending = [];
    readonly List<EditStep> _steps = [];

    /// <summary>Raised after a step was added, taken back, made again or dropped.</summary>
    public event Action? Changed;

    /// <summary>Every step, oldest first. The first <see cref="Done"/> of them are made and the rest are undone.</summary>
    public IReadOnlyList<EditStep> Steps => _steps;

    public int Done { get; private set; }

    public bool CanUndo => Done > 0;

    public bool CanRedo => Done < _steps.Count;

    public void Add(Edit edit) => _pending.Add(edit);

    /// <summary>Makes the edits since the last commit one step. Nothing happens when there are none.</summary>
    public void Commit(string label)
    {
        if (_pending.Count == 0)
        {
            return;
        }
        DropFrom(Done);
        _steps.Add(new EditStep(label, time.GetLocalNow(), [.. _pending]));
        _pending.Clear();
        Done = _steps.Count;
        Changed?.Invoke();
    }

    /// <summary>Takes steps back or makes them again until this many are done.</summary>
    public void GoTo(int done)
    {
        done = Math.Clamp(done, 0, _steps.Count);
        while (Done > done)
        {
            Done--;
            foreach (var edit in _steps[Done].Edits.Reverse())
            {
                edit.Undo();
            }
        }
        while (Done < done)
        {
            foreach (var edit in _steps[Done].Edits)
            {
                edit.Redo();
            }
            Done++;
        }
        Changed?.Invoke();
    }

    /// <summary>Drops every step, as when another scene opens.</summary>
    public void Clear()
    {
        foreach (var edit in _pending)
        {
            edit.Forget?.Invoke(true);
        }
        _pending.Clear();
        DropFrom(0);
        Done = 0;
        Changed?.Invoke();
    }

    void DropFrom(int first)
    {
        for (var i = first; i < _steps.Count; i++)
        {
            foreach (var edit in _steps[i].Edits)
            {
                edit.Forget?.Invoke(i < Done);
            }
        }
        _steps.RemoveRange(first, _steps.Count - first);
    }
}

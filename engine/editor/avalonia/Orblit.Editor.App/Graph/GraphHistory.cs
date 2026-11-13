using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What the history needs from a script: the whole script as text and a way to put it back. The history owns this
/// interface. A restore raises <see cref="Changed"/> like any edit does.
/// </summary>
internal interface IGraphSnapshots
{
    event Action? Changed;

    string GetSnapshot();

    GraphEditResult Restore(string snapshot);
}

/// <summary>
/// Undo and redo for one open script. Each edit the engine takes is one entry, and the edits made inside
/// <see cref="Group"/> are one entry together, so a drag of five steps is undone in one go. An entry is the
/// whole script as it was, so undo needs no inverse of any call. The history belongs to the open file and is gone with it.
/// </summary>
// shortcut: every entry holds the whole script as text and a history keeps the last 200, so a script of many
// thousands of steps takes memory and a snapshot of its own per edit. Keep a diff per entry when that shows.
internal sealed class GraphHistory
{
    const int Limit = 200;

    readonly IGraphSnapshots _source;
    readonly List<string> _undo = [];
    readonly List<string> _redo = [];
    string _current;
    int _depth;
    bool _restoring;

    public GraphHistory(IGraphSnapshots source)
    {
        _source = source;
        _current = source.GetSnapshot();
        source.Changed += OnChanged;
    }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Puts the script back to what it was before the last entry. Nothing to undo is a refusal.</summary>
    public GraphEditResult Undo() => Move(_undo, _redo, "There is nothing to undo.");

    /// <summary>Takes back the last undo. Nothing to redo is a refusal.</summary>
    public GraphEditResult Redo() => Move(_redo, _undo, "There is nothing to redo.");

    /// <summary>Runs the edits and records what they changed as one entry. A nested group joins the one around it.</summary>
    public void Group(Action edits)
    {
        _depth++;
        try
        {
            edits();
        }
        finally
        {
            _depth--;
            if (_depth == 0)
            {
                Record();
            }
        }
    }

    void OnChanged()
    {
        if (!_restoring && _depth == 0)
        {
            Record();
        }
    }

    // An edit that left the text as it was, such as the same value set again, is not an entry.
    void Record()
    {
        var now = _source.GetSnapshot();
        if (now == _current)
        {
            return;
        }

        _undo.Add(_current);
        if (_undo.Count > Limit)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
        _current = now;
    }

    GraphEditResult Move(List<string> from, List<string> to, string nothing)
    {
        if (from.Count == 0)
        {
            return new GraphEditResult(33, nothing);
        }

        var target = from[^1];
        _restoring = true;
        GraphEditResult result;
        try
        {
            result = _source.Restore(target);
        }
        finally
        {
            _restoring = false;
        }

        if (result.Ok)
        {
            from.RemoveAt(from.Count - 1);
            to.Add(_current);
            _current = target;
        }

        return result;
    }
}

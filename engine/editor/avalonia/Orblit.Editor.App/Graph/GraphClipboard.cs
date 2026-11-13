using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>Where copied steps go as text. The clipboard of the window in the editor, and a string in a test.</summary>
internal interface ITextClipboard
{
    Task<string?> GetText();

    Task SetText(string text);
}

/// <summary>The clipboard of the window a control is in. A control in no window has none, so it holds nothing.</summary>
internal sealed class WindowClipboard(Control owner) : ITextClipboard
{
    public async Task<string?> GetText() => TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard ? await clipboard.TryGetTextAsync() : null;

    public async Task SetText(string text)
    {
        if (TopLevel.GetTopLevel(owner)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}

/// <summary>
/// Copy, cut, paste and duplicate for the selected steps. The engine writes the text of a copy and reads it back, so
/// a paste into another script brings the variables, signals and functions the steps name. A paste that would
/// change something the script already has is held until a person picks keep both, reuse or cancel.
/// </summary>
internal sealed class GraphClipboard(IGraphSource source, GraphScene scene, GraphEdits edits, ITextClipboard text)
{
    const long InvalidData = 30;
    const long AlreadyExists = 32;
    const double DuplicateOffset = 40;
    const string NothingToCopy = "Select the steps to copy. The entry step cannot be copied.";
    const string NoSteps = "The clipboard holds no steps.";
    const string NoClipboard = "The clipboard is not available.";

    sealed record Pending(string Payload, Point At);

    Pending? _pending;

    /// <summary>Raised with the engine's reason when a copy or paste cannot be done.</summary>
    public event Action<string>? Refused;

    /// <summary>Raised when a paste waits for a choice, with the names that differ from what the script has.</summary>
    public event Action<IReadOnlyList<GraphPasteClash>>? Conflicted;

    /// <summary>Raised when the choice is made or dropped, so the question can go away.</summary>
    public event Action? ChoiceClosed;

    /// <summary>Raised with the ids of the steps a paste or a duplicate made.</summary>
    public event Action<IReadOnlyList<int>>? Pasted;

    /// <summary>Whether a paste waits for a choice.</summary>
    public bool HasChoice => _pending is not null;

    /// <summary>Puts the selected steps on the clipboard. False when nothing could be copied.</summary>
    public async Task<bool> Copy()
    {
        var ids = Copyable();
        if (ids.Count == 0)
        {
            Refused?.Invoke(NothingToCopy);
            return false;
        }

        try
        {
            await text.SetText(source.GetPayload(ids));
            return true;
        }
        catch (Exception) // the platform clipboard can fail in ways that cannot be listed, and a failed copy must not end the editor
        {
            Refused?.Invoke(NoClipboard);
            return false;
        }
    }

    /// <summary>Copies the selection, then removes it as a delete does.</summary>
    public async Task Cut()
    {
        if (await Copy())
        {
            edits.DeleteSelection();
        }
    }

    /// <summary>Puts what the clipboard holds in the function on show, with its upper left step at the point.</summary>
    public async Task Paste(Point at)
    {
        string? held;
        try
        {
            held = await text.GetText();
        }
        catch (Exception) // as in Copy
        {
            Refused?.Invoke(NoClipboard);
            return;
        }

        Place(held ?? "", at, GraphPasteChoice.Ask);
    }

    /// <summary>Copies the selected steps next to where they are, without touching the clipboard.</summary>
    public void Duplicate()
    {
        var ids = Copyable();
        if (ids.Count == 0)
        {
            Refused?.Invoke(NothingToCopy);
            return;
        }

        var views = ids.Select(scene.Find).OfType<StepView>().ToList();
        var corner = new Point(views.Min(view => view.Step.X) + DuplicateOffset, views.Min(view => view.Step.Y) + DuplicateOffset);
        Place(source.GetPayload(ids), corner, GraphPasteChoice.Ask);
    }

    /// <summary>Finishes the paste that waits for a choice. Without one waiting this does nothing.</summary>
    public void Resolve(GraphPasteChoice choice)
    {
        if (_pending is { } pending)
        {
            Place(pending.Payload, pending.At, choice);
        }
    }

    /// <summary>Drops the paste that waits for a choice. The script is as it was.</summary>
    public void Cancel()
    {
        if (_pending is not null)
        {
            _pending = null;
            ChoiceClosed?.Invoke();
        }
    }

    // The entry step is never copied, so a selection of only the entry has nothing to put on the clipboard.
    List<int> Copyable() => [.. scene.SelectedSteps.Where(id => scene.Find(id)?.Step.Kind is { } kind && kind != "entry").Order()];

    void Place(string payload, Point at, GraphPasteChoice choice)
    {
        Cancel();
        var paste = source.PasteSteps(payload, at.X, at.Y, choice);
        if (paste.Result.Code == AlreadyExists && paste.Clashes.Count > 0)
        {
            _pending = new Pending(payload, at);
            Conflicted?.Invoke(paste.Clashes);
        }
        else if (!paste.Result.Ok)
        {
            Refused?.Invoke(paste.Result.Code == InvalidData ? NoSteps : paste.Result.Reason);
        }
        else
        {
            Pasted?.Invoke(paste.Steps);
        }
    }
}

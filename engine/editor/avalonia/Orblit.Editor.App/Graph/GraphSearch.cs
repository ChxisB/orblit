namespace Orblit.Editor.App.Graph;

/// <summary>
/// Moves the view to a step by a search, an id or a bookmark, and keeps the bookmarks. Each answer is reported as
/// a line of text for the find bar. Moving the view is not an edit, so nothing here reaches the engine.
/// </summary>
internal sealed class GraphSearch(GraphScene scene, Action<int> reveal, Action redraw)
{
    /// <summary>Raised with a line that says what the last command found, or empty after one that needs no words.</summary>
    public event Action<string>? Reported;

    /// <summary>Shows the next step that holds the text, or the previous one. It starts from the selected step.</summary>
    public void Find(string text, bool forward)
    {
        var ids = StepSearch.Find(scene.Steps, text);
        if (StepSearch.Cycle(ids, Current(), forward) is not { } id)
        {
            Reported?.Invoke("No step matches. Search looks at names and numbers.");
            return;
        }

        reveal(id);
        Reported?.Invoke($"{ids.IndexOf(id) + 1} of {ids.Count}");
    }

    /// <summary>Shows the step with this id, typed as a whole number.</summary>
    public void GoTo(string text)
    {
        if (!int.TryParse(text.Trim(), out var id))
        {
            Reported?.Invoke("Type a step number, like 12.");
        }
        else if (scene.Find(id) is null)
        {
            Reported?.Invoke($"No step has the number {id}.");
        }
        else
        {
            reveal(id);
            Reported?.Invoke("");
        }
    }

    /// <summary>Marks the one selected step, or takes its mark away.</summary>
    public void ToggleBookmark()
    {
        if (scene.SelectedSteps.Count != 1)
        {
            Reported?.Invoke("Pick one step to bookmark.");
            return;
        }

        var id = scene.SelectedSteps.First();
        redraw();
        Reported?.Invoke(scene.ToggleBookmark(id) ? $"Bookmarked step {id}." : $"Took the bookmark off step {id}.");
    }

    /// <summary>Sets a breakpoint on the one selected step, or takes it off.</summary>
    public void ToggleBreakpoint()
    {
        if (scene.SelectedSteps.Count != 1)
        {
            Reported?.Invoke("Pick one step to pause at.");
            return;
        }

        var id = scene.SelectedSteps.First();
        redraw();
        Reported?.Invoke(scene.ToggleBreakpoint(id) ? $"The game pauses at step {id}." : $"The game no longer pauses at step {id}.");
    }

    /// <summary>Shows the next bookmarked step, or the previous one, wrapping at the ends.</summary>
    public void StepBookmark(bool forward)
    {
        var ids = StepSearch.InReadingOrder(scene.Steps.Where(step => scene.Bookmarked.Contains(step.Id)));
        if (StepSearch.Cycle(ids, Current(), forward) is not { } id)
        {
            Reported?.Invoke("No bookmarks yet. Bookmark a step with ⌘F2.");
            return;
        }

        reveal(id);
        Reported?.Invoke($"Bookmark {ids.IndexOf(id) + 1} of {ids.Count}");
    }

    int? Current() => scene.SelectedSteps.Count == 1 ? scene.SelectedSteps.First() : null;
}

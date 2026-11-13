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
            Reported?.Invoke("No steps match.");
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
            Reported?.Invoke("Type a step id, a whole number.");
        }
        else if (scene.Find(id) is null)
        {
            Reported?.Invoke($"This function has no step {id}.");
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
            Reported?.Invoke("Select one step to bookmark.");
            return;
        }

        var id = scene.SelectedSteps.First();
        redraw();
        Reported?.Invoke(scene.ToggleBookmark(id) ? $"Bookmarked step {id}." : $"Took the bookmark off step {id}.");
    }

    /// <summary>Shows the next bookmarked step, or the previous one, wrapping at the ends.</summary>
    public void StepBookmark(bool forward)
    {
        var ids = StepSearch.InReadingOrder(scene.Steps.Where(step => scene.Bookmarked.Contains(step.Id)));
        if (StepSearch.Cycle(ids, Current(), forward) is not { } id)
        {
            Reported?.Invoke("No bookmarks.");
            return;
        }

        reveal(id);
        Reported?.Invoke($"Bookmark {ids.IndexOf(id) + 1} of {ids.Count}");
    }

    int? Current() => scene.SelectedSteps.Count == 1 ? scene.SelectedSteps.First() : null;
}

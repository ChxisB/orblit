namespace Orblit.Editor.App.Tests;

public sealed class EditHistoryTests
{
    readonly List<string> _log = [];
    readonly EditHistory _history = new(new ManualTime());

    Edit Logged(string name) => new(() => _log.Add($"undo {name}"), () => _log.Add($"redo {name}"), done => _log.Add($"forget {name} {done}"));

    void Step(string label, params string[] edits)
    {
        foreach (var edit in edits)
        {
            _history.Add(Logged(edit));
        }

        _history.Commit(label);
    }

    [Fact]
    public void A_commit_makes_the_pending_edits_one_step_that_undoes_in_reverse_and_redoes_in_order()
    {
        Step("Moved Box", "a", "b");

        _history.GoTo(0);
        _history.GoTo(1);

        Assert.Equal(["undo b", "undo a", "redo a", "redo b"], _log);
        Assert.Equal(["Moved Box"], _history.Steps.Select(step => step.Label));
        Assert.Equal(ManualTime.Start, _history.Steps[0].At);
    }

    [Fact]
    public void A_commit_with_nothing_pending_adds_no_step()
    {
        _history.Commit("Moved Box");

        Assert.Empty(_history.Steps);
        Assert.False(_history.CanUndo);
    }

    [Fact]
    public void A_new_step_drops_the_undone_steps_as_not_done()
    {
        Step("One", "a");
        Step("Two", "b");
        _history.GoTo(1);

        Step("Three", "c");

        Assert.Equal(["One", "Three"], _history.Steps.Select(step => step.Label));
        Assert.Contains("forget b False", _log);
        Assert.False(_history.CanRedo);
    }

    [Fact]
    public void Clear_drops_every_step_and_tells_each_whether_it_was_done()
    {
        Step("One", "a");
        Step("Two", "b");
        _history.GoTo(1);
        _history.Add(Logged("c"));

        _history.Clear();

        Assert.Equal(["undo b", "forget c True", "forget a True", "forget b False"], _log);
        Assert.Empty(_history.Steps);
        Assert.Equal(0, _history.Done);
    }

    [Fact]
    public void GoTo_stays_inside_the_steps()
    {
        Step("One", "a");

        _history.GoTo(5);
        Assert.Equal(1, _history.Done);
        _history.GoTo(-3);
        Assert.Equal(0, _history.Done);
    }
}

using Avalonia;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class GraphHistoryTests
{
    // A script that is one number. It refuses a restore on demand, as the engine does when it cannot read a text.
    sealed class Counter : IGraphSnapshots
    {
        public int Value { get; private set; }

        public bool RefuseRestore { get; set; }

        public event Action? Changed;

        public void Set(int value)
        {
            Value = value;
            Changed?.Invoke();
        }

        public string GetSnapshot() => Value.ToString();

        public GraphEditResult Restore(string snapshot)
        {
            if (RefuseRestore)
            {
                return new GraphEditResult(30, "The text is not a copy of steps.");
            }

            Value = int.Parse(snapshot);
            Changed?.Invoke();
            return new GraphEditResult(0, "");
        }
    }

    static string TextOf(FakeGraphSource source) => source.Function!.Steps.Single(step => step.Id == 3).Parameters["text"];

    [Fact]
    public void Ten_edits_undo_one_by_one_and_redo_to_the_same_documents()
    {
        var source = FakeGraphSource.Sample();
        var history = source.History;
        var snapshots = new List<string> { source.GetSnapshot() };
        for (var edit = 0; edit < 10; edit++)
        {
            Assert.True(source.SetStepParameter(3, "text", $"\"edit {edit}\"").Ok);
            snapshots.Add(source.GetSnapshot());
        }

        for (var back = 9; back >= 0; back--)
        {
            Assert.True(history.Undo().Ok);
            Assert.Equal(snapshots[back], source.GetSnapshot());
        }

        Assert.Equal("\"hi\"", TextOf(source));
        for (var forward = 1; forward <= 10; forward++)
        {
            Assert.True(history.Redo().Ok);
            Assert.Equal(snapshots[forward], source.GetSnapshot());
        }

        Assert.Equal("\"edit 9\"", TextOf(source));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void The_edits_of_a_group_are_one_entry()
    {
        var source = FakeGraphSource.Sample();
        var before = source.GetSnapshot();
        var history = source.History;

        history.Group(() =>
        {
            source.MoveStep(2, 300, 40);
            source.MoveStep(3, 560, 40);
        });

        Assert.True(history.Undo().Ok);
        Assert.Equal(before, source.GetSnapshot());
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void A_group_inside_a_group_joins_it()
    {
        var source = FakeGraphSource.Sample();
        var history = source.History;

        history.Group(() =>
        {
            source.MoveStep(2, 300, 40);
            history.Group(() => source.MoveStep(3, 560, 40));
            source.MoveStep(1, -20, 0);
        });

        history.Undo();
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void A_group_that_throws_still_records_what_it_changed()
    {
        var source = FakeGraphSource.Sample();
        var history = source.History;

        Assert.Throws<InvalidOperationException>(() => history.Group(() =>
        {
            source.MoveStep(2, 300, 40);
            throw new InvalidOperationException();
        }));

        Assert.True(history.CanUndo);
        Assert.True(history.Undo().Ok);
        Assert.Equal(260, source.Function!.Steps.Single(step => step.Id == 2).X);
    }

    [Fact]
    public void A_new_edit_after_an_undo_clears_the_redo_branch()
    {
        var source = FakeGraphSource.Sample();
        var history = source.History;
        source.SetStepParameter(3, "text", "\"one\"");
        source.SetStepParameter(3, "text", "\"two\"");
        history.Undo();
        Assert.True(history.CanRedo);

        source.SetStepParameter(3, "text", "\"three\"");

        Assert.False(history.CanRedo);
        Assert.False(history.Redo().Ok);
        Assert.Equal("\"three\"", TextOf(source));
    }

    [Fact]
    public void An_edit_that_changes_nothing_is_not_an_entry()
    {
        var source = FakeGraphSource.Sample();
        var history = source.History;

        source.SetStepParameter(3, "text", "\"hi\"");

        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Undo_and_redo_with_nothing_to_do_are_refused_and_change_nothing()
    {
        var source = FakeGraphSource.Sample();
        var before = source.GetSnapshot();
        var history = source.History;

        var undo = history.Undo();
        var redo = history.Redo();

        Assert.False(undo.Ok);
        Assert.Equal("There is nothing to undo.", undo.Reason);
        Assert.False(redo.Ok);
        Assert.Equal("There is nothing to redo.", redo.Reason);
        Assert.Equal(before, source.GetSnapshot());
    }

    [Fact]
    public void A_restore_the_script_refuses_keeps_the_entry_for_another_try()
    {
        var counter = new Counter();
        var history = new GraphHistory(counter);
        counter.Set(1);
        counter.RefuseRestore = true;

        var refused = history.Undo();

        Assert.False(refused.Ok);
        Assert.True(history.CanUndo);
        Assert.Equal(1, counter.Value);
        counter.RefuseRestore = false;
        Assert.True(history.Undo().Ok);
        Assert.Equal(0, counter.Value);
    }

    [Fact]
    public void The_history_keeps_the_last_two_hundred_entries()
    {
        var counter = new Counter();
        var history = new GraphHistory(counter);
        for (var value = 1; value <= 205; value++)
        {
            counter.Set(value);
        }

        var undone = 0;
        while (history.Undo().Ok)
        {
            undone++;
        }

        Assert.Equal(200, undone);
        Assert.Equal(5, counter.Value);
    }

    [Fact]
    public void A_drag_of_selected_steps_through_the_canvas_is_one_undo()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            var canvas = new GraphCanvas(source);

            canvas.Edits.MoveSteps([2, 3], new Vector(30, 10));
            Assert.Equal(290, source.Function!.Steps.Single(step => step.Id == 2).X);
            source.History.Undo();

            var steps = source.Function!.Steps;
            Assert.Equal(260, steps.Single(step => step.Id == 2).X);
            Assert.Equal(520, steps.Single(step => step.Id == 3).X);
            Assert.False(source.History.CanUndo);
        });
    }

    [Fact]
    public void Deleting_a_selection_through_the_canvas_is_one_undo_that_brings_back_steps_and_wires()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            var canvas = new GraphCanvas(source);
            canvas.Scene.SelectStep(2, false);
            canvas.Scene.SelectStep(3, true);

            canvas.Edits.DeleteSelection();
            Assert.Single(source.Function!.Steps);
            source.History.Undo();

            Assert.Equal(3, source.Function!.Steps.Count);
            Assert.Equal(2, source.Function.Wires.Count);
        });
    }
}

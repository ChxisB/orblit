using Avalonia.Controls;
using Orblit.Editor.App.Graph;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class FoldTests
{
    static GraphCanvas Open(FakeGraphSource source, params int[] selected)
    {
        _ = source.History;
        var canvas = new GraphCanvas(source);
        canvas.Select(selected);
        return canvas;
    }

    static void Click(GraphCanvas canvas, string id) => canvas.Find<Button>($"arrange:{id}")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public void To_function_asks_the_source_to_collapse_the_selection_and_selects_the_call_in_one_undo()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            var canvas = Open(source, 2, 3);
            var before = source.GetSnapshot();

            Click(canvas, "collapse");

            Assert.Contains("Collapse 2,3", source.Calls);
            // The source reuses the lowest free id, as the engine does.
            Assert.Equal([1, 2], source.Function!.Steps.Select(step => step.Id).Order());
            Assert.Equal("call_function", source.Function.Steps.Single(step => step.Id == 2).Kind);
            Assert.Equal([2], canvas.Scene.SelectedSteps.Order());
            Assert.True(source.History.Undo().Ok);
            Assert.Equal(before, source.GetSnapshot());
            Assert.False(source.History.CanUndo);
        });
    }

    [Fact]
    public void Expand_opens_a_selected_call_and_selects_what_came_back()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.AddStep(4, "call_function");
            var canvas = Open(source, 4);

            Click(canvas, "expand");

            Assert.Contains("Expand 4", source.Calls);
            Assert.DoesNotContain(source.Function!.Steps, step => step.Kind == "call_function");
            Assert.Equal([4], canvas.Scene.SelectedSteps.Order());
        });
    }

    [Fact]
    public void Expand_of_a_step_that_is_not_a_call_is_refused_with_the_reason_and_changes_nothing()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            var canvas = Open(source, 3);
            var before = source.GetSnapshot();
            var folded = false;
            canvas.Edits.Folded += _ => folded = true;

            Click(canvas, "expand");

            Assert.Equal("Select one Call step to expand.", canvas.NoticeText);
            Assert.Equal(before, source.GetSnapshot());
            Assert.False(source.History.CanUndo);
            Assert.False(folded);
        });
    }

    [Fact]
    public void Expand_with_two_steps_selected_is_refused_without_asking_the_source()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            var canvas = Open(source, 2, 3);

            Click(canvas, "expand");

            Assert.Equal("Select one Call step to expand.", canvas.NoticeText);
            Assert.DoesNotContain(source.Calls, call => call.StartsWith("Expand", StringComparison.Ordinal));
        });
    }
}

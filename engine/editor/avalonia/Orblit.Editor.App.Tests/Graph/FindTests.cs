using Avalonia.Controls;
using Avalonia.Input;
using Orblit.Editor.App.Graph;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class FindTests
{
    static GraphCanvas Open(FakeGraphSource? source = null) => new(source ?? FakeGraphSource.Sample());

    static void Click(GraphCanvas canvas, string id) => canvas.Find<Button>($"find:{id}")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    static TextBox Box(GraphCanvas canvas, string id, string text)
    {
        var box = canvas.Find<TextBox>($"find:{id}")!;
        box.Text = text;
        return box;
    }

    static IEnumerable<int> Selected(GraphCanvas canvas) => canvas.Scene.SelectedSteps.Order();

    [Fact]
    public void Cycle_walks_the_list_both_ways_and_wraps()
    {
        List<int> ids = [4, 7, 9];

        Assert.Equal(7, StepSearch.Cycle(ids, 4, forward: true));
        Assert.Equal(4, StepSearch.Cycle(ids, 9, forward: true));
        Assert.Equal(9, StepSearch.Cycle(ids, 4, forward: false));
        Assert.Equal(4, StepSearch.Cycle(ids, null, forward: true));
        Assert.Equal(9, StepSearch.Cycle(ids, null, forward: false));
        Assert.Equal(4, StepSearch.Cycle(ids, 5, forward: true));
        Assert.Null(StepSearch.Cycle([], 1, forward: true));
    }

    [Fact]
    public void A_search_finds_titles_kinds_pin_names_and_values_and_ignores_case()
    {
        UiThread.Run(() =>
        {
            var steps = Open().Scene.Steps;

            Assert.Equal([3], StepSearch.Find(steps, "PRINT"));
            Assert.Equal([1, 3], StepSearch.Find(steps, "then"));
            Assert.Equal([3], StepSearch.Find(steps, "\"hi\""));
            Assert.Equal([2], StepSearch.Find(steps, "condition"));
            Assert.Empty(StepSearch.Find(steps, "   "));
            Assert.Empty(StepSearch.Find(steps, "nothing like this"));
        });
    }

    [Fact]
    public void Next_and_previous_show_the_matches_in_reading_order_and_wrap()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            Box(canvas, "text", "then");

            Click(canvas, "next");
            Assert.Equal([1], Selected(canvas));
            Assert.Equal("1 of 2", canvas.FindBar.Status);
            Click(canvas, "next");
            Assert.Equal([3], Selected(canvas));
            Click(canvas, "next");
            Assert.Equal([1], Selected(canvas));
            Click(canvas, "previous");
            Assert.Equal([3], Selected(canvas));
            Assert.Equal("2 of 2", canvas.FindBar.Status);
        });
    }

    [Fact]
    public void Steps_in_one_column_are_read_from_the_top_down()
    {
        UiThread.Run(() =>
        {
            var source = new FakeGraphSource();
            source.Put(FakeGraphSource.MakeStep(1, "print", 100, 200, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));
            source.Put(FakeGraphSource.MakeStep(2, "print", 100, 50, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));

            Assert.Equal([2, 1], StepSearch.Find(Open(source).Scene.Steps, "print"));
        });
    }

    [Fact]
    public void Two_selected_steps_make_no_current_step_so_a_search_starts_at_an_end_of_the_list()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            Box(canvas, "text", "then");

            canvas.Select([1, 3]);
            Click(canvas, "next");
            Assert.Equal("1 of 2", canvas.FindBar.Status);

            canvas.Select([3, 1]);
            Click(canvas, "previous");
            Assert.Equal("2 of 2", canvas.FindBar.Status);
        });
    }

    [Fact]
    public void Enter_finds_the_next_match_and_shift_enter_the_previous_one()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            var box = Box(canvas, "text", "then");

            Press(box, Key.Enter);
            Press(box, Key.Enter);
            Assert.Equal([3], Selected(canvas));
            Press(box, Key.Enter, KeyModifiers.Shift);
            Assert.Equal([1], Selected(canvas));
        });
    }

    [Fact]
    public void A_search_with_no_match_says_so_and_keeps_the_selection()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            canvas.Select([2]);
            Box(canvas, "text", "zzz");

            Click(canvas, "next");

            Assert.Equal("No steps match.", canvas.FindBar.Status);
            Assert.Equal([2], Selected(canvas));
        });
    }

    [Fact]
    public void Go_to_shows_the_step_with_the_id_and_explains_an_id_that_does_not_work()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            var box = Box(canvas, "goto", "3");

            Press(box, Key.Enter);
            Assert.Equal([3], Selected(canvas));

            box.Text = "99";
            Press(box, Key.Enter);
            Assert.Equal("This function has no step 99.", canvas.FindBar.Status);
            Assert.Equal([3], Selected(canvas));

            box.Text = "three";
            Press(box, Key.Enter);
            Assert.Equal("Type a step id, a whole number.", canvas.FindBar.Status);
        });
    }

    [Fact]
    public void Bookmarks_are_walked_in_reading_order_and_wrap()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            canvas.Select([3]);
            Click(canvas, "bookmark");
            canvas.Select([2]);
            Click(canvas, "bookmark");
            canvas.Select([]);

            Click(canvas, "bookmark_next");
            Assert.Equal([2], Selected(canvas));
            Assert.Equal("Bookmark 1 of 2", canvas.FindBar.Status);
            Click(canvas, "bookmark_next");
            Assert.Equal([3], Selected(canvas));
            Click(canvas, "bookmark_next");
            Assert.Equal([2], Selected(canvas));
            Click(canvas, "bookmark_previous");
            Assert.Equal([3], Selected(canvas));
        });
    }

    [Fact]
    public void A_second_bookmark_click_takes_the_mark_off()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            canvas.Select([2]);

            Click(canvas, "bookmark");
            Assert.Contains(2, canvas.Scene.Bookmarked);
            Assert.Equal("Bookmarked step 2.", canvas.FindBar.Status);
            Click(canvas, "bookmark");

            Assert.Empty(canvas.Scene.Bookmarked);
            Click(canvas, "bookmark_next");
            Assert.Equal("No bookmarks.", canvas.FindBar.Status);
        });
    }

    [Fact]
    public void A_bookmark_needs_exactly_one_selected_step()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            canvas.Select([2, 3]);

            Click(canvas, "bookmark");

            Assert.Equal("Select one step to bookmark.", canvas.FindBar.Status);
            Assert.Empty(canvas.Scene.Bookmarked);
        });
    }

    [Fact]
    public void A_bookmark_goes_when_its_step_is_removed()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            var canvas = Open(source);
            canvas.Select([3]);
            Click(canvas, "bookmark");

            source.RemoveStep(3);
            source.AddStep(3, "print");

            Assert.Empty(canvas.Scene.Bookmarked);
        });
    }

    [Fact]
    public void The_find_shortcut_opens_the_bar_and_escape_closes_it()
    {
        UiThread.Run(() =>
        {
            var canvas = Open();
            Assert.False(canvas.FindBar.IsVisible);

            Press(canvas.Surface, Key.F, KeyModifiers.Meta);
            Assert.True(canvas.FindBar.IsVisible);

            Press(canvas.Find<TextBox>("find:text")!, Key.Escape);
            Assert.False(canvas.FindBar.IsVisible);
        });
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class GraphClipboardTests
{
    sealed class MemoryClipboard : ITextClipboard
    {
        public string? Held { get; set; }

        public Task<string?> GetText() => Task.FromResult(Held);

        public Task SetText(string text)
        {
            Held = text;
            return Task.CompletedTask;
        }
    }

    // A reads a String variable named total through step 2 and prints it with step 3.
    static FakeGraphSource Reader(int totalType = 4)
    {
        var source = new FakeGraphSource();
        source.Put(FakeGraphSource.MakeStep(1, "entry", 0, 0, [], [FakeGraphSource.Flow("then")]));
        source.Put(FakeGraphSource.MakeStep(2, "get", 100, 80, [], [FakeGraphSource.Data("value", 4)], null, ("name", "\"total\"")));
        source.Put(FakeGraphSource.MakeStep(3, "print", 360, 60, [FakeGraphSource.Flow("run"), FakeGraphSource.Data("text", 4, "text")], [FakeGraphSource.Flow("then")]));
        source.Put(new GraphWire(2, 0, 3, 1));
        source.Put(new GraphVariable("total", totalType, "null", false, 0, "", "", ""));
        return source;
    }

    // The fake makes its history on first use, so it is asked for before the first edit.
    static GraphCanvas Open(FakeGraphSource source, MemoryClipboard clipboard)
    {
        _ = source.History;
        return new GraphCanvas(source, clipboard);
    }

    static void Pick(GraphCanvas canvas, params int[] ids) => canvas.Select(ids);

    static void Press(GraphCanvas canvas, Key key, KeyModifiers modifiers = KeyModifiers.Meta) =>
        canvas.Surface.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    static void Click(GraphCanvas canvas, string id) => canvas.Find<Button>(id)!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public void Two_wired_steps_and_their_variable_paste_into_another_script()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var from = Open(Reader(), clipboard);
            var into = FakeGraphSource.Sample();
            var target = Open(into, clipboard);
            Pick(from, 2, 3);

            Assert.True(from.Clipboard.Copy().GetAwaiter().GetResult());
            target.Clipboard.Paste(new Point(100, 200)).GetAwaiter().GetResult();

            var steps = into.Function!.Steps;
            Assert.Equal(5, steps.Count);
            Assert.Equal([4, 5], steps.Skip(3).Select(step => step.Id));
            Assert.Equal(new GraphWire(4, 0, 5, 1), into.Function.Wires.Last());
            Assert.Equal((100, 220), (steps[3].X, steps[3].Y));
            Assert.Contains(into.Document.Variables, variable => variable.Name == "total");
            Assert.Equal([4, 5], target.Scene.SelectedSteps.Order());
        });
    }

    [Fact]
    public void A_paste_is_one_entry_in_the_history()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var from = Open(Reader(), clipboard);
            var into = FakeGraphSource.Sample();
            var target = Open(into, clipboard);
            var before = into.GetSnapshot();
            var history = into.History;
            Pick(from, 2, 3);
            from.Clipboard.Copy().GetAwaiter().GetResult();

            target.Clipboard.Paste(new Point(0, 0)).GetAwaiter().GetResult();
            Assert.True(history.Undo().Ok);

            Assert.Equal(before, into.GetSnapshot());
            Assert.False(history.CanUndo);
        });
    }

    [Fact]
    public void A_variable_with_another_type_raises_the_choice_and_changes_nothing()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var from = Open(Reader(), clipboard);
            var into = Reader(totalType: 3);
            var target = Open(into, clipboard);
            var before = into.GetSnapshot();
            Pick(from, 2, 3);
            from.Clipboard.Copy().GetAwaiter().GetResult();

            target.Clipboard.Paste(new Point(0, 0)).GetAwaiter().GetResult();

            Assert.True(target.Clipboard.HasChoice);
            Assert.True(target.PasteBar.IsVisible);
            Assert.Contains("variable total", target.PasteBar.Message);
            Assert.Equal(before, into.GetSnapshot());
        });
    }

    [Fact]
    public void Keep_both_renames_the_pasted_variable_and_points_the_steps_at_it()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var from = Open(Reader(), clipboard);
            var into = Reader(totalType: 3);
            var target = Open(into, clipboard);
            Pick(from, 2, 3);
            from.Clipboard.Copy().GetAwaiter().GetResult();
            target.Clipboard.Paste(new Point(0, 0)).GetAwaiter().GetResult();

            Click(target, "paste:keep_both");

            Assert.Equal(["total", "total_2"], into.Document.Variables.Select(variable => variable.Name));
            Assert.Equal("\"total_2\"", into.Function!.Steps.Single(step => step.Id == 4).Parameters["name"]);
            Assert.False(target.PasteBar.IsVisible);
            Assert.False(target.Clipboard.HasChoice);
        });
    }

    [Fact]
    public void Reuse_pastes_the_steps_and_keeps_the_scripts_own_variable()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var from = Open(Reader(), clipboard);
            var into = Reader(totalType: 3);
            var target = Open(into, clipboard);
            Pick(from, 2, 3);
            from.Clipboard.Copy().GetAwaiter().GetResult();
            target.Clipboard.Paste(new Point(0, 0)).GetAwaiter().GetResult();

            Click(target, "paste:reuse");

            Assert.Single(into.Document.Variables);
            Assert.Equal(3, into.Document.Variables[0].Type);
            Assert.Equal(5, into.Function!.Steps.Count);
        });
    }

    [Fact]
    public void Cancel_drops_the_paste_and_the_question()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var from = Open(Reader(), clipboard);
            var into = Reader(totalType: 3);
            var target = Open(into, clipboard);
            var before = into.GetSnapshot();
            Pick(from, 2, 3);
            from.Clipboard.Copy().GetAwaiter().GetResult();
            target.Clipboard.Paste(new Point(0, 0)).GetAwaiter().GetResult();

            Click(target, "paste:cancel");

            Assert.False(target.PasteBar.IsVisible);
            Assert.Equal(before, into.GetSnapshot());
        });
    }

    [Fact]
    public void Text_that_is_not_a_copy_of_steps_is_refused_with_a_reason()
    {
        UiThread.Run(() =>
        {
            var into = FakeGraphSource.Sample();
            var canvas = Open(into, new MemoryClipboard { Held = "just words" });
            var before = into.GetSnapshot();

            canvas.Clipboard.Paste(new Point(0, 0)).GetAwaiter().GetResult();

            Assert.Equal("The clipboard holds no steps.", canvas.NoticeText);
            Assert.True(canvas.NoticeBar.IsVisible);
            Assert.Equal(before, into.GetSnapshot());
        });
    }

    [Fact]
    public void Cut_puts_the_steps_on_the_clipboard_and_removes_them_in_one_undo()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var source = Reader();
            var canvas = Open(source, clipboard);
            var before = source.GetSnapshot();
            Pick(canvas, 2, 3);

            canvas.Clipboard.Cut().GetAwaiter().GetResult();

            Assert.NotNull(clipboard.Held);
            Assert.Equal([1], source.Function!.Steps.Select(step => step.Id));
            Assert.True(source.History.Undo().Ok);
            Assert.Equal(before, source.GetSnapshot());
        });
    }

    [Fact]
    public void Duplicate_makes_a_copy_beside_the_original_and_leaves_the_clipboard_alone()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard { Held = "kept" };
            var source = Reader();
            var canvas = Open(source, clipboard);
            Pick(canvas, 2, 3);

            canvas.Clipboard.Duplicate();

            var steps = source.Function!.Steps;
            Assert.Equal(5, steps.Count);
            Assert.Equal((140, 120), (steps[3].X, steps[3].Y));
            Assert.Equal(new GraphWire(4, 0, 5, 1), source.Function.Wires.Last());
            Assert.Equal("kept", clipboard.Held);
        });
    }

    [Fact]
    public void The_entry_step_cannot_be_copied()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var canvas = Open(Reader(), clipboard);
            Pick(canvas, 1);

            var copied = canvas.Clipboard.Copy().GetAwaiter().GetResult();

            Assert.False(copied);
            Assert.Null(clipboard.Held);
            Assert.Contains("entry step", canvas.NoticeText);
        });
    }

    [Fact]
    public void The_keys_copy_paste_duplicate_undo_and_redo()
    {
        UiThread.Run(() =>
        {
            var source = Reader();
            var canvas = Open(source, new MemoryClipboard());
            Pick(canvas, 2, 3);

            Press(canvas, Key.C);
            Press(canvas, Key.V);
            Assert.Equal(5, source.Function!.Steps.Count);
            Press(canvas, Key.D);
            Assert.Equal(7, source.Function.Steps.Count);
            Press(canvas, Key.Z);
            Press(canvas, Key.Z);
            Assert.Equal(3, source.Function.Steps.Count);
            Press(canvas, Key.Z, KeyModifiers.Meta | KeyModifiers.Shift);
            Assert.Equal(5, source.Function.Steps.Count);
            Press(canvas, Key.Y, KeyModifiers.Control);
            Assert.Equal(7, source.Function.Steps.Count);
        });
    }

    [Fact]
    public void A_key_without_the_platform_modifier_runs_no_command()
    {
        UiThread.Run(() =>
        {
            var source = Reader();
            var canvas = Open(source, new MemoryClipboard());
            Pick(canvas, 2, 3);

            Press(canvas, Key.D, KeyModifiers.None);
            Press(canvas, Key.D, KeyModifiers.Shift);

            Assert.Equal(3, source.Function!.Steps.Count);
        });
    }
}

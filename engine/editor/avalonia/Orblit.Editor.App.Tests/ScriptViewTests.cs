using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ScriptViewTests
{
    sealed class Files : IScriptFiles
    {
        public Dictionary<string, string> Text { get; } = new()
        {
            ["res://spin.cs"] = "class Spin\n{\n}\n",
            ["res://slime.cs"] = "class Slime { }",
        };

        public List<(string Path, string Text)> Written { get; } = [];

        public bool WriteFails { get; set; }

        public bool Denied { get; set; }

        // A listed file that cannot be read, like one deleted since the list was made.
        public string? Gone { get; set; }

        public Dictionary<string, FakeGraphSource> Graphs { get; } = [];

        public FakeDebugChannel Channel { get; } = new();

        public IDebugChannel Debug => Channel;

        public IReadOnlyList<string> GraphScripts() => [.. Graphs.Keys];

        public IGraphFile OpenGraph(string resourcePath) =>
            Graphs.TryGetValue(resourcePath, out var graph) ? graph : throw new IOException($"No {resourcePath}.");

        public IReadOnlyList<string> Scripts() => Gone is null ? [.. Text.Keys] : [.. Text.Keys, Gone];

        public string ReadScript(string resourcePath) =>
            Text.TryGetValue(resourcePath, out var text) ? text : throw new IOException($"No {resourcePath}.");

        public void WriteScript(string resourcePath, string text)
        {
            if (Denied)
            {
                throw new UnauthorizedAccessException("Access denied.");
            }

            if (WriteFails)
            {
                throw new IOException("The disk is full.");
            }

            Written.Add((resourcePath, text));
            Text[resourcePath] = text;
        }
    }

    static ScriptView Open(Files files)
    {
        var view = new ScriptView();
        new Window { Content = view, Width = 800, Height = 600 }.Show();
        view.Show(files);
        return view;
    }

    static void Pick(ScriptView view, string path) => view.List.SelectedItem = path;

    [Fact]
    public void It_starts_with_the_files_listed_and_none_open()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());

            Assert.Equal(["res://spin.cs", "res://slime.cs"], (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Equal("Pick a script.", view.Heading.Text);
            Assert.False(view.Editor.IsEnabled);
            Assert.False(view.SaveButton.IsEnabled);
            Assert.False(view.IsDirty);
            Assert.True(view.Save());
        });
    }

    [Fact]
    public void Picking_a_script_shows_its_text_and_numbers_the_lines()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());

            Pick(view, "res://spin.cs");

            Assert.Equal("class Spin\n{\n}\n", view.Editor.Text);
            Assert.Equal("1\n2\n3\n4", view.Gutter.Text);
            Assert.Equal("res://spin.cs", view.Heading.Text);
            Assert.True(view.Editor.IsEnabled);
            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
        });
    }

    [Fact]
    public void A_file_with_CR_LF_line_ends_is_not_an_edit_once_opened()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            files.Text["res://crlf.cs"] = "a\r\nb\r\n";
            var view = Open(files);

            Pick(view, "res://crlf.cs");

            Assert.False(view.IsDirty);
            Assert.True(view.Save());
            Assert.Empty(files.Written);
        });
    }

    [Fact]
    public void Typing_marks_the_script_edited_and_putting_the_text_back_clears_it()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://slime.cs");

            view.Editor.Text = "class Slime { int hp; }";
            Assert.True(view.IsDirty);
            Assert.True(view.SaveButton.IsEnabled);
            Assert.Equal("1", view.Gutter.Text);

            view.Editor.Text = "class Slime { }";
            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
        });
    }

    [Fact]
    public void The_gutter_follows_the_number_of_lines()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://slime.cs");

            view.Editor.Text = "a\nb\nc";

            Assert.Equal("1\n2\n3", view.Gutter.Text);
        });
    }

    [Fact]
    public void Save_writes_the_text_and_clears_the_mark()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Click(view.SaveButton);

            Assert.Equal([("res://spin.cs", "class Spin { }")], files.Written);
            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
            Assert.Equal("Saved res://spin.cs.", view.Status.Text);
        });
    }

    [Fact]
    public void A_write_the_system_refuses_is_said_and_keeps_the_edits()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files { Denied = true });
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Assert.False(view.Save());

            Assert.True(view.IsDirty);
            Assert.Equal("Access denied.", view.Status.Text);
        });
    }

    [Fact]
    public void Save_with_no_edits_writes_nothing()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");

            Assert.True(view.Save());

            Assert.Empty(files.Written);
        });
    }

    [Fact]
    public void A_failed_write_keeps_the_edits_and_says_why()
    {
        UiThread.Run(() =>
        {
            var files = new Files { WriteFails = true };
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Assert.False(view.Save());

            Assert.True(view.IsDirty);
            Assert.True(view.SaveButton.IsEnabled);
            Assert.Equal("The disk is full.", view.Status.Text);
            Assert.Equal("class Spin { }", view.Editor.Text);
        });
    }

    [Fact]
    public void Picking_another_script_writes_the_open_one_first()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Pick(view, "res://slime.cs");

            Assert.Equal([("res://spin.cs", "class Spin { }")], files.Written);
            Assert.Equal("class Slime { }", view.Editor.Text);
            Assert.Equal("res://slime.cs", view.Heading.Text);
            Assert.Equal("", view.Status.Text);
            Assert.False(view.IsDirty);
        });
    }

    [Fact]
    public void Picking_another_script_after_a_failed_write_stays_on_the_open_one()
    {
        UiThread.Run(() =>
        {
            var files = new Files { WriteFails = true };
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            Pick(view, "res://slime.cs");

            Assert.Equal("class Spin { }", view.Editor.Text);
            Assert.Equal("res://spin.cs", view.Heading.Text);
            Assert.Equal("res://spin.cs", view.List.SelectedItem);
            Assert.True(view.IsDirty);
            Assert.Equal("The disk is full.", view.Status.Text);
        });
    }

    [Fact]
    public void A_script_that_cannot_be_read_says_why_and_keeps_the_open_one()
    {
        UiThread.Run(() =>
        {
            var files = new Files { Gone = "res://gone.cs" };
            var view = Open(files);
            Pick(view, "res://spin.cs");

            Pick(view, "res://gone.cs");

            Assert.Equal("No res://gone.cs.", view.Status.Text);
            Assert.Equal("res://spin.cs", view.Heading.Text);
            Assert.Equal("res://spin.cs", view.List.SelectedItem);
            Assert.Equal("class Spin\n{\n}\n", view.Editor.Text);
        });
    }

    [Fact]
    public void A_script_that_cannot_be_read_as_the_first_choice_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files { Gone = "res://gone.cs" });

            Pick(view, "res://gone.cs");

            Assert.Equal("No res://gone.cs.", view.Status.Text);
            Assert.Equal("Pick a script.", view.Heading.Text);
            Assert.False(view.Editor.IsEnabled);
            Assert.Null(view.List.SelectedItem);
        });
    }

    [Fact]
    public void Discard_counts_the_text_as_saved_and_writes_nothing()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            view.Discard();

            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
            Assert.Empty(files.Written);
            Assert.Equal("class Spin { }", view.Editor.Text);
        });
    }

    [Fact]
    public void Showing_the_view_again_lists_new_files_and_keeps_the_open_script_and_its_edits()
    {
        UiThread.Run(() =>
        {
            var files = new Files();
            var view = Open(files);
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            view.IsVisible = false;
            files.Text["res://new.cs"] = "";
            view.IsVisible = true;

            Assert.Equal(["res://spin.cs", "res://slime.cs", "res://new.cs"], (IEnumerable<string>)view.List.ItemsSource!);
            Assert.Equal("res://spin.cs", view.List.SelectedItem);
            Assert.Equal("class Spin { }", view.Editor.Text);
            Assert.True(view.IsDirty);
        });
    }

    [Fact]
    public void Showing_a_project_again_closes_the_open_script()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files());
            Pick(view, "res://spin.cs");
            view.Editor.Text = "class Spin { }";

            view.Show(new Files());

            Assert.Equal("Pick a script.", view.Heading.Text);
            Assert.Equal("", view.Editor.Text);
            Assert.False(view.Editor.IsEnabled);
            Assert.False(view.IsDirty);
            Assert.Null(view.List.SelectedItem);
        });
    }

    static (Files Files, FakeGraphSource Graph, ScriptView View) OpenWithGraph()
    {
        var files = new Files();
        var graph = FakeGraphSource.Sample();
        files.Graphs["res://logic.ograph"] = graph;
        var view = Open(files);
        return (files, graph, view);
    }

    [Fact]
    public void A_graph_script_is_listed_after_the_C_sharp_files_and_opens_on_the_canvas()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Assert.Equal(["res://spin.cs", "res://slime.cs", "res://logic.ograph"], view.List.Items.Cast<string>());
            Assert.Null(view.Canvas);

            Pick(view, "res://logic.ograph");

            Assert.NotNull(view.Canvas);
            Assert.Equal(3, view.Canvas.Scene.Steps.Count);
            Assert.Equal("res://logic.ograph", view.Heading.Text);
            Assert.False(view.IsDirty);
            Assert.False(view.SaveButton.IsEnabled);
        });
    }

    [Fact]
    public void A_graph_script_shows_its_panels_beside_the_canvas_and_a_text_file_shows_none()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            graph.Put(new GraphVariable("total", 2, "0", false, 0, "", "", ""));
            Assert.Null(view.Panels);

            Pick(view, "res://logic.ograph");

            Assert.NotNull(view.Panels);
            Assert.NotNull(view.Panels.Find<TextBox>("variable:total:name"));

            Pick(view, "res://spin.cs");

            Assert.Null(view.Panels);
        });
    }

    [Fact]
    public void Showing_another_function_in_the_panels_gives_the_canvas_that_function()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            graph.PutFunction("helper");
            Pick(view, "res://logic.ograph");
            var before = view.Canvas;

            view.Panels!.Find<Button>("function:helper:open")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.NotSame(before, view.Canvas);
            Assert.Contains("OpenFunction helper", graph.Calls);
        });
    }

    [Fact]
    public void Selecting_one_step_shows_it_in_the_inspector_and_a_new_canvas_clears_it()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            graph.PutFunction("helper");
            Pick(view, "res://logic.ograph");
            Assert.Equal("No step selected.", view.Inspector!.Heading);

            var canvas = view.Canvas!;
            var header = canvas.Scene.Find(3)!.Origin + new Vector(40, 10);
            canvas.Gestures.Press(header, MouseButton.Left, KeyModifiers.None, 1);
            canvas.Gestures.Release(header);
            canvas.Surface.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F13 });

            Assert.Equal("print", view.Inspector.Heading);

            view.Panels!.Find<Button>("function:helper:open")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("No step selected.", view.Inspector.Heading);
        });
    }

    [Fact]
    public void Picking_a_link_selects_its_step_on_the_canvas_and_shows_it_in_the_inspector()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            graph.Put(new GraphSignal("scored", []));
            graph.Put(FakeGraphSource.MakeStep(4, "emit", 520, 200, [], [], null, ("signal", "\"scored\"")));
            Pick(view, "res://logic.ograph");
            var canvas = view.Canvas;

            view.Panels!.Find<Button>("connection:main:4")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Same(canvas, view.Canvas);
            Assert.Equal([4], view.Canvas!.Scene.SelectedSteps);
            Assert.Equal("emit", view.Inspector!.Heading);
        });
    }

    [Fact]
    public void Picking_a_link_in_another_function_gives_the_canvas_that_function()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            graph.PutFunction("helper", FakeGraphSource.MakeStep(5, "await_signal", 0, 0, [], [], null, ("signal", "\"scored\"")));
            Pick(view, "res://logic.ograph");
            var before = view.Canvas;

            view.Panels!.Find<Button>("connection:helper:5")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.NotSame(before, view.Canvas);
            Assert.Contains("OpenFunction helper", graph.Calls);
        });
    }

    [Fact]
    public void A_text_file_has_no_inspector()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, "res://logic.ograph");
            Assert.NotNull(view.Inspector);

            Pick(view, "res://spin.cs");

            Assert.Null(view.Inspector);
        });
    }

    [Fact]
    public void An_edit_on_the_canvas_makes_the_script_dirty_and_save_writes_it_through_the_file()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            Pick(view, "res://logic.ograph");

            view.Canvas!.Edits.MoveSteps([2], new Vector(10, 0));

            Assert.True(view.IsDirty);
            Assert.True(view.SaveButton.IsEnabled);
            view.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, graph.Saves);
            Assert.False(view.IsDirty);
            Assert.Contains("Saved res://logic.ograph", view.Status.Text);
        });
    }

    [Fact]
    public void A_graph_that_cannot_be_written_stays_dirty_and_says_why()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            Pick(view, "res://logic.ograph");
            view.Canvas!.Edits.MoveSteps([2], new Vector(10, 0));
            graph.SaveFails = true;

            Assert.False(view.Save());

            Assert.True(view.IsDirty);
            Assert.Equal("The disk is full.", view.Status.Text);
        });
    }

    [Fact]
    public void Choosing_a_C_sharp_file_saves_the_open_graph_releases_it_and_shows_the_text()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            Pick(view, "res://logic.ograph");
            view.Canvas!.Edits.MoveSteps([2], new Vector(10, 0));

            Pick(view, "res://spin.cs");

            Assert.Equal(1, graph.Saves);
            Assert.True(graph.Disposed);
            Assert.Null(view.Canvas);
            Assert.Equal("class Spin\n{\n}\n", view.Editor.Text);
            Assert.False(view.IsDirty);
        });
    }

    [Fact]
    public void A_graph_the_engine_cannot_load_keeps_the_open_file_and_says_why()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Pick(view, "res://spin.cs");
            files.Graphs.Remove("res://logic.ograph");
            view.List.SelectedItem = null;
            files.Graphs["res://gone.ograph"] = FakeGraphSource.Sample();
            view.Show(files);
            files.Graphs.Remove("res://gone.ograph");

            Pick(view, "res://gone.ograph");

            Assert.Contains("No res://gone.ograph.", view.Status.Text);
            Assert.Null(view.Canvas);
        });
    }

    [Fact]
    public void Discarding_counts_a_changed_graph_as_saved_and_writes_nothing()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            Pick(view, "res://logic.ograph");
            view.Canvas!.Edits.MoveSteps([2], new Vector(10, 0));

            view.Discard();

            Assert.False(view.IsDirty);
            Assert.Equal(0, graph.Saves);
        });
    }

    [Fact]
    public void A_breakpoint_set_on_the_canvas_is_kept_when_the_script_is_closed_and_opened_again()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, "res://logic.ograph");
            var step = view.Canvas!.Scene.Find(2)!;
            view.Canvas.Gestures.Press(step.Origin + new Vector(-8, 10), MouseButton.Left, KeyModifiers.None, 1);
            view.Canvas.Gestures.Release(step.Origin + new Vector(-8, 10));
            Assert.True(view.Breakpoints.Has("res://logic.ograph#main", 2));

            Pick(view, "res://spin.cs");
            Pick(view, "res://logic.ograph");

            Assert.Equal([2], view.Canvas!.Scene.Breakpointed);
        });
    }

    const string Logic = "res://logic.ograph";

    static StepMarks MarksOf(ScriptView view, int step) => view.Canvas!.Scene.MarksOf(view.Canvas.Scene.Find(step)!);

    static void Hear(Files files, ScriptView view, DebugState state)
    {
        files.Channel.Next = state;
        view.PollDebugger();
    }

    static DebugState StoppedAt(string script, string function, int step) =>
        FakeDebugChannel.Stopped("breakpoint", FakeDebugChannel.Frame(script, function, step));

    [Fact]
    public void A_stop_opens_the_script_and_marks_and_selects_the_step()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Assert.Null(view.Canvas);

            Hear(files, view, StoppedAt(Logic, "main", 2));

            Assert.Equal(Logic, view.Heading.Text);
            Assert.Equal(new DebugPlace(Logic, "main", 2), view.Canvas!.Scene.StoppedAt);
            Assert.True(MarksOf(view, 2).HasFlag(StepMarks.Current));
            Assert.Equal([2], view.Canvas.Scene.SelectedSteps);
        });
    }

    [Fact]
    public void A_stop_in_another_function_opens_that_function_and_marks_the_step()
    {
        UiThread.Run(() =>
        {
            var (files, graph, view) = OpenWithGraph();
            graph.PutFunction("helper", FakeGraphSource.MakeStep(5, "print", 0, 0, [], []));

            Hear(files, view, StoppedAt(Logic, "helper", 5));

            Assert.Contains("OpenFunction helper", graph.Calls);
            Assert.True(MarksOf(view, 5).HasFlag(StepMarks.Current));
            Assert.Equal([5], view.Canvas!.Scene.SelectedSteps);
        });
    }

    [Fact]
    public void Going_on_takes_the_mark_off()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Hear(files, view, StoppedAt(Logic, "main", 2));

            Hear(files, view, FakeDebugChannel.Running());

            Assert.Null(view.Canvas!.Scene.StoppedAt);
            Assert.Equal(StepMarks.Selected, MarksOf(view, 2));
        });
    }

    [Fact]
    public void A_stop_the_view_cannot_show_opens_nothing()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();

            Hear(files, view, StoppedAt("res://gone.ograph", "main", 2));
            Hear(files, view, FakeDebugChannel.Stopped("step", new DebugFrame("res://spin.cs", 3, "Spin")));

            Assert.Null(view.Canvas);
            Assert.Equal("Pick a script.", view.Heading.Text);
        });
    }

    [Fact]
    public void A_stop_in_the_open_script_does_not_move_a_view_the_person_changed_until_the_place_changes()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Hear(files, view, StoppedAt(Logic, "main", 2));
            view.Canvas!.Reveal(3);

            Hear(files, view, StoppedAt(Logic, "main", 2) with { Locals = [new DebugValue("total", "5")] });
            Assert.Equal([3], view.Canvas.Scene.SelectedSteps);

            Hear(files, view, FakeDebugChannel.Running());
            Hear(files, view, StoppedAt(Logic, "main", 2));
            Assert.Equal([2], view.Canvas.Scene.SelectedSteps);
        });
    }

    [Fact]
    public void The_debugger_panel_follows_the_game_and_survives_opening_scripts_again()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Pick(view, Logic);
            Pick(view, "res://spin.cs");
            Pick(view, Logic);

            Hear(files, view, StoppedAt(Logic, "main", 2));

            Assert.NotNull(view.DebugPanel);
            Assert.True(view.DebugPanel!.Find<Button>("debugger:continue")!.IsEnabled);
            Assert.NotNull(view.DebugPanel.Parent);
        });
    }

    [Fact]
    public void Showing_the_files_again_hands_the_breakpoints_to_one_debugger_only()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Hear(files, view, FakeDebugChannel.Running());
            view.Show(files);

            view.Breakpoints.Toggle($"{Logic}#main", 2);

            Assert.Equal([$"breakpoint {Logic}#main 2 True"], files.Channel.Sent);
        });
    }
}

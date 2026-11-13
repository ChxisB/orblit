using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class GraphViewTests
{
    const string Logic = "res://logic.ograph";
    const string Other = "res://enemies/boss.ograph";

    sealed class Files : IGraphFiles
    {
        public Dictionary<string, FakeGraphSource> Graphs { get; } = [];

        // The text of the breakpoint file, or null when the project has none.
        public string? BreakpointText { get; set; }

        public bool BreakpointReadFails { get; set; }

        public bool BreakpointWriteFails { get; set; }

        public int BreakpointWrites { get; private set; }

        public List<(string Path, string Base)> Created { get; } = [];

        // What a make raises, or null for one that works.
        public Exception? CreateError { get; set; }

        public FakeDebugChannel Channel { get; } = new();

        public IDebugChannel Debug => Channel;

        public IReadOnlyList<string> GraphScripts() => [.. Graphs.Keys];

        public IGraphFile OpenGraph(string resourcePath) =>
            Graphs.TryGetValue(resourcePath, out var graph) ? graph : throw new IOException($"No {resourcePath}.");

        public void CreateGraph(string resourcePath, string baseType)
        {
            if (CreateError is not null)
            {
                throw CreateError;
            }

            Created.Add((resourcePath, baseType));
            Graphs[resourcePath] = FakeGraphSource.Sample();
        }

        public string? ReadBreakpoints() => BreakpointReadFails ? throw new IOException("The file is locked.") : BreakpointText;

        public void WriteBreakpoints(string text)
        {
            if (BreakpointWriteFails)
            {
                throw new IOException("The disk is full.");
            }

            BreakpointWrites++;
            BreakpointText = text;
        }
    }

    sealed class MemoryClipboard : ITextClipboard
    {
        public string? Text { get; private set; }

        public Task<string?> GetText() => Task.FromResult(Text);

        public Task SetText(string text)
        {
            Text = text;
            return Task.CompletedTask;
        }
    }

    static GraphView Open(Files files, ITextClipboard? clipboard = null)
    {
        var view = new GraphView(clipboard);
        new Window { Content = view, Width = 1000, Height = 600 }.Show();
        view.Show(files);
        return view;
    }

    static (Files Files, FakeGraphSource Graph, GraphView View) OpenWithGraph()
    {
        var files = new Files();
        var graph = FakeGraphSource.Sample();
        files.Graphs[Logic] = graph;
        var view = Open(files);
        return (files, graph, view);
    }

    static void Pick(GraphView view, string path) => view.List.SelectedItem = path;

    static MenuItem Item(GraphView view, string header) =>
        view.FileMenu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header);

    static void Choose(GraphView view, string header) => Item(view, header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static void Edit(GraphView view) => view.Canvas!.Edits.MoveSteps([2], new Vector(10, 0));

    [Fact]
    public void It_starts_with_the_graph_scripts_listed_and_none_open()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();

            Assert.Equal([Logic], view.List.Items.Cast<string>());
            Assert.Null(view.Current);
            Assert.Null(view.Canvas);
            Assert.Empty(view.Tabs);
            Assert.True(view.Hint.IsVisible);
            Assert.False(view.IsDirty);
            Assert.True(view.Save());
        });
    }

    [Fact]
    public void Picking_a_graph_script_opens_it_on_the_canvas_with_a_tab()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();

            Pick(view, Logic);

            Assert.Equal(Logic, view.Current);
            Assert.Equal(3, view.Canvas!.Scene.Steps.Count);
            Assert.Equal(["logic.ograph"], view.Tabs.Select(tab => tab.Label.Text));
            Assert.False(view.Hint.IsVisible);
            Assert.False(view.IsDirty);
        });
    }

    [Fact]
    public void The_filter_narrows_the_list_and_keeps_the_open_script_selected_while_it_shows()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            files.Graphs[Other] = FakeGraphSource.Sample();
            view.Show(files);
            Pick(view, Logic);

            view.Filter.Text = "BOSS";

            Assert.Equal([Other], view.List.Items.Cast<string>());
            Assert.Null(view.List.SelectedItem);
            Assert.Equal(Logic, view.Current);

            view.Filter.Text = "";

            Assert.Equal([Logic, Other], view.List.Items.Cast<string>());
            Assert.Equal(Logic, view.List.SelectedItem);
        });
    }

    [Fact]
    public void A_graph_script_shows_its_panels_beside_the_canvas()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            graph.Put(new GraphVariable("total", 2, "0", false, 0, "", "", ""));
            Assert.Null(view.Panels);

            Pick(view, Logic);

            Assert.NotNull(view.Panels);
            Assert.NotNull(view.Panels.Find<TextBox>("variable:total:name"));
        });
    }

    [Fact]
    public void Showing_another_function_in_the_panels_gives_the_canvas_that_function()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            graph.PutFunction("helper");
            Pick(view, Logic);
            var before = view.Canvas;

            Click(view.Panels!.Find<Button>("function:helper:open")!);

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
            Pick(view, Logic);
            Assert.Equal("No step selected.", view.Inspector!.Heading);

            var canvas = view.Canvas!;
            var header = canvas.Scene.Find(3)!.Origin + new Vector(40, 10);
            canvas.Gestures.Press(header, MouseButton.Left, KeyModifiers.None, 1);
            canvas.Gestures.Release(header);
            canvas.Surface.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F13 });

            Assert.Equal("print", view.Inspector.Heading);

            Click(view.Panels!.Find<Button>("function:helper:open")!);

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
            Pick(view, Logic);
            var canvas = view.Canvas;

            Click(view.Panels!.Find<Button>("connection:main:4")!);

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
            Pick(view, Logic);
            var before = view.Canvas;

            Click(view.Panels!.Find<Button>("connection:helper:5")!);

            Assert.NotSame(before, view.Canvas);
            Assert.Contains("OpenFunction helper", graph.Calls);
        });
    }

    [Fact]
    public void An_edit_on_the_canvas_marks_the_script_and_its_tab_and_save_writes_it_through_the_file()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            Pick(view, Logic);
            Assert.False(Item(view, "Save").IsEnabled);

            Edit(view);

            Assert.True(view.IsDirty);
            Assert.Equal("logic.ograph *", view.Tabs.Single().Label.Text);
            Assert.True(Item(view, "Save").IsEnabled);
            Choose(view, "Save");
            Assert.Equal(1, graph.Saves);
            Assert.False(view.IsDirty);
            Assert.Equal("logic.ograph", view.Tabs.Single().Label.Text);
            Assert.Contains($"Saved {Logic}", view.Status.Text);
        });
    }

    [Fact]
    public void A_graph_that_cannot_be_written_stays_dirty_and_says_why()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            Pick(view, Logic);
            Edit(view);
            graph.SaveFails = true;

            Assert.False(view.Save());

            Assert.True(view.IsDirty);
            Assert.Equal("The disk is full.", view.Status.Text);
        });
    }

    static (Files Files, FakeGraphSource First, FakeGraphSource Second, GraphView View) OpenTwo()
    {
        var files = new Files();
        var first = FakeGraphSource.Sample();
        var second = FakeGraphSource.Sample();
        files.Graphs[Logic] = first;
        files.Graphs[Other] = second;
        return (files, first, second, Open(files));
    }

    [Fact]
    public void Opening_a_second_script_keeps_the_first_open_with_its_edits()
    {
        UiThread.Run(() =>
        {
            var (_, first, second, view) = OpenTwo();
            Pick(view, Logic);
            var canvas = view.Canvas;
            Edit(view);

            Pick(view, Other);

            Assert.Equal(["logic.ograph *", "boss.ograph"], view.Tabs.Select(tab => tab.Label.Text));
            Assert.Equal(Other, view.Current);
            Assert.False(first.Disposed);
            Assert.Equal(0, first.Saves);

            Click(view.Tabs.First().ChooseButton);

            Assert.Equal(Logic, view.Current);
            Assert.Same(canvas, view.Canvas);
            Assert.Equal(Logic, view.List.SelectedItem);
            Assert.False(second.Disposed);
        });
    }

    [Fact]
    public void Picking_a_script_that_is_open_shows_it_again_without_loading_it_twice()
    {
        UiThread.Run(() =>
        {
            var (_, _, _, view) = OpenTwo();
            Pick(view, Logic);
            Pick(view, Other);

            Pick(view, Logic);

            Assert.Equal(2, view.Tabs.Count());
            Assert.Equal(Logic, view.Current);
        });
    }

    [Fact]
    public void Close_writes_the_script_releases_it_and_shows_the_next_tab()
    {
        UiThread.Run(() =>
        {
            var (_, first, _, view) = OpenTwo();
            Pick(view, Logic);
            Pick(view, Other);
            Pick(view, Logic);
            Edit(view);

            Choose(view, "Close");

            Assert.Equal(1, first.Saves);
            Assert.True(first.Disposed);
            Assert.Equal(["boss.ograph"], view.Tabs.Select(tab => tab.Label.Text));
            Assert.Equal(Other, view.Current);
            Assert.False(view.IsDirty);
        });
    }

    [Fact]
    public void The_close_button_of_a_tab_closes_that_script_only()
    {
        UiThread.Run(() =>
        {
            var (_, _, second, view) = OpenTwo();
            Pick(view, Logic);
            Pick(view, Other);

            Click(view.Tabs.First().CloseButton);

            Assert.Equal(Other, view.Current);
            Assert.Equal(["boss.ograph"], view.Tabs.Select(tab => tab.Label.Text));
            Assert.False(second.Disposed);
        });
    }

    [Fact]
    public void Closing_the_last_script_leaves_the_hint_and_no_canvas()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, Logic);

            Choose(view, "Close");

            Assert.Null(view.Current);
            Assert.Null(view.Canvas);
            Assert.True(view.Hint.IsVisible);
            Assert.Null(view.List.SelectedItem);
            Assert.False(Item(view, "Close").IsEnabled);
        });
    }

    [Fact]
    public void A_close_that_cannot_write_keeps_the_script_open_and_says_why()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            Pick(view, Logic);
            Edit(view);
            graph.SaveFails = true;

            Choose(view, "Close");

            Assert.Equal(Logic, view.Current);
            Assert.False(graph.Disposed);
            Assert.True(view.IsDirty);
            Assert.Equal("The disk is full.", view.Status.Text);
        });
    }

    [Fact]
    public void Close_All_writes_and_closes_every_script()
    {
        UiThread.Run(() =>
        {
            var (_, first, second, view) = OpenTwo();
            Pick(view, Logic);
            Edit(view);
            Pick(view, Other);

            Choose(view, "Close All");

            Assert.Empty(view.Tabs);
            Assert.Null(view.Current);
            Assert.Equal(1, first.Saves);
            Assert.True(first.Disposed);
            Assert.True(second.Disposed);
        });
    }

    [Fact]
    public void Save_All_writes_every_script_that_has_edits()
    {
        UiThread.Run(() =>
        {
            var (_, first, second, view) = OpenTwo();
            Pick(view, Logic);
            Edit(view);
            Pick(view, Other);
            Assert.False(Item(view, "Save").IsEnabled);
            Assert.True(Item(view, "Save All").IsEnabled);

            Choose(view, "Save All");

            Assert.Equal(1, first.Saves);
            Assert.Equal(0, second.Saves);
            Assert.False(view.IsDirty);
            Assert.False(Item(view, "Save All").IsEnabled);
        });
    }

    [Fact]
    public void The_menu_offers_only_what_the_state_allows()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();

            foreach (var header in new[] { "Save", "Save All", "Close", "Close All", "Copy Path" })
            {
                Assert.False(Item(view, header).IsEnabled);
            }

            Assert.True(Item(view, "New...").IsEnabled);
            Assert.True(Item(view, "Show in FileSystem").IsEnabled);

            Pick(view, Logic);

            Assert.True(Item(view, "Close").IsEnabled);
            Assert.True(Item(view, "Close All").IsEnabled);
            Assert.True(Item(view, "Copy Path").IsEnabled);
        });
    }

    [Fact]
    public void Copy_Path_puts_the_path_on_the_clipboard()
    {
        UiThread.Run(() =>
        {
            var clipboard = new MemoryClipboard();
            var files = new Files();
            files.Graphs[Logic] = FakeGraphSource.Sample();
            var view = Open(files, clipboard);
            Pick(view, Logic);

            Choose(view, "Copy Path");

            Assert.Equal(Logic, clipboard.Text);
            Assert.Equal($"Copied {Logic}.", view.Status.Text);
        });
    }

    [Fact]
    public void Show_in_FileSystem_says_it_is_not_built()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            string? said = null;
            view.AddHandler(Unbuilt.Event, (_, e) => said = e.Message);

            Choose(view, "Show in FileSystem");

            Assert.Equal("The FileSystem dock does not list graph scripts yet.", said);
        });
    }

    [Fact]
    public void New_opens_the_form_with_the_first_values()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            view.Form.InheritsBox.Text = "Other";
            view.Form.Say("old");

            Choose(view, "New...");

            Assert.True(view.CreateFlyout.IsOpen);
            Assert.Equal("Entity", view.Form.InheritsBox.Text);
            Assert.Equal("res://new_graph.ograph", view.Form.PathBox.Text);
            Assert.Equal("", view.Form.Notice);
        });
    }

    [Fact]
    public void Create_makes_the_file_lists_it_and_opens_it()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Choose(view, "New...");
            view.Form.InheritsBox.Text = " Entity3D ";
            view.Form.PathBox.Text = " res://enemies/slime.ograph ";

            Click(view.Form.CreateButton);

            Assert.Equal([("res://enemies/slime.ograph", "Entity3D")], files.Created);
            Assert.Equal("res://enemies/slime.ograph", view.Current);
            Assert.Contains("res://enemies/slime.ograph", view.List.Items.Cast<string>());
            Assert.False(view.CreateFlyout.IsOpen);
            Assert.Equal("Created res://enemies/slime.ograph.", view.Status.Text);
        });
    }

    [Theory]
    [InlineData(typeof(IOException), "res://logic.ograph exists already.")]
    [InlineData(typeof(UnauthorizedAccessException), "Access denied.")]
    [InlineData(typeof(ArgumentException), "Nope is not a class.")]
    public void A_make_the_engine_refuses_says_why_in_the_form_and_opens_nothing(Type kind, string message)
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            files.CreateError = (Exception)Activator.CreateInstance(kind, message)!;
            Choose(view, "New...");

            Click(view.Form.CreateButton);

            Assert.Equal(message, view.Form.Notice);
            Assert.True(view.CreateFlyout.IsOpen);
            Assert.Null(view.Current);
            Assert.Empty(view.Tabs);
        });
    }

    [Fact]
    public void Create_is_off_while_a_field_is_empty_and_Cancel_closes_the_form()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Choose(view, "New...");
            view.Form.PathBox.Text = "  ";

            Assert.False(view.Form.CreateButton.IsEnabled);

            view.Form.PathBox.Text = "res://a.ograph";
            Assert.True(view.Form.CreateButton.IsEnabled);

            Click(view.Form.CancelButton);
            Assert.False(view.CreateFlyout.IsOpen);
        });
    }

    [Fact]
    public void Showing_the_view_again_lists_new_files_and_keeps_the_open_script_and_its_edits()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Pick(view, Logic);
            Edit(view);

            view.IsVisible = false;
            files.Graphs[Other] = FakeGraphSource.Sample();
            view.IsVisible = true;

            Assert.Equal([Logic, Other], view.List.Items.Cast<string>());
            Assert.Equal(Logic, view.List.SelectedItem);
            Assert.True(view.IsDirty);
        });
    }

    [Fact]
    public void Showing_a_project_again_closes_every_script()
    {
        UiThread.Run(() =>
        {
            var (files, graph, view) = OpenWithGraph();
            Pick(view, Logic);
            Edit(view);

            view.Show(files);

            Assert.Null(view.Current);
            Assert.Empty(view.Tabs);
            Assert.True(graph.Disposed);
            Assert.False(view.IsDirty);
            Assert.Null(view.List.SelectedItem);
        });
    }

    [Fact]
    public void A_graph_the_engine_cannot_load_gets_no_tab_and_says_why()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            files.Graphs["res://gone.ograph"] = FakeGraphSource.Sample();
            view.Show(files);
            Pick(view, Logic);
            files.Graphs.Remove("res://gone.ograph");

            Pick(view, "res://gone.ograph");

            Assert.Contains("No res://gone.ograph.", view.Status.Text);
            Assert.Equal(Logic, view.Current);
            Assert.Equal(["logic.ograph"], view.Tabs.Select(tab => tab.Label.Text));
            Assert.Equal(Logic, view.List.SelectedItem);
        });
    }

    [Fact]
    public void Discarding_counts_a_changed_graph_as_saved_and_writes_nothing()
    {
        UiThread.Run(() =>
        {
            var (_, graph, view) = OpenWithGraph();
            Pick(view, Logic);
            Edit(view);

            view.Discard();

            Assert.False(view.IsDirty);
            Assert.Equal("logic.ograph", view.Tabs.Single().Label.Text);
            Assert.Equal(0, graph.Saves);
        });
    }

    [Fact]
    public void A_breakpoint_set_on_the_canvas_is_kept_when_the_script_is_closed_and_opened_again()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, Logic);
            var step = view.Canvas!.Scene.Find(2)!;
            view.Canvas.Gestures.Press(step.Origin + new Vector(-8, 10), MouseButton.Left, KeyModifiers.None, 1);
            view.Canvas.Gestures.Release(step.Origin + new Vector(-8, 10));
            Assert.True(view.Breakpoints.Has("res://logic.ograph#main", 2));

            Choose(view, "Close");
            Pick(view, Logic);

            Assert.Equal([2], view.Canvas!.Scene.Breakpointed);
        });
    }

    static StepMarks MarksOf(GraphView view, int step) => view.Canvas!.Scene.MarksOf(view.Canvas.Scene.Find(step)!);

    static void Hear(Files files, GraphView view, DebugState state)
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

            Assert.Equal(Logic, view.Current);
            Assert.Equal(new DebugPlace(Logic, "main", 2), view.Canvas!.Scene.StoppedAt);
            Assert.True(MarksOf(view, 2).HasFlag(StepMarks.Current));
            Assert.Equal([2], view.Canvas.Scene.SelectedSteps);
        });
    }

    [Fact]
    public void A_stop_in_a_script_that_is_open_but_hidden_shows_its_tab()
    {
        UiThread.Run(() =>
        {
            var (files, _, _, view) = OpenTwo();
            Pick(view, Logic);
            Pick(view, Other);

            Hear(files, view, StoppedAt(Logic, "main", 2));

            Assert.Equal(Logic, view.Current);
            Assert.Equal(2, view.Tabs.Count());
            Assert.True(MarksOf(view, 2).HasFlag(StepMarks.Current));
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
            Assert.Null(view.Current);
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
            var (files, _, _, view) = OpenTwo();
            Pick(view, Logic);
            Pick(view, Other);
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

    [Fact]
    public void Breakpoints_saved_in_the_project_are_set_when_the_files_are_shown()
    {
        UiThread.Run(() =>
        {
            var files = new Files { BreakpointText = $"{{\"{Logic}#main\":[2]}}" };
            files.Graphs[Logic] = FakeGraphSource.Sample();

            var view = Open(files);
            Pick(view, Logic);

            Assert.True(view.Breakpoints.Has($"{Logic}#main", 2));
            Assert.Equal([2], view.Canvas!.Scene.Breakpointed);
            Assert.Equal("", view.Status.Text);
        });
    }

    [Fact]
    public void A_breakpoint_that_is_set_or_taken_off_is_written_to_the_project()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();

            view.Breakpoints.Toggle($"{Logic}#main", 2);
            Assert.Equal($"{{\"{Logic}#main\":[2]}}", files.BreakpointText);

            view.Breakpoints.Toggle($"{Logic}#main", 2);
            Assert.Equal("{}", files.BreakpointText);
        });
    }

    [Fact]
    public void Showing_the_files_again_writes_each_change_once()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            view.Show(files);

            view.Breakpoints.Toggle($"{Logic}#main", 2);

            Assert.Equal(1, files.BreakpointWrites);
        });
    }

    [Fact]
    public void A_breakpoint_file_that_is_not_breakpoints_gives_none_and_a_status()
    {
        UiThread.Run(() =>
        {
            var files = new Files { BreakpointText = "not json" };

            var view = Open(files);

            Assert.Empty(view.Breakpoints.All);
            Assert.Equal("The saved breakpoints could not be read.", view.Status.Text);
        });
    }

    [Fact]
    public void A_breakpoint_file_that_cannot_be_read_gives_none_and_the_reason()
    {
        UiThread.Run(() =>
        {
            var view = Open(new Files { BreakpointReadFails = true });

            Assert.Empty(view.Breakpoints.All);
            Assert.Equal("The file is locked.", view.Status.Text);
        });
    }

    [Fact]
    public void A_breakpoint_that_cannot_be_written_stays_set_and_the_status_says_so()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            files.BreakpointWriteFails = true;

            view.Breakpoints.Toggle($"{Logic}#main", 2);

            Assert.True(view.Breakpoints.Has($"{Logic}#main", 2));
            Assert.Equal("The breakpoints were not saved. The disk is full.", view.Status.Text);
        });
    }
}

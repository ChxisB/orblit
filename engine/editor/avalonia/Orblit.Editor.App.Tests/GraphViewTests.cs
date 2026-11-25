using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.App.Theme;
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

        public IReadOnlyList<string> AddableClasses() => ["Entity", "Entity3D"];

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

    sealed class Links : ILinkOpener
    {
        public List<Uri> Opened { get; } = [];

        public void Open(Uri link) => Opened.Add(link);
    }

    static GraphView Open(Files files, ITextClipboard? clipboard = null, ILinkOpener? links = null)
    {
        var view = new GraphView(clipboard, links);
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
        view.FileMenu.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetName(item) == header);

    static void Choose(GraphView view, string header) => Item(view, header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static MenuItem GotoItem(GraphView view, string header) =>
        view.GotoMenu.Items.OfType<MenuItem>().Single(item => AutomationProperties.GetName(item) == header);

    static string[] Header(MenuItem item) => [.. ReferenceWindow.Texts((Control)item.Header!)];

    static KeyEventArgs Press(InputElement target, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
        target.RaiseEvent(press);
        return press;
    }

    static void ChooseGoto(GraphView view, string header) => GotoItem(view, header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    static void Edit(GraphView view) => view.Canvas!.Edits.MoveSteps([2], new Vector(10, 0));

    [Fact]
    public void A_listed_graph_script_holds_its_name_in_the_middle_of_its_row()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            view.UpdateLayout();

            var row = view.List.GetVisualDescendants().OfType<ListBoxItem>().Single();
            var name = row.GetVisualDescendants().OfType<TextBlock>().Single();
            var box = ReferenceWindow.BoxOf(name, row);
            Assert.Equal(EditorTheme.RowHeight, row.Bounds.Height);
            Assert.Equal(name.DesiredSize.Height, box.Height);
            Assert.Equal(row.Bounds.Height / 2, box.Center.Y, 0.5);
        });
    }

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
    public void Find_bookmark_and_pause_name_their_keys_and_say_why_they_are_off()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            HoverCard Card(Control control) => (HoverCard)ToolTip.GetTip(control)!;
            Button[] buttons = [view.FindButton, view.BookmarkButton, view.BreakpointButton];

            Assert.Equal<(string, string?)>([("Find a step", "⌘F"), ("Bookmark", "⌘F2"), ("Pause here", "F9")], buttons.Select(button => (Card(button).Title, Card(button).Key)));
            Assert.All(buttons, button => Assert.Equal("Open a graph first.", Card(button).Line));

            Pick(view, Logic);

            Assert.Equal("Finds steps by their names and numbers.", Card(view.FindButton).Line);
        });
    }

    [Fact]
    public void The_bars_name_the_base_type_and_turn_on_once_a_script_is_open_and_save_once_it_is_edited()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Button[] scriptButtons = [view.FindButton, view.BookmarkButton, view.BreakpointButton];

            Assert.Equal("", view.Extends.Text);
            Assert.All(scriptButtons, button => Assert.False(button.IsEnabled));
            Assert.False(view.SaveButton.IsEnabled);

            Pick(view, Logic);

            Assert.Equal("extends Entity", view.Extends.Text);
            Assert.All(scriptButtons, button => Assert.True(button.IsEnabled));
            Assert.False(view.SaveButton.IsEnabled);

            Edit(view);

            Assert.True(view.SaveButton.IsEnabled);
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
            Assert.Equal("No step picked", view.Inspector!.Heading);

            var canvas = view.Canvas!;
            var header = canvas.Scene.Find(3)!.Origin + new Vector(40, 10);
            canvas.Gestures.Press(header, MouseButton.Left, KeyModifiers.None, 1);
            canvas.Gestures.Release(header);
            canvas.Surface.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F13 });

            Assert.Equal("Print text", view.Inspector.Heading);

            Click(view.Panels!.Find<Button>("function:helper:open")!);

            Assert.Equal("No step picked", view.Inspector.Heading);
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
            Assert.Equal("Emit", view.Inspector!.Heading);
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

            Choose(view, "Close all");

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
            Assert.True(Item(view, "Save all").IsEnabled);

            Choose(view, "Save all");

            Assert.Equal(1, first.Saves);
            Assert.Equal(0, second.Saves);
            Assert.False(view.IsDirty);
            Assert.False(Item(view, "Save all").IsEnabled);
        });
    }

    [Fact]
    public void The_menu_offers_only_what_the_state_allows()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();

            foreach (var header in new[] { "Save", "Save all", "Close", "Close all", "Copy path" })
            {
                Assert.False(Item(view, header).IsEnabled);
            }

            Assert.True(Item(view, "New graph…").IsEnabled);

            Pick(view, Logic);

            Assert.True(Item(view, "Close").IsEnabled);
            Assert.True(Item(view, "Close all").IsEnabled);
            Assert.True(Item(view, "Copy path").IsEnabled);
        });
    }

    [Fact]
    public void The_file_menu_says_what_each_item_does_and_what_to_do_first_while_it_is_off()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();

            Assert.Equal(["New graph…", "A script made of steps. You pick what it runs on."], Header(Item(view, "New graph…")));
            Assert.Equal(["Save", "Open a graph first."], Header(Item(view, "Save")));
            Assert.Equal(["Close", "Nothing is open."], Header(Item(view, "Close")));
            Assert.Equal(["Show in Files", "Open a graph first."], Header(Item(view, "Show in Files")));

            Pick(view, Logic);

            Assert.Equal(["Save", "Nothing new to save."], Header(Item(view, "Save")));
            Assert.Equal(["Close", "Saves logic first, then closes it"], Header(Item(view, "Close")));
            Assert.Equal(["Copy path", Logic], Header(Item(view, "Copy path")));
            Assert.Equal(["Show in Files", "Picks the file in the Files panel"], Header(Item(view, "Show in Files")));
            Assert.True(Item(view, "Show in Files").IsEnabled);

            Edit(view);

            Assert.Equal(["Save", "Saves logic"], Header(Item(view, "Save")));
            Assert.Equal(["Save all", "Saves every open graph"], Header(Item(view, "Save all")));
            Assert.Equal(
                [new KeyGesture(Key.S, KeyModifiers.Meta), new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Alt), new KeyGesture(Key.W, KeyModifiers.Meta)],
                new[] { "Save", "Save all", "Close" }.Select(name => Item(view, name).InputGesture));
        });
    }

    [Fact]
    public void The_go_to_menu_names_each_command_with_its_key_and_says_why_it_is_off()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            var items = view.GotoMenu.Items.OfType<MenuItem>().ToList();

            Assert.Equal(["Find a step…", "Go to step number…", "Bookmark this step", "Next bookmark", "Previous bookmark", "Pause here"], items.Select(item => AutomationProperties.GetName(item)));
            Assert.Equal(["⌘F", "⌘L", "⌘F2", "F2", "⇧F2", "F9"], items.Select(item => Parts.KeyText(item.InputGesture!)));
            Assert.All(items, item => Assert.Equal("Open a graph first.", Header(item)[^1]));

            Pick(view, Logic);

            Assert.Equal(["Pause here", "The game stops at this step so you can look around"], Header(GotoItem(view, "Pause here")));
            Assert.Equal(["Next bookmark"], Header(GotoItem(view, "Next bookmark")));
        });
    }

    [Fact]
    public void The_keys_on_the_go_to_menu_work_on_the_canvas()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, Logic);
            var canvas = view.Canvas!;
            canvas.Select([2]);

            Assert.True(Press(canvas.Surface, Key.F9).Handled);
            Assert.Contains(2, canvas.Scene.Breakpointed);
            Assert.True(Press(canvas.Surface, Key.F2, KeyModifiers.Meta).Handled);
            Assert.Contains(2, canvas.Scene.Bookmarked);
            canvas.Select([1]);
            Press(canvas.Surface, Key.F2);
            Assert.Equal([2], canvas.Scene.SelectedSteps);
            canvas.Select([3]);
            Press(canvas.Surface, Key.F2, KeyModifiers.Shift);
            Assert.Equal([2], canvas.Scene.SelectedSteps);

            canvas.FindBar.IsVisible = false;
            Press(canvas.Surface, Key.L, KeyModifiers.Meta);
            Assert.True(canvas.FindBar.IsVisible);
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

            Choose(view, "Copy path");

            Assert.Equal(Logic, clipboard.Text);
            Assert.Equal($"Copied {Logic}.", view.Status.Text);
        });
    }

    [Fact]
    public void New_opens_the_dialog_with_the_first_values()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            view.Form.FolderBox.Text = "res://old";
            view.Form.Say("old");

            Choose(view, "New graph…");

            Assert.True(view.CreateDialog.IsVisible);
            Assert.Equal("new_graph", view.Form.NameBox.Text);
            Assert.Equal("res://", view.Form.FolderBox.Text);
            Assert.Equal("", view.Form.Fault);
        });
    }

    [Fact]
    public void Create_makes_the_file_lists_it_and_opens_it()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Choose(view, "New graph…");
            view.Form.NameBox.Text = "slime";
            view.Form.ExtendsBox.SelectedItem = "Entity3D";
            view.Form.FolderBox.Text = "res://enemies";

            Click(view.Form.CreateButton);

            Assert.Equal([("res://enemies/slime.ograph", "Entity3D")], files.Created);
            Assert.Equal("res://enemies/slime.ograph", view.Current);
            Assert.Contains("res://enemies/slime.ograph", view.List.Items.Cast<string>());
            Assert.False(view.CreateDialog.IsVisible);
            Assert.Equal("Created res://enemies/slime.ograph.", view.Status.Text);
        });
    }

    [Fact]
    public void New_knows_the_graph_scripts_of_the_project()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Choose(view, "New graph…");

            view.Form.NameBox.Text = "logic";

            Assert.Equal("logic.ograph is already in this folder. Pick another name.", view.Form.Fault);
            Assert.False(view.Form.CreateButton.IsEnabled);
            Assert.Empty(files.Created);
        });
    }

    [Theory]
    [InlineData(typeof(IOException), "res://logic.ograph exists already.")]
    [InlineData(typeof(UnauthorizedAccessException), "Access denied.")]
    [InlineData(typeof(ArgumentException), "Nope is not a class.")]
    public void A_make_the_engine_refuses_says_why_in_the_dialog_and_opens_nothing(Type kind, string message)
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            files.CreateError = (Exception)Activator.CreateInstance(kind, message)!;
            Choose(view, "New graph…");

            Click(view.Form.CreateButton);

            Assert.Equal(message, view.Form.Fault);
            Assert.True(view.CreateDialog.IsVisible);
            Assert.Null(view.Current);
            Assert.Empty(view.Tabs);
        });
    }

    [Fact]
    public void Cancel_closes_the_dialog_and_makes_nothing()
    {
        UiThread.Run(() =>
        {
            var (files, _, view) = OpenWithGraph();
            Choose(view, "New graph…");

            Click(view.CreateDialog.CancelButton);

            Assert.False(view.CreateDialog.IsVisible);
            Assert.Empty(files.Created);
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

    [Fact]
    public void The_goto_commands_are_off_until_a_script_is_open()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();

            Assert.All(view.GotoMenu.Items.OfType<MenuItem>(), item => Assert.False(item.IsEnabled));

            Pick(view, Logic);

            Assert.All(view.GotoMenu.Items.OfType<MenuItem>(), item => Assert.True(item.IsEnabled));
        });
    }

    [Fact]
    public void Find_step_and_go_to_step_show_the_find_bar()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, Logic);
            Assert.False(view.Canvas!.FindBar.IsVisible);

            ChooseGoto(view, "Find a step…");
            Assert.True(view.Canvas.FindBar.IsVisible);

            view.Canvas.FindBar.IsVisible = false;
            ChooseGoto(view, "Go to step number…");
            Assert.True(view.Canvas.FindBar.IsVisible);
        });
    }

    [Fact]
    public void Toggle_bookmark_marks_the_selected_step_and_says_so_on_the_find_bar()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, Logic);

            ChooseGoto(view, "Bookmark this step");
            Assert.Equal("Pick one step to bookmark.", view.Canvas!.FindBar.Status);

            view.Canvas.Select([2]);
            ChooseGoto(view, "Bookmark this step");
            Assert.Contains(2, view.Canvas.Scene.Bookmarked);
            Assert.Equal("Bookmarked step 2.", view.Canvas.FindBar.Status);
            Assert.True(view.Canvas.FindBar.IsVisible);

            ChooseGoto(view, "Bookmark this step");
            Assert.DoesNotContain(2, view.Canvas.Scene.Bookmarked);
        });
    }

    [Fact]
    public void Next_and_previous_bookmark_walk_the_bookmarks_and_say_when_there_are_none()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, Logic);

            ChooseGoto(view, "Next bookmark");
            Assert.Equal("No bookmarks yet. Bookmark a step with ⌘F2.", view.Canvas!.FindBar.Status);

            view.Canvas.Select([3]);
            ChooseGoto(view, "Bookmark this step");
            view.Canvas.Select([1]);
            ChooseGoto(view, "Previous bookmark");

            Assert.Equal([3], view.Canvas.Scene.SelectedSteps);
            Assert.Equal("Bookmark 1 of 1", view.Canvas.FindBar.Status);
        });
    }

    [Fact]
    public void Toggle_breakpoint_sets_and_clears_a_breakpoint_on_the_selected_step()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, Logic);

            ChooseGoto(view, "Pause here");
            Assert.Equal("Pick one step to pause at.", view.Canvas!.FindBar.Status);
            Assert.Empty(view.Breakpoints.All);

            view.Canvas.Select([2]);
            ChooseGoto(view, "Pause here");
            Assert.Contains(2, view.Canvas.Scene.Breakpointed);
            Assert.Equal("The game pauses at step 2.", view.Canvas.FindBar.Status);

            ChooseGoto(view, "Pause here");
            Assert.DoesNotContain(2, view.Canvas.Scene.Breakpointed);
            Assert.Equal("The game no longer pauses at step 2.", view.Canvas.FindBar.Status);
        });
    }

    [Fact]
    public void Help_opens_the_guide_and_the_class_reference()
    {
        UiThread.Run(() =>
        {
            var links = new Links();
            var view = Open(new Files(), links: links);
            var help = ((MenuFlyout)view.HelpButton.Flyout!).Items.OfType<MenuItem>().ToList();

            help[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            help[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal([["Your first graph", "A five minute walk through"], ["Every step explained", "What each step does, with an example"]], help.Select(Header));
            Assert.Equal(
                [
                    new Uri("https://orblitengine.com/docs/start/your-first-graph-script/"),
                    new Uri("https://orblitengine.com/docs/classes/class_graphscript/"),
                ],
                links.Opened);
        });
    }

    static GraphView OpenStarted(FakeGraphSource graph)
    {
        graph.Overrides.Add(new GraphStepEntry("override", "", "main", new Dictionary<string, string>()));
        var files = new Files();
        files.Graphs[Logic] = graph;
        var view = Open(files);
        Pick(view, Logic);
        return view;
    }

    [Fact]
    public void What_this_graph_does_reads_the_graph_on_show_and_reads_it_again_after_an_edit()
    {
        UiThread.Run(() =>
        {
            var graph = FakeGraphSource.Sample();
            var view = OpenStarted(graph);
            Assert.Equal(["main", "2 If", "true 3 Print text"], view.Sentences!.Lines);

            graph.AddStep(4, "print");
            graph.Connect(new GraphWire(3, 0, 4, 0));

            Assert.Equal(["main", "2 If", "true 3 Print text", "4 Print text"], view.Sentences.Lines);
        });
    }

    [Fact]
    public void What_this_graph_does_says_to_add_a_start_while_nothing_starts_the_graph()
    {
        UiThread.Run(() =>
        {
            var (_, _, view) = OpenWithGraph();
            Pick(view, Logic);

            Assert.Equal([SentencesCard.NoStart], view.Sentences!.Lines);
        });
    }

    [Fact]
    public void A_step_number_in_what_this_graph_does_picks_that_step_on_the_canvas()
    {
        UiThread.Run(() =>
        {
            var view = OpenStarted(FakeGraphSource.Sample());
            var number = view.Sentences!.NumberOf("main", 3)!;

            number.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal([3], view.Canvas!.Scene.SelectedSteps);
            Assert.Equal("Step 3", ((HoverCard)ToolTip.GetTip(number)!).Title);
        });
    }
}

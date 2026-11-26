using Avalonia;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

public sealed class PaletteTests
{
    static readonly Point Empty = new(2000, 2000);

    static (FakeGraphSource Source, GraphCanvas Canvas) Open(FakeGraphSource? source = null)
    {
        source ??= FakeGraphSource.Sample();
        return (source, new GraphCanvas(source, KeyModifiers.Meta));
    }

    static void Drag(GraphCanvas canvas, Point from, Point to)
    {
        canvas.Gestures.Press(from, MouseButton.Left, KeyModifiers.None, 1);
        canvas.Gestures.Move(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        canvas.Gestures.Move(to);
        canvas.Gestures.Release(to);
    }

    static Point Pin(GraphCanvas canvas, int step, bool isInput, int port) => canvas.Scene.Find(step)!.PinCenter(isInput, port);

    static void Key(Avalonia.Controls.Control target, Key key, KeyModifiers keys = KeyModifiers.None) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = keys });

    static GraphStepEntry Entry(string group, string label) => new(label.ToLowerInvariant(), group, label, new Dictionary<string, string>());

    static string[] Tags(PalettePopup palette) => [.. palette.Rows.Select(row => row.Tag as string ?? "")];

    static string[] Words(Avalonia.Controls.Control control) => [.. control.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>().Select(text => text.Text ?? "")];

    static void Press(Avalonia.Controls.Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    static string[] Labels(GraphCanvas canvas) => [.. canvas.Palette.Entries.Select(entry => entry.Label)];

    static string Where(GraphCanvas canvas, Point screen) =>
        $"{Math.Round(canvas.Viewport.ToGraph(screen).X)} {Math.Round(canvas.Viewport.ToGraph(screen).Y)}";

    [Fact]
    public void Typing_lists_the_steps_that_match()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();

            canvas.OpenPalette(new Point(100, 100), null);
            Assert.True(canvas.Palette.IsOpen);
            Assert.Equal(["Branch", "Translate", "Print", "Constant"], Labels(canvas));

            canvas.Palette.SearchBox.Text = "TRA";

            Assert.Equal(["Translate"], Labels(canvas));
            Assert.Equal(0, canvas.Palette.SelectedIndex);
        });
    }

    [Fact]
    public void A_wire_dragged_from_a_vector_output_offers_only_steps_that_take_a_vector()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(4, "vector", 0, 200, [], [FakeGraphSource.Data("value", 5)]));
            var (_, canvas) = Open(source);

            Drag(canvas, Pin(canvas, 4, false, 0), Empty);

            Assert.True(canvas.Palette.IsOpen);
            Assert.Equal(["Translate"], Labels(canvas));
            Assert.Equal("|Output|5|Data", source.Searches[^1]);
        });
    }

    [Fact]
    public void A_flow_wire_let_go_over_empty_space_offers_the_steps_with_a_flow_input()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();

            Drag(canvas, Pin(canvas, 3, false, 0), Empty);

            Assert.Equal(["Branch", "Translate", "Print"], Labels(canvas));
        });
    }

    [Fact]
    public void The_steps_are_grouped_and_only_the_group_of_the_chosen_step_lists_its_steps()
    {
        UiThread.Run(() =>
        {
            var palette = new PalettePopup();
            IReadOnlyList<GraphStepEntry> found = [Entry("Flow", "Branch"), Entry("Math", "Add"), Entry("Flow", "Loop"), Entry("Math", "Sine")];
            palette.Open(default, new Size(2000, 2000), new PaletteJob(_ => found, (_, _) => { }));

            Assert.Equal(["Branch", "Loop", "Add", "Sine"], palette.Entries.Select(entry => entry.Label));
            Assert.Equal(["palette:group:Flow", "palette:step:0", "palette:step:1", "palette:group:Math"], Tags(palette));
            Assert.StartsWith("Choices and loops If Branch If the check is true", palette.Preview);

            Key(palette.SearchBox, Avalonia.Input.Key.Down);
            Key(palette.SearchBox, Avalonia.Input.Key.Down);

            Assert.Equal(2, palette.SelectedIndex);
            Assert.Equal(["palette:group:Flow", "palette:group:Math", "palette:step:2", "palette:step:3"], Tags(palette));
            Assert.Equal("Math Add", palette.Preview);

            palette.Rows.First().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

            Assert.Equal(0, palette.SelectedIndex);
            Assert.True(palette.IsOpen);
        });
    }

    [Fact]
    public void The_preview_draws_the_chosen_step_with_its_pins_and_starting_values()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);

            var branch = canvas.Palette.PreviewStep!;
            Assert.Equal("branch", branch.Kind);
            Assert.Equal(["run", "condition"], branch.Inputs.Select(port => port.Name));
            Assert.Equal(["true", "false"], branch.Outputs.Select(port => port.Name));

            canvas.Palette.SearchBox.Text = "constant";

            var constant = canvas.Palette.PreviewStep!;
            Assert.Empty(constant.Inputs);
            Assert.Equal("0", constant.Parameters["value"]);

            canvas.Palette.SearchBox.Text = "nothing like it";

            Assert.Null(canvas.Palette.PreviewStep);
        });
    }

    [Fact]
    public void Shift_enter_adds_the_step_and_wires_it_from_the_selected_step()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            canvas.Select([3]);
            canvas.OpenPalette(Empty, null);
            canvas.Palette.SearchBox.Text = "branch";

            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Enter, KeyModifiers.Shift);

            Assert.False(canvas.Palette.IsOpen);
            Assert.Equal(["AddStep 4 branch", $"MoveStep 4 {Where(canvas, Empty)}", "Connect 3.0 4.0"], source.Calls);
        });
    }

    [Fact]
    public void Enter_adds_the_step_without_a_wire_even_with_a_step_selected()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            canvas.Select([3]);
            canvas.OpenPalette(Empty, null);
            canvas.Palette.SearchBox.Text = "branch";

            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Enter);

            Assert.Equal(["AddStep 4 branch", $"MoveStep 4 {Where(canvas, Empty)}"], source.Calls);
        });
    }

    [Fact]
    public void Picking_adds_the_step_at_the_point_with_its_values_and_joins_the_wire()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            Drag(canvas, Pin(canvas, 3, false, 0), Empty);
            canvas.Palette.SearchBox.Text = "print";

            canvas.Palette.Pick();

            Assert.False(canvas.Palette.IsOpen);
            Assert.Equal(["AddStep 4 print", "SetStepParameter 4 text \"\"", $"MoveStep 4 {Where(canvas, Empty)}", "Connect 3.0 4.0"], source.Calls);
            Assert.Equal(4, canvas.Scene.Steps.Count);
            Assert.Equal(3, canvas.Scene.Wires.Count);
        });
    }

    [Fact]
    public void A_step_picked_from_an_input_drag_is_joined_by_its_output()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(4, "print", 0, 200, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));
            var (_, canvas) = Open(source);
            Drag(canvas, Pin(canvas, 4, true, 0), Empty);
            canvas.Palette.SearchBox.Text = "branch";

            canvas.Palette.Pick();

            Assert.Contains("Connect 5.0 4.0", source.Calls);
        });
    }

    [Fact]
    public void A_wire_the_engine_refuses_after_the_step_was_added_is_reported_and_the_step_stays()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            Drag(canvas, Pin(canvas, 2, true, 0), Empty);
            canvas.Palette.SearchBox.Text = "translate";

            canvas.Palette.Pick();

            Assert.Equal(4, canvas.Scene.Steps.Count);
            Assert.True(canvas.NoticeBar.IsVisible);
            Assert.Contains("already has a wire", canvas.NoticeText);
        });
    }

    [Fact]
    public void A_wire_let_go_over_a_pin_or_a_step_opens_no_palette()
    {
        UiThread.Run(() =>
        {
            var source = FakeGraphSource.Sample();
            source.Put(FakeGraphSource.MakeStep(4, "print", 520, 200, [FakeGraphSource.Flow("run")], [FakeGraphSource.Flow("then")]));
            var (_, canvas) = Open(source);

            Drag(canvas, Pin(canvas, 3, false, 0), Pin(canvas, 4, true, 0));
            Assert.False(canvas.Palette.IsOpen);

            Drag(canvas, Pin(canvas, 4, false, 0), canvas.Scene.Find(1)!.Origin + new Vector(40, 10));
            Assert.False(canvas.Palette.IsOpen);
        });
    }

    [Fact]
    public void Steps_and_groups_show_plain_names_with_the_engine_names_dim_beside_them()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);

            var rows = canvas.Palette.Rows.ToList();
            Assert.Equal(["1", "Choices and loops", "Flow"], Words(rows[0]));
            Assert.Equal(["If", "Branch", "Goes one way or the other."], Words(rows[1]));
            Assert.Equal("Find a step, like wait or if", canvas.Palette.SearchBox.PlaceholderText);
            Assert.Contains("Good for: doors that need a key", canvas.Palette.Preview);
        });
    }

    [Fact]
    public void A_plain_name_finds_its_step_first()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);

            canvas.Palette.SearchBox.Text = "if";

            Assert.Equal("branch", canvas.Palette.Entries[0].Kind);
            Assert.StartsWith("Choices and loops If Branch", canvas.Palette.Preview);
        });
    }

    [Fact]
    public void Pin_to_the_top_keeps_the_step_first_until_it_is_unpinned()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);
            canvas.Palette.SearchBox.Text = "print";

            Press(canvas.Palette.PinButton);
            canvas.Palette.SearchBox.Text = "";

            Assert.Equal(("print", "Pinned"), (canvas.Palette.Entries[0].Kind, canvas.Palette.Entries[0].Group));
            Assert.Equal(["palette:group:Pinned", "palette:step:0", "palette:group:Flow"], Tags(canvas.Palette).Take(3));
            Assert.Equal("Unpin", canvas.Palette.PinButton.Content);

            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Escape);
            canvas.OpenPalette(new Point(100, 100), null);
            Assert.Equal("Pinned", canvas.Palette.Entries[0].Group);

            Press(canvas.Palette.PinButton);

            Assert.Equal(["Branch", "Translate", "Print", "Constant"], Labels(canvas));
            Assert.Equal("Pin to the top", canvas.Palette.PinButton.Content);
        });
    }

    [Fact]
    public void A_step_found_only_by_search_has_no_pin()
    {
        UiThread.Run(() =>
        {
            var palette = new PalettePopup();
            palette.Open(default, new Size(2000, 2000), new PaletteJob(text => text == "" ? [Entry("Flow", "Branch")] : [Entry("Calls", "Move")], (_, _) => { }));
            Assert.True(palette.PinButton.IsVisible);

            palette.SearchBox.Text = "move";

            Assert.Equal("Move", palette.Entries[0].Label);
            Assert.False(palette.PinButton.IsVisible);
        });
    }

    [Fact]
    public void The_foot_names_the_step_a_new_one_joins_to()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);
            Assert.Equal("↑ ↓ move ↵ Add here", canvas.Palette.Footer);
            canvas.Palette.Close();

            canvas.Select([3]);
            canvas.OpenPalette(new Point(100, 100), null);
            Assert.Equal("↑ ↓ move ↵ Add here ⇧↵ Add and join to step 3", canvas.Palette.Footer);
            canvas.Palette.Close();

            Drag(canvas, Pin(canvas, 2, false, 1), Empty);
            Assert.Equal("↑ ↓ move ↵ Add and join to step 2", canvas.Palette.Footer);
        });
    }

    [Fact]
    public void Escape_closes_the_palette_and_adds_nothing()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);

            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Escape);

            Assert.False(canvas.Palette.IsOpen);
            Assert.Empty(source.Calls);
        });
    }

    [Fact]
    public void The_arrow_keys_move_the_choice_and_Enter_picks_it()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);

            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Down);
            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Down);
            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Up);
            Assert.Equal(1, canvas.Palette.SelectedIndex);
            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Enter);

            Assert.False(canvas.Palette.IsOpen);
            Assert.Equal("AddStep 4 translate", source.Calls[0]);
        });
    }

    [Fact]
    public void Enter_on_an_empty_list_adds_nothing_and_leaves_the_palette_open()
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);
            canvas.Palette.SearchBox.Text = "no such step";

            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Enter);

            Assert.True(canvas.Palette.IsOpen);
            Assert.Empty(source.Calls);
        });
    }

    [Theory]
    [InlineData(Avalonia.Input.Key.Space, KeyModifiers.None)]
    [InlineData(Avalonia.Input.Key.A, KeyModifiers.Shift)]
    public void Space_or_shift_A_opens_the_palette(Key key, KeyModifiers keys)
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();

            Key(canvas.Surface, key, keys);

            Assert.True(canvas.Palette.IsOpen);
            Assert.Equal(4, canvas.Palette.Entries.Count);
        });
    }

    [Fact]
    public void A_right_click_opens_the_palette_and_a_right_drag_pans_instead()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            var at = new Point(300, 300);

            canvas.Gestures.Press(at, MouseButton.Right, KeyModifiers.None, 1);
            canvas.Gestures.Release(at);
            Assert.True(canvas.Palette.IsOpen);

            var pan = canvas.Viewport.Pan;
            canvas.Gestures.Press(at, MouseButton.Right, KeyModifiers.None, 1);
            canvas.Gestures.Move(at + new Vector(40, 0));
            canvas.Gestures.Release(at + new Vector(40, 0));

            Assert.False(canvas.Palette.IsOpen);
            Assert.Equal(pan.X + 40, canvas.Viewport.Pan.X);
        });
    }

    [Fact]
    public void The_last_search_is_kept_the_next_time_it_opens()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);
            canvas.Palette.SearchBox.Text = "tra";
            Key(canvas.Palette.SearchBox, Avalonia.Input.Key.Escape);

            canvas.OpenPalette(new Point(100, 100), null);

            Assert.Equal("tra", canvas.Palette.SearchBox.Text);
            Assert.Equal(["Translate"], Labels(canvas));
        });
    }

    [Fact]
    public void A_press_on_the_canvas_closes_the_palette()
    {
        UiThread.Run(() =>
        {
            var (_, canvas) = Open();
            canvas.OpenPalette(new Point(100, 100), null);

            canvas.Gestures.Press(Empty, MouseButton.Left, KeyModifiers.None, 1);
            canvas.Gestures.Release(Empty);

            Assert.False(canvas.Palette.IsOpen);
        });
    }

    [Theory]
    [InlineData("File", "res://a \"b\".png", "preload", "path", "\"res://a \\\"b\\\".png\"")]
    [InlineData("Entity", "Player/Arm", "entity_at_path", "path", "\"Player/Arm\"")]
    [InlineData("Variable", "speed", "get", "name", "\"speed\"")]
    [InlineData("Local", "i", "local", "name", "\"i\"")]
    [InlineData("Function", "main", "call_function", "function", "\"main\"")]
    [InlineData("Signal", "hit", "emit", "signal", "\"hit\"")]
    public void A_drop_becomes_the_step_that_reads_it(string kind, string value, string step, string parameter, string text)
    {
        UiThread.Run(() =>
        {
            var (source, canvas) = Open();

            canvas.Edits.AddStep(new GraphDrop(Enum.Parse<GraphDropKind>(kind), value).ToEntry(), new Point(10, 20), null);

            Assert.Equal([$"AddStep 4 {step}", $"SetStepParameter 4 {parameter} {text}", "MoveStep 4 10 20"], source.Calls);
        });
    }

    [Fact]
    public void A_drop_makes_the_text_it_travels_as_and_reads_it_back()
    {
        var drop = new GraphDrop(GraphDropKind.Entity, "A:B");

        Assert.True(GraphDrop.TryParse(drop.ToText(), out var read));
        Assert.Equal(drop, read);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nothing")]
    [InlineData("File:")]
    [InlineData(":value")]
    [InlineData("Bogus:value")]
    [InlineData("99:value")]
    public void Text_that_is_not_a_drop_is_refused(string? text)
    {
        Assert.False(GraphDrop.TryParse(text, out _));
    }
}

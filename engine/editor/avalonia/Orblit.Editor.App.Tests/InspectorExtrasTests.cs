using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests.Graph;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class InspectorExtrasTests
{
    static EditorWindow Selected(FakeScene scene)
    {
        var window = OpenAtReferenceSize(scene);
        window.Outline.SelectedItem = FakeScene.Lamp;
        window.UpdateLayout();
        return window;
    }

    static MetadataRow[] Rows(EditorWindow window) => [.. window.Inspector.MetadataRows.Rows];

    static readonly string[] SlotItems = ["Load...", "Edit", "Clear"];

    static void Choose(MenuFlyout menu, string header) =>
        menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static string Line(Control control) => ((HoverCard)ToolTip.GetTip(control)!).Line;

    static void PressEnter(Control control) => control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

    static void Type(TextBox box, string text)
    {
        box.Text = text;
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
    }

    [Fact]
    public void An_entity_with_no_metadata_shows_no_row_and_the_button_keeps_its_place()
    {
        UiThread.Run(() =>
        {
            var window = Selected(new FakeScene());

            Assert.Empty(Rows(window));
            Assert.True(window.Inspector.MetadataButton.IsEffectivelyVisible);
        });
    }

    [Fact]
    public void Each_metadata_value_gets_a_row_of_its_kind_in_the_order_the_scene_lists_them()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "label", "crate");
            scene.SetMetadata(1, "count", 3L);
            scene.SetMetadata(1, "weight", 1.5);
            scene.SetMetadata(1, "fragile", true);
            var window = Selected(scene);
            var rows = Rows(window);

            Assert.Equal(["label", "count", "weight", "fragile"], rows.Select(row => row.Key));
            Assert.Equal(["crate", "3", "1.5"], rows.Take(3).Select(row => row.Box!.Text));
            Assert.True(rows[3].Check!.IsChecked);
            Assert.All(rows, row => Assert.True(row.IsEffectivelyVisible));
        });
    }

    [Fact]
    public void Another_entity_shows_its_own_metadata()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "label", "crate");
            var window = Selected(scene);
            Assert.Single(Rows(window));

            window.Outline.SelectedItem = FakeScene.Sound;
            window.UpdateLayout();

            Assert.Empty(Rows(window));
        });
    }

    [Fact]
    public void Typing_a_new_string_sets_it_and_marks_the_scene_edited()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "label", "crate");
            var window = Selected(scene);

            Type(Rows(window).Single().Box!, "barrel");

            Assert.Equal([new MetaEntry("label", "barrel")], scene.MetadataOf(1));
            Assert.StartsWith("(*)", window.Title);
        });
    }

    [Fact]
    public void A_number_keeps_its_kind_and_text_that_is_no_number_is_put_back()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "count", 3L);
            scene.SetMetadata(1, "weight", 1.5);
            var window = Selected(scene);
            var rows = Rows(window);

            Type(rows[0].Box!, "7");
            Type(rows[1].Box!, "2.25");
            Assert.Equal([new MetaEntry("count", 7L), new MetaEntry("weight", 2.25)], scene.MetadataOf(1));

            Type(Rows(window)[0].Box!, "2.5");
            Type(Rows(window)[1].Box!, "heavy");
            Type(Rows(window)[1].Box!, "NaN");
            Assert.Equal([new MetaEntry("count", 7L), new MetaEntry("weight", 2.25)], scene.MetadataOf(1));
            Assert.Equal(["7", "2.25"], Rows(window).Select(row => row.Box!.Text));
        });
    }

    [Fact]
    public void An_unchanged_value_is_not_an_edit()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "label", "crate");
            var window = Selected(scene);
            var title = window.Title;

            Type(Rows(window).Single().Box!, "crate");

            Assert.Equal(title, window.Title);
        });
    }

    [Fact]
    public void A_check_box_sets_a_bool()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "fragile", false);
            var window = Selected(scene);

            Rows(window).Single().Check!.IsChecked = true;

            Assert.Equal([new MetaEntry("fragile", true)], scene.MetadataOf(1));
        });
    }

    [Fact]
    public void The_remove_button_drops_the_value_and_its_row()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "label", "crate");
            scene.SetMetadata(1, "count", 3L);
            var window = Selected(scene);

            ClickOn(Rows(window)[0].RemoveButton, window);

            Assert.Equal([new MetaEntry("count", 3L)], scene.MetadataOf(1));
            Assert.Equal(["count"], Rows(window).Select(row => row.Key));
            Assert.StartsWith("(*)", window.Title);
        });
    }

    [Fact]
    public void Add_a_custom_value_opens_a_flyout_with_the_name_box_and_the_kind()
    {
        UiThread.Run(() =>
        {
            var window = Selected(new FakeScene());

            ClickOn(window.Inspector.MetadataButton, window);

            Assert.True(window.Inspector.MetadataFlyout.IsOpen);
            Assert.Same(window.Inspector.MetadataTool, window.Inspector.MetadataFlyout.Content);
        });
    }

    [Fact]
    public void Add_is_off_until_the_name_has_a_letter_and_each_kind_starts_from_its_empty_value()
    {
        UiThread.Run(() =>
        {
            var tool = new MetadataTool();
            var made = new List<(string Name, object Value)>();
            tool.Requested += (name, value) => made.Add((name, value));
            Assert.False(tool.AddButton.IsEnabled);

            tool.NameBox.Text = "  ";
            Assert.False(tool.AddButton.IsEnabled);

            tool.NameBox.Text = " tag ";
            Assert.True(tool.AddButton.IsEnabled);
            foreach (var kind in new[] { 0, 1, 2, 3 })
            {
                tool.Kind.SelectedIndex = kind;
                tool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }

            Assert.Equal([("tag", (object)""), ("tag", 0L), ("tag", 0.0), ("tag", false)], made);
        });
    }

    [Theory]
    [InlineData("", "Give it a name first.")]
    [InlineData("door code", "Letters, numbers and _ only.")]
    [InlineData("2nd", "Letters, numbers and _ only.")]
    [InlineData("door_code", "Stores the value on this thing.")]
    public void Add_says_why_it_is_off_and_the_name_rule_turns_red(string name, string line)
    {
        UiThread.Run(() =>
        {
            var tool = new MetadataTool();

            tool.NameBox.Text = name;

            Assert.Equal(line == "Stores the value on this thing.", tool.AddButton.IsEnabled);
            Assert.Equal(line, Line(tool.AddButton));
            Assert.Equal(line == "Letters, numbers and _ only.", tool.NameBox.Classes.Contains(EditorTheme.Bad));
        });
    }

    [Fact]
    public void Enter_adds_the_value_while_Add_is_on()
    {
        UiThread.Run(() =>
        {
            var tool = new MetadataTool();
            var made = new List<string>();
            tool.Requested += (name, _) => made.Add(name);

            PressEnter(tool);
            tool.NameBox.Text = "tag";
            PressEnter(tool);

            Assert.Equal(["tag"], made);
        });
    }

    [Fact]
    public void Adding_through_the_flyout_sets_the_value_shows_its_row_and_closes_the_flyout()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Selected(scene);
            ClickOn(window.Inspector.MetadataButton, window);

            window.Inspector.MetadataTool.NameBox.Text = "weight";
            window.Inspector.MetadataTool.Kind.SelectedIndex = 2;
            window.Inspector.MetadataTool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal([new MetaEntry("weight", 0.0)], scene.MetadataOf(1));
            Assert.Equal(["weight"], Rows(window).Select(row => row.Key));
            Assert.False(window.Inspector.MetadataFlyout.IsOpen);
            Assert.Equal("", window.Inspector.MetadataTool.NameBox.Text);
            Assert.StartsWith("(*)", window.Title);
        });
    }

    [Fact]
    public void A_refused_name_shows_the_reason_and_changes_nothing()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { MetadataFails = true };
            var window = Selected(scene);
            var title = window.Title;
            ClickOn(window.Inspector.MetadataButton, window);

            window.Inspector.MetadataTool.NameBox.Text = "_hidden";
            window.Inspector.MetadataTool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("_hidden is not a metadata name.", window.Status);
            Assert.Empty(Rows(window));
            Assert.Equal(title, window.Title);
            Assert.True(window.Inspector.MetadataFlyout.IsOpen);
            Assert.Equal("_hidden", window.Inspector.MetadataTool.NameBox.Text);
        });
    }

    [Theory]
    [InlineData(0, "hi there", "hi there")]
    [InlineData(1, " 42 ", 42L)]
    [InlineData(2, "-2.5", -2.5)]
    [InlineData(3, "True", true)]
    [InlineData(1, "", 0L)]
    public void A_typed_value_is_read_as_the_chosen_kind(int kind, string typed, object value)
    {
        UiThread.Run(() =>
        {
            var tool = new MetadataTool();
            var made = new List<(string Name, object Value)>();
            tool.Requested += (name, value) => made.Add((name, value));

            tool.NameBox.Text = "tag";
            tool.Kind.SelectedIndex = kind;
            tool.ValueBox.Text = typed;
            tool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(("tag", value), Assert.Single(made));
        });
    }

    [Fact]
    public void A_value_the_kind_cannot_read_turns_Add_off_and_says_why_until_the_kind_fits()
    {
        UiThread.Run(() =>
        {
            var tool = new MetadataTool();
            tool.NameBox.Text = "tag";
            tool.Kind.SelectedIndex = 1;

            tool.ValueBox.Text = "2.5";
            Assert.False(tool.AddButton.IsEnabled);
            Assert.True(tool.Fault.IsVisible);
            Assert.Equal("Type a whole number.", tool.Fault.Text);
            Assert.Contains(EditorTheme.Bad, tool.ValueBox.Classes);

            tool.Kind.SelectedIndex = 2;
            Assert.True(tool.AddButton.IsEnabled);
            Assert.False(tool.Fault.IsVisible);
            Assert.DoesNotContain(EditorTheme.Bad, tool.ValueBox.Classes);
        });
    }

    [Fact]
    public void The_kinds_are_a_segment_row_in_plain_words_with_Text_chosen_first()
    {
        UiThread.Run(() =>
        {
            var tool = new MetadataTool();

            Assert.Equal(["Text", "Whole number", "Number", "Yes or no"], tool.Kind.Choices.Select(choice => (string?)choice.Content));
            Assert.Equal(["String", "int", "float", "bool"], tool.Kind.Choices.Select(choice => ((HoverCard)ToolTip.GetTip(choice)!).Engine));
            Assert.Equal([true, false, false, false], tool.Kind.Choices.Select(choice => choice.IsChecked == true));
            Assert.Equal(EditorBrushes.Field, tool.Kind.Background);
            Assert.Equal(320, tool.Width);
        });
    }

    [Fact]
    public void The_popover_says_what_a_custom_value_is_for_and_Cancel_closes_it()
    {
        UiThread.Run(() =>
        {
            var window = Selected(new FakeScene());
            var tool = window.Inspector.MetadataTool;
            ClickOn(window.Inspector.MetadataButton, window);

            Assert.Equal(("Add a custom value", "Metadata"), (tool.Head.Heading.Text, tool.Head.EngineName));
            Assert.Equal("Store your own data on Lamp, like a score or a door code. Scripts can read it.", tool.Head.Why.Text);
            tool.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(window.Inspector.MetadataFlyout.IsOpen);
        });
    }

    [Fact]
    public void A_typed_edit_keeps_its_row_so_the_box_keeps_the_typing_focus()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "label", "crate");
            var window = Selected(scene);
            var row = Rows(window).Single();

            Type(row.Box!, "barrel");

            Assert.Same(row, Rows(window).Single());
        });
    }

    [Fact]
    public void A_script_choice_made_after_the_selection_went_changes_nothing()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Selected(scene);
            ClickOn(window.Inspector.ScriptField, window);
            var title = window.Title;
            window.Outline.SelectedItem = null;

            Choose(window.Inspector.ScriptMenu, "res://spin.cs");

            Assert.Null(scene.ScriptOf(1));
            Assert.Equal(title, window.Title);
        });
    }

    [Fact]
    public void A_filter_hides_the_metadata_rows_with_the_other_parts()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.SetMetadata(1, "label", "crate");
            var window = Selected(scene);

            window.InspectorPane.FilterBox.Text = "pos";
            Assert.False(Rows(window).Single().IsEffectivelyVisible);

            window.InspectorPane.FilterBox.Text = "";
            Assert.True(Rows(window).Single().IsEffectivelyVisible);
        });
    }

    [Fact]
    public void The_script_field_says_empty_until_a_script_is_attached_then_names_its_file()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Selected(scene);
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "None");

            scene.Scripted[1] = "res://enemies/slime.cs";
            window.Outline.SelectedItem = FakeScene.Sound;
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.UpdateLayout();

            var texts = window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
            Assert.Contains("slime.cs", texts);
            Assert.DoesNotContain("None", texts);
        });
    }

    [Fact]
    public void The_script_field_opens_a_menu_of_the_project_scripts_and_Clear()
    {
        UiThread.Run(() =>
        {
            var window = Selected(new FakeScene());

            ClickOn(window.Inspector.ScriptField, window);

            Assert.True(window.Inspector.ScriptMenu.IsOpen);
            Assert.Equal(["res://spin.cs", "res://enemies/slime.cs", .. SlotItems], window.Inspector.ScriptMenu.Items.OfType<MenuItem>().Select(item => (string?)item.Header));
        });
    }

    [Fact]
    public void Add_a_script_starts_on_a_new_graph_named_after_the_thing()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.Graphs["res://sum.ograph"] = FakeGraphSource.Sample();
            var window = Selected(scene);
            var tool = window.Inspector.ScriptTool;

            ClickOn(window.Inspector.AttachButton, window);

            Assert.True(window.Inspector.AttachFlyout.IsOpen);
            Assert.Equal(("Add a script to Lamp", "A script tells Lamp what to do while the game runs."), (tool.Head.Heading.Text, tool.Head.Why.Text));
            Assert.Equal(("Graph", "Join steps with wires. No code needed."), ((string?)tool.Kind.Choices[0].Content, tool.KindLine.Text));
            Assert.Equal(("LampGraph", "Saved as res://LampGraph.ograph"), (tool.NameBox.Text, tool.SavedLine.Text));
            Assert.True(tool.AddButton.IsEnabled);
            Assert.Equal(["res://sum.ograph"], tool.List.ItemsSource!.Cast<string>());

            tool.Kind.SelectedIndex = 1;

            Assert.Equal(("LampScript", "Saved as res://LampScript.cs"), (tool.NameBox.Text, tool.SavedLine.Text));
            Assert.Equal(["res://spin.cs", "res://enemies/slime.cs"], tool.List.ItemsSource!.Cast<string>());
        });
    }

    [Fact]
    public void A_script_chosen_in_the_popover_is_attached_and_the_popover_closes()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Selected(scene);
            var tool = window.Inspector.ScriptTool;
            ClickOn(window.Inspector.AttachButton, window);
            tool.Kind.SelectedIndex = 1;
            tool.Source.SelectedIndex = 1;
            Assert.False(tool.AddButton.IsEnabled);
            Assert.Equal("Pick a script in the list first.", Line(tool.AddButton));

            tool.List.SelectedItem = "res://enemies/slime.cs";
            Assert.True(tool.AddButton.IsEnabled);
            tool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("res://enemies/slime.cs", scene.ScriptOf(1));
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "slime.cs");
            Assert.False(window.Inspector.AttachFlyout.IsOpen);
            Assert.StartsWith("(*)", window.Title);
        });
    }

    [Fact]
    public void A_kind_with_no_scripts_says_so()
    {
        UiThread.Run(() =>
        {
            var window = Selected(new FakeScene());
            var tool = window.Inspector.ScriptTool;
            ClickOn(window.Inspector.AttachButton, window);

            tool.Source.SelectedIndex = 1;
            Assert.False(tool.List.IsVisible);
            Assert.True(tool.None.IsEffectivelyVisible);
            Assert.Equal("No graph scripts yet.", tool.None.Text);
            Assert.Equal("No graph scripts yet. Make a new one instead.", Line(tool.AddButton));

            tool.MakeInsteadButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(0, tool.Source.SelectedIndex);
            Assert.True(tool.NameBox.IsEffectivelyVisible);

            tool.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.False(window.Inspector.AttachFlyout.IsOpen);
        });
    }

    [Fact]
    public void Making_a_new_graph_writes_it_and_attaches_it()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Selected(scene);
            var tool = window.Inspector.ScriptTool;
            ClickOn(window.Inspector.AttachButton, window);

            PressEnter(tool);

            Assert.Contains("res://LampGraph.ograph", scene.Graphs.Keys);
            Assert.Equal("res://LampGraph.ograph", scene.ScriptOf(1));
            Assert.False(window.Inspector.AttachFlyout.IsOpen);
            Assert.StartsWith("(*)", window.Title);
        });
    }

    [Fact]
    public void Making_a_new_C_sharp_script_writes_it_on_the_class_of_the_thing()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Selected(scene);
            var tool = window.Inspector.ScriptTool;
            ClickOn(window.Inspector.AttachButton, window);
            tool.Kind.SelectedIndex = 1;
            tool.NameBox.Text = "Spinner";

            tool.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("res://Spinner.cs", Assert.Single(scene.ScriptsWritten).Path);
            Assert.Contains(": Entity3D", scene.ScriptsWritten[0].Text);
            Assert.Equal("res://Spinner.cs", scene.ScriptOf(1));
        });
    }

    [Theory]
    [InlineData("", "Give it a name first.")]
    [InlineData("my script", "Letters, numbers and _ only.")]
    [InlineData("spin", "spin.cs already exists. Pick another name.")]
    public void A_new_script_name_says_why_Add_is_off(string name, string why)
    {
        UiThread.Run(() =>
        {
            var tool = new ScriptTool();
            tool.Show("Lamp", ["res://spin.cs"], []);
            tool.Kind.SelectedIndex = 1;

            tool.NameBox.Text = name;

            Assert.False(tool.AddButton.IsEnabled);
            Assert.Equal(why, Line(tool.AddButton));
        });
    }

    [Fact]
    public void Showing_the_tool_for_another_thing_drops_the_name_typed_before()
    {
        UiThread.Run(() =>
        {
            var tool = new ScriptTool();
            tool.Show("Lamp", [], []);
            tool.NameBox.Text = "Spinner";

            tool.Show("Sound", [], []);

            Assert.Equal("SoundGraph", tool.NameBox.Text);
        });
    }

    [Fact]
    public void The_script_menu_lists_graph_scripts_after_the_C_sharp_ones_and_attaches_one()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            scene.Graphs["res://sum.ograph"] = FakeGraphSource.Sample();
            var window = Selected(scene);

            ClickOn(window.Inspector.ScriptField, window);

            Assert.Equal(["res://spin.cs", "res://enemies/slime.cs", "res://sum.ograph", .. SlotItems], window.Inspector.ScriptMenu.Items.OfType<MenuItem>().Select(item => (string?)item.Header));
            Choose(window.Inspector.ScriptMenu, "res://sum.ograph");
            Assert.Equal("res://sum.ograph", scene.ScriptOf(1));
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "sum.ograph");
        });
    }

    [Fact]
    public void Choosing_a_script_attaches_it_shows_its_file_and_marks_the_scene_edited()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Selected(scene);
            ClickOn(window.Inspector.ScriptField, window);

            Choose(window.Inspector.ScriptMenu, "res://spin.cs");

            Assert.Equal("res://spin.cs", scene.ScriptOf(1));
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "spin.cs");
            Assert.StartsWith("(*)", window.Title);
        });
    }

    [Fact]
    public void Edit_opens_the_attached_script_in_its_workspace_and_is_off_while_none_is_attached()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { ScriptText = { ["res://spin.cs"] = "class Spin {}" } };
            var window = Selected(scene);
            ClickOn(window.Inspector.ScriptField, window);
            Assert.False(window.Inspector.ScriptMenu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Edit").IsEnabled);

            Choose(window.Inspector.ScriptMenu, "res://spin.cs");
            window.Inspector.ScriptMenu.Hide();
            ClickOn(window.Inspector.ScriptField, window);
            Choose(window.Inspector.ScriptMenu, "Edit");

            Assert.Equal(["Script"], window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));
            Assert.Equal("res://spin.cs", window.Script.Heading.Text);
        });
    }

    [Fact]
    public void Load_opens_the_attach_popover()
    {
        UiThread.Run(() =>
        {
            var window = Selected(new FakeScene());
            ClickOn(window.Inspector.ScriptField, window);

            Choose(window.Inspector.ScriptMenu, "Load...");

            Assert.True(window.Inspector.AttachFlyout.IsOpen);
            Assert.Equal("A script tells Lamp what to do while the game runs.", window.Inspector.ScriptTool.Head.Why.Text);
        });
    }

    [Fact]
    public void Clear_detaches_the_script_and_is_off_while_none_is_attached()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = Selected(scene);
            ClickOn(window.Inspector.ScriptField, window);
            Assert.False(window.Inspector.ScriptMenu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Clear").IsEnabled);

            Choose(window.Inspector.ScriptMenu, "res://spin.cs");
            window.Inspector.ScriptMenu.Hide();
            ClickOn(window.Inspector.ScriptField, window);
            Assert.True(window.Inspector.ScriptMenu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Clear").IsEnabled);
            Choose(window.Inspector.ScriptMenu, "Clear");

            Assert.Null(scene.ScriptOf(1));
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "None");
        });
    }

    [Fact]
    public void A_script_the_engine_cannot_load_shows_the_reason_and_leaves_the_field_as_it_was()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene { AttachFails = true };
            var window = Selected(scene);
            var title = window.Title;
            ClickOn(window.Inspector.ScriptField, window);

            Choose(window.Inspector.ScriptMenu, "res://spin.cs");

            Assert.Equal("The engine could not load res://spin.cs.", window.Status);
            Assert.Null(scene.ScriptOf(1));
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "None");
            Assert.Equal(title, window.Title);
        });
    }
}

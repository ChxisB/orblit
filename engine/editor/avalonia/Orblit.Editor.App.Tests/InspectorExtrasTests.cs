using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
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

    static void Choose(MenuFlyout menu, string header) =>
        menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

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
    public void Add_Metadata_opens_a_flyout_with_the_name_box_and_the_kind()
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
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "<empty>");

            scene.Scripted[1] = "res://enemies/slime.cs";
            window.Outline.SelectedItem = FakeScene.Sound;
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.UpdateLayout();

            var texts = window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
            Assert.Contains("slime.cs", texts);
            Assert.DoesNotContain("<empty>", texts);
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
            Assert.Equal(["res://spin.cs", "res://enemies/slime.cs", "Clear"], window.Inspector.ScriptMenu.Items.OfType<MenuItem>().Select(item => (string?)item.Header));
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
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "<empty>");
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
            Assert.Contains(window.Inspector.ScriptField.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "<empty>");
            Assert.Equal(title, window.Title);
        });
    }
}

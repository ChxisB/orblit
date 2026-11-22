using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class NewSceneToolTests
{
    static readonly string[] Classes = ["Camera3D", "Entity", "Timer"];

    // The dialog laid out in a window of its own, so the cards have their templates and the radio group its parent.
    static NewSceneTool Opened(params string[] scenes) => Opened(false, scenes);

    static NewSceneTool Opened(bool planar, params string[] scenes)
    {
        var tool = new NewSceneTool();
        new Window { Width = 1000, Height = 700, Content = tool.Dialog }.Show();
        tool.Open(scenes, planar, Classes);
        tool.Dialog.UpdateLayout();
        return tool;
    }

    static IEnumerable<string> Texts(Control control) => control.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? "");

    static ContentPresenter Presenter(Button button) =>
        button.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_ContentPresenter");

    [Fact]
    public void The_dialog_says_what_it_is_for_and_Create_shows_its_key()
    {
        UiThread.Run(() =>
        {
            var tool = Opened();

            Assert.Equal("New Scene", tool.Dialog.Title);
            Assert.Equal("Pick what the scene is for. You can change its root later.", tool.Dialog.Why.Text);
            Assert.Equal(420, tool.Dialog.CardWidth);
            Assert.Equal(["Create", "↵"], Texts(tool.CreateButton));
        });
    }

    [Fact]
    public void The_cards_offer_three_roots_and_Other_which_searches_every_class()
    {
        UiThread.Run(() =>
        {
            var tool = Opened();

            Assert.Equal(
                [["3D scene", "Root Entity3D"], ["2D scene", "Root Entity2D"], ["Empty", "Root Entity"]],
                tool.Roots.Select(card => Texts(card).ToArray()));
            Assert.Equal(["Other...", "Any class"], Texts(tool.OtherCard));
            Assert.Equal([true, false, false], tool.Roots.Select(card => card.IsChecked == true));
            Assert.False(tool.OtherClasses.IsVisible);

            tool.OtherCard.IsChecked = true;

            Assert.True(tool.OtherClasses.IsVisible);
            Assert.Equal(Classes, tool.OtherClasses.Shown.Select(choice => choice.Name));
            Assert.Equal([false, false, false], tool.Roots.Select(card => card.IsChecked == true));
        });
    }

    [Fact]
    public void Other_makes_the_root_of_the_lit_class_and_waits_for_one()
    {
        UiThread.Run(() =>
        {
            var tool = Opened();
            (string Path, string Class)? asked = null;
            tool.Requested += (path, root) => asked = (path, root);
            tool.NameBox.Text = "clock";
            tool.OtherCard.IsChecked = true;

            tool.OtherClasses.SearchBox.Text = "zzz";
            Assert.False(tool.CreateButton.IsEnabled);

            tool.OtherClasses.SearchBox.Text = "tim";
            Click(tool.CreateButton);

            Assert.Equal(("res://clock.oscn", "Timer"), asked);
        });
    }

    [Fact]
    public void The_root_starts_on_the_kind_of_the_workspace_each_time_it_opens()
    {
        UiThread.Run(() =>
        {
            var tool = Opened(planar: true);
            Assert.Equal([false, true, false], tool.Roots.Select(card => card.IsChecked == true));

            tool.Roots.Last().IsChecked = true;
            tool.Dialog.Close();
            tool.Open([], planar: false, Classes);

            Assert.Equal([true, false, false], tool.Roots.Select(card => card.IsChecked == true));
        });
    }

    [Theory]
    [InlineData(0, "Entity3D")]
    [InlineData(1, "Entity2D")]
    [InlineData(2, "Entity")]
    public void Each_root_makes_its_scene_at_the_project_root(int card, string className)
    {
        UiThread.Run(() =>
        {
            var tool = Opened();
            (string Path, string Class)? asked = null;
            tool.Requested += (path, root) => asked = (path, root);
            // A click raises the event and does not check the card, so the test checks it as a press would.
            tool.Roots.ElementAt(card).IsChecked = true;
            tool.NameBox.Text = " arena ";

            Click(tool.CreateButton);

            Assert.Equal(("res://arena.oscn", className), asked);
            Assert.Single(tool.Roots, root => root.IsChecked == true);
        });
    }

    [Fact]
    public void The_path_follows_the_name_and_Create_waits_for_one()
    {
        UiThread.Run(() =>
        {
            var tool = Opened();
            Assert.Equal("", tool.PathBox.Text);
            Assert.False(tool.CreateButton.IsEnabled);

            tool.NameBox.Text = "arena";
            Assert.Equal("res://arena.oscn", tool.PathBox.Text);
            Assert.True(tool.CreateButton.IsEnabled);

            tool.NameBox.Text = "   ";
            Assert.Equal("", tool.PathBox.Text);
            Assert.False(tool.CreateButton.IsEnabled);
        });
    }

    [Fact]
    public void A_name_that_is_a_scene_already_says_so_and_keeps_Create_off()
    {
        UiThread.Run(() =>
        {
            var tool = Opened("res://field.oscn", "res://levels/");

            tool.NameBox.Text = "field";

            Assert.Equal("field.oscn already exists. Pick another name.", tool.Fault);
            Assert.Contains(EditorTheme.Bad, tool.NameBox.Classes);
            Assert.False(tool.CreateButton.IsEnabled);

            tool.NameBox.Text = "field2";

            Assert.Equal("", tool.Fault);
            Assert.DoesNotContain(EditorTheme.Bad, tool.NameBox.Classes);
            Assert.True(tool.CreateButton.IsEnabled);
        });
    }

    [Fact]
    public void Open_starts_again_with_no_name_and_no_fault()
    {
        UiThread.Run(() =>
        {
            var tool = Opened();
            tool.NameBox.Text = "arena";
            tool.Say("The engine could not save res://arena.oscn. Error code 7.");
            tool.Dialog.Close();

            tool.Open([], planar: false, Classes);

            Assert.True(tool.Dialog.IsVisible);
            Assert.Equal("", tool.NameBox.Text);
            Assert.Equal("", tool.Fault);
        });
    }

    [Fact]
    public void The_chosen_card_is_drawn_in_the_accent()
    {
        UiThread.Run(() =>
        {
            var tool = Opened();
            var chosen = Presenter(tool.Roots.First());
            var other = Presenter(tool.Roots.Last());

            Assert.Equal(EditorColors.AccentSoft, ((ISolidColorBrush)chosen.Background!).Color);
            Assert.Equal(EditorColors.Accent, ((ISolidColorBrush)chosen.BorderBrush!).Color);
            Assert.NotEqual(EditorColors.AccentSoft, ((ISolidColorBrush)other.Background!).Color);
        });
    }
}

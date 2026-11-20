using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class CreateGraphFormTests
{
    static CreateGraphForm Opened(params string[] scripts)
    {
        var form = new CreateGraphForm();
        new Window { Width = 1000, Height = 700, Content = form.Dialog }.Show();
        form.Open(scripts);
        form.Dialog.UpdateLayout();
        return form;
    }

    static (string Path, string Base)? Create(CreateGraphForm form)
    {
        (string, string)? asked = null;
        form.Requested += (path, baseType) => asked = (path, baseType);
        Click(form.CreateButton);
        return asked;
    }

    [Fact]
    public void The_dialog_opens_with_the_first_values_and_the_name_selected()
    {
        UiThread.Run(() =>
        {
            var form = Opened();

            Assert.Equal("New Graph Script", form.Dialog.Title);
            Assert.Equal("A script made of steps.", form.Dialog.Why.Text);
            Assert.Equal(380, form.Dialog.CardWidth);
            Assert.Equal("Create", form.Dialog.MainText);
            Assert.Equal("new_graph", form.NameBox.Text);
            Assert.Equal((0, 9), (form.NameBox.SelectionStart, form.NameBox.SelectionEnd));
            Assert.Equal(".ograph", ((TextBlock)form.NameBox.InnerRightContent!).Text);
            Assert.Equal("Entity", form.ExtendsBox.SelectedItem);
            Assert.Equal("res://", form.FolderBox.Text);
            Assert.Equal("", form.Fault);
        });
    }

    [Fact]
    public void Extends_marks_a_3D_base_and_a_2D_base_in_their_colours()
    {
        UiThread.Run(() =>
        {
            var form = Opened();

            IBrush? Mark(string name) => TextElement.GetForeground(((Panel)form.ExtendsBox.ItemTemplate!.Build(name)!).Children.OfType<EditorIcon>().Single());

            Assert.Equal(["Entity", "Entity2D", "Entity3D", "Marker3D", "MeshInstance3D", "Camera3D"], form.ExtendsBox.Items.Cast<string>());
            Assert.Same(EditorBrushes.Spatial, Mark("Camera3D"));
            Assert.Same(EditorBrushes.Planar, Mark("Entity2D"));
            Assert.Same(EditorBrushes.TextSoft, Mark("Entity"));
        });
    }

    [Theory]
    [InlineData(" res://enemies ")]
    [InlineData("res://enemies/")]
    public void Create_gives_the_path_in_the_folder_and_the_base(string folder)
    {
        UiThread.Run(() =>
        {
            var form = Opened();
            form.NameBox.Text = " slime ";
            form.ExtendsBox.SelectedItem = "Entity3D";
            form.FolderBox.Text = folder;

            Assert.Equal(("res://enemies/slime.ograph", "Entity3D"), Create(form));
        });
    }

    [Theory]
    [InlineData("  ", "res://")]
    [InlineData("slime", " ")]
    public void Create_is_off_while_the_name_or_the_folder_is_blank(string name, string folder)
    {
        UiThread.Run(() =>
        {
            var form = Opened();
            form.NameBox.Text = name;
            form.FolderBox.Text = folder;

            Assert.False(form.CreateButton.IsEnabled);
        });
    }

    [Fact]
    public void A_name_that_is_a_graph_script_already_says_so_and_keeps_Create_off()
    {
        UiThread.Run(() =>
        {
            var form = Opened("res://player_brain.ograph");

            form.NameBox.Text = "player_brain";

            Assert.Equal("player_brain.ograph already exists. Pick another name.", form.Fault);
            Assert.Contains(EditorTheme.Bad, form.NameBox.Classes);
            Assert.False(form.CreateButton.IsEnabled);

            form.FolderBox.Text = "res://ai";

            Assert.Equal("", form.Fault);
            Assert.True(form.CreateButton.IsEnabled);
        });
    }

    [Fact]
    public void A_Create_that_is_off_is_drawn_grey()
    {
        UiThread.Run(() =>
        {
            var form = Opened();
            form.NameBox.Text = "";
            form.Dialog.UpdateLayout();

            var presenter = form.CreateButton.GetVisualDescendants().OfType<ContentPresenter>().Single(part => part.Name == "PART_ContentPresenter");

            Assert.Equal(EditorColors.DisabledFill, ((ISolidColorBrush)presenter.Background!).Color);
            Assert.Equal(EditorColors.Line, ((ISolidColorBrush)presenter.BorderBrush!).Color);
            Assert.Equal(EditorColors.Disabled, ((ISolidColorBrush)presenter.Foreground!).Color);
        });
    }
}

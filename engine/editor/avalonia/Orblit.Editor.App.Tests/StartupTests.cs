using Avalonia.Controls;

namespace Orblit.Editor.App.Tests;

public sealed class StartupTests
{
    static FakeScene Project() => new() { ProjectName = "Demo", SceneFile = "res://field.oscn" };

    static EditorWindow Starting()
    {
        var window = new EditorWindow();
        window.Show();
        window.ShowProject(Project());
        return window;
    }

    static string ViewportText(EditorWindow window) => ((TextBlock)window.Viewport.Content!).Text!;

    [Fact]
    public void The_project_shows_from_disk_before_the_engine_starts()
    {
        UiThread.Run(() =>
        {
            var window = Starting();

            Assert.Equal("field.oscn - Demo", window.Title);
            var root = Assert.Single(window.Scenes.Tree.ItemsSource!.Cast<FileNode>());
            Assert.Contains(root.Children, node => node.Path == "res://field.oscn");
            Assert.True(window.SceneTabs.Tab.IsVisible);
            Assert.Equal("field", window.SceneTabs.Label.Text);
            Assert.False(window.SceneTabs.CloseButton.IsEnabled);
            Assert.True(window.SceneTabs.CloseButton.Opacity < 1);
        });
    }

    [Fact]
    public void The_parts_that_wait_for_the_engine_say_loading_in_place()
    {
        UiThread.Run(() =>
        {
            var window = Starting();

            Assert.True(window.ScenePane.Loading.IsVisible);
            Assert.False(window.ScenePane.Empty.IsVisible);
            Assert.True(window.InspectorPane.Loading.IsVisible);
            Assert.False(window.InspectorPane.Empty.IsVisible);
            Assert.StartsWith("Loading", ViewportText(window), StringComparison.Ordinal);
            Assert.False(window.Toolbar.IsVisible);
            Assert.False(window.Scenes.IsHitTestVisible);
        });
    }

    [Fact]
    public void Loading_the_scene_takes_the_loading_words_away()
    {
        UiThread.Run(() =>
        {
            var window = Starting();

            window.Load(Project());

            Assert.False(window.ScenePane.Loading.IsVisible);
            Assert.False(window.InspectorPane.Loading.IsVisible);
            Assert.True(window.InspectorPane.Empty.IsVisible);
            Assert.StartsWith("No viewport", ViewportText(window), StringComparison.Ordinal);
            Assert.True(window.Toolbar.IsVisible);
            Assert.True(window.Scenes.IsHitTestVisible);
            Assert.Equal(1, window.SceneTabs.CloseButton.Opacity);
        });
    }

    [Fact]
    public void A_start_that_fails_says_so_and_stops_saying_loading()
    {
        UiThread.Run(() =>
        {
            var window = Starting();

            window.FailToStart("No engine library.");

            Assert.Equal("No engine library.", window.Status);
            Assert.False(window.ScenePane.Loading.IsVisible);
            Assert.False(window.InspectorPane.Loading.IsVisible);
            Assert.Equal("No engine library.", ViewportText(window));
        });
    }

    [Fact]
    public void A_project_on_disk_lists_its_name_files_and_kinds()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(folder, "project.orblit"), "[application]\nconfig/name=\"Demo\"\n");
            File.WriteAllText(Path.Combine(folder, "main.oscn"), "[node name=\"Main\" type=\"Entity3D\"]\n");
            File.WriteAllText(Path.Combine(folder, "hero.cs"), "");

            var project = new DiskProject(new EditorOptions(folder, "res://main.oscn", true));

            Assert.Equal("Demo", project.ProjectName);
            Assert.Equal("res://main.oscn", project.SceneFile);
            Assert.Equal(["res://main.oscn"], project.Scenes());
            Assert.Equal(["res://hero.cs"], project.Scripts());
            Assert.Equal("3D", project.SceneKindOf("res://main.oscn"));
            Assert.Equal(File.GetLastWriteTimeUtc(Path.Combine(folder, "main.oscn")), project.ModifiedOf("res://main.oscn"));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}

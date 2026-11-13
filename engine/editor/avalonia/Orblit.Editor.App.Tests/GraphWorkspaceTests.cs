using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Tests.Graph;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class GraphWorkspaceTests
{
    const string Logic = "res://logic.ograph";

    static (EditorWindow Window, FakeScene Scene, FakeGraphSource Graph) Open()
    {
        var scene = new FakeScene();
        var graph = FakeGraphSource.Sample();
        scene.Graphs[Logic] = graph;
        var window = new EditorWindow();
        window.Show();
        window.Load(scene);
        window.UpdateLayout();
        return (window, scene, graph);
    }

    static void ShowGraph(EditorWindow window) => ClickOn(window.Bar.Tabs.Single(tab => tab.Label == "Graph"), window);

    // Opens the Graph tab and moves a step, which is a change no file holds yet.
    static void EditAGraph(EditorWindow window)
    {
        ShowGraph(window);
        window.Graph.List.SelectedItem = Logic;
        window.Graph.Canvas!.Edits.MoveSteps([2], new Vector(10, 0));
    }

    [Fact]
    public void The_Graph_tab_swaps_the_toolbar_and_viewport_for_the_graph_view()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Open();

            ShowGraph(window);
            window.UpdateLayout();

            Assert.Equal(["Graph"], window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label));
            Assert.True(window.Graph.IsVisible);
            Assert.False(window.Script.IsVisible);
            Assert.False(window.Center.Toolbar.IsVisible);
            Assert.False(window.Viewport.IsEffectivelyVisible);
            Assert.Equal([Logic], window.Graph.List.Items.Cast<string>());
        });
    }

    [Fact]
    public void The_Script_tab_lists_no_graph_scripts()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Open();

            ClickOn(window.Bar.Tabs.Single(tab => tab.Label == "Script"), window);

            Assert.DoesNotContain(Logic, (IEnumerable<string>)window.Script.List.ItemsSource!);
        });
    }

    [Fact]
    public void Save_writes_an_edited_graph_with_the_scene()
    {
        UiThread.Run(() =>
        {
            var (window, scene, graph) = Open();
            EditAGraph(window);
            Assert.True(window.Graph.IsDirty);

            Click(window.SaveButton);

            Assert.Equal(1, graph.Saves);
            Assert.Equal(1, scene.Saves);
            Assert.False(window.Graph.IsDirty);
        });
    }

    [Fact]
    public void Closing_with_an_edited_graph_keeps_the_window_open_and_asks()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Open();
            EditAGraph(window);

            window.Close();

            Assert.True(window.IsVisible);
            Assert.True(window.CloseBar.IsVisible);
        });
    }

    [Fact]
    public void Save_and_close_writes_only_the_graph_when_the_scene_has_no_edits()
    {
        UiThread.Run(() =>
        {
            var (window, scene, graph) = Open();
            EditAGraph(window);
            window.Close();

            Click(window.CloseBar.SaveAndCloseButton);

            Assert.Equal(1, graph.Saves);
            Assert.Equal(0, scene.Saves);
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void Close_without_saving_drops_the_graph_edit_and_closes()
    {
        UiThread.Run(() =>
        {
            var (window, _, graph) = Open();
            EditAGraph(window);
            window.Close();

            Click(window.CloseBar.CloseWithoutButton);

            Assert.Equal(0, graph.Saves);
            Assert.False(window.IsVisible);
        });
    }

    [Fact]
    public void A_failed_graph_write_on_save_and_close_keeps_the_window_open_and_says_why()
    {
        UiThread.Run(() =>
        {
            var (window, _, graph) = Open();
            EditAGraph(window);
            graph.SaveFails = true;
            window.Close();

            Click(window.CloseBar.SaveAndCloseButton);

            Assert.True(window.IsVisible);
            Assert.True(window.Graph.IsDirty);
            Assert.Equal("The disk is full.", window.Graph.Status.Text);
            Assert.Equal("The disk is full.", window.Status);
        });
    }

    [Fact]
    public void The_Graph_workspace_is_a_tab_beside_Script_in_the_title_bar()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Open();

            var labels = window.Bar.Tabs.Select(tab => tab.Label).ToList();

            Assert.Equal(labels.IndexOf("Script") + 1, labels.IndexOf("Graph"));
        });
    }
}

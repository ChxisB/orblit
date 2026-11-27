using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

    static KeyEventArgs Press(InputElement target, Key key, KeyModifiers modifiers)
    {
        var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
        target.RaiseEvent(press);
        return press;
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
    public void Command_W_in_the_Graph_tab_closes_the_graph_and_leaves_the_scene_open()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _) = Open();
            ShowGraph(window);
            window.Graph.List.SelectedItem = Logic;

            Assert.True(Press(window, Key.W, window.CommandKey).Handled);

            Assert.Null(window.Graph.Current);
            Assert.Equal(0, scene.Closed);
        });
    }

    [Fact]
    public void Option_command_S_in_the_Graph_tab_saves_every_open_graph()
    {
        UiThread.Run(() =>
        {
            var (window, _, graph) = Open();
            EditAGraph(window);

            Assert.True(Press(window, Key.S, window.CommandKey | KeyModifiers.Alt).Handled);

            Assert.Equal(1, graph.Saves);
        });
    }

    [Fact]
    public void Show_in_Files_shows_the_Files_panel_and_picks_the_graph()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = Open();
            ShowGraph(window);
            window.Graph.List.SelectedItem = Logic;
            Assert.False(window.LeftSide.IsVisible);

            Click(window.Graph.ShowInFilesButton);

            Assert.True(window.LeftSide.IsVisible);
            Assert.Equal(0, window.FileDock.SelectedIndex);
            Assert.Equal(Logic, ((FileNode)window.Scenes.Tree.SelectedItem!).Path);
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
            Assert.True(window.SaveChanges.IsVisible);
            Assert.Equal("Save logic.ograph?", window.SaveChanges.Title);
            Assert.Equal(["logic.ograph", "res://logic.ograph · Changes not saved"], Texts(window.ChangesList));
        });
    }

    [Fact]
    public void Save_changes_lists_an_edited_script_and_graph_and_saves_both_on_one_button()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _) = Open();
            scene.ScriptText["res://spin.cs"] = "class Spin\n{\n}";
            EditAGraph(window);
            ClickOn(window.Bar.Tabs.Single(tab => tab.Label == "Script"), window);
            window.Script.List.SelectedItem = "res://spin.cs";
            window.Script.Editor.Text = "class Spin\n{\n  int turns;\n  int speed;\n}";

            window.Close();

            var dialog = window.SaveChanges;
            Assert.Equal("Save 2 scripts?", dialog.Title);
            Assert.Equal("They have changes that are not saved.", dialog.Why.Text);
            Assert.Equal(["spin.cs", "res://spin.cs · 2 lines changed", "logic.ograph", "res://logic.ograph · Changes not saved"], Texts(window.ChangesList));
            Assert.Equal(["Save 2 and close", "↵"], Texts(dialog.MainButton));
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

            Click(window.SaveChanges.MainButton);

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

            Click(window.SaveChanges.LeadButton!);

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

            Click(window.SaveChanges.MainButton);

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

    [Fact]
    public void The_run_keys_press_the_run_bar_buttons()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _) = Open();
            ShowGraph(window);
            scene.Channel.Next = FakeDebugChannel.Running();
            window.Graph.PollDebugger();

            Assert.True(Press(window, Key.Y, window.CommandKey).Handled);
            scene.Channel.Next = FakeDebugChannel.Stopped("breakpoint", FakeDebugChannel.Frame(Logic, "main", 2));
            window.Graph.PollDebugger();
            Press(window, Key.F10, KeyModifiers.None);
            Press(window, Key.F11, KeyModifiers.None);
            Press(window, Key.F11, KeyModifiers.Shift);
            Press(window, Key.F12, KeyModifiers.None);

            Assert.Equal(["break", "next", "step", "out", "continue"], scene.Channel.Sent);
        });
    }
}

using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class WorkspaceTests
{
    static WorkspaceTab Tab(EditorWindow window, string label) => window.Bar.Tabs.Single(tab => tab.Label == label);

    static string[] Lit(EditorWindow window) => [.. window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label)];

    [Fact]
    public void The_3D_tab_brings_the_toolbar_and_viewport_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            ClickOn(Tab(window, "Game"), window);

            ClickOn(Tab(window, "3D"), window);
            window.UpdateLayout();

            Assert.Equal(["3D"], Lit(window));
            Assert.False(window.Game.IsVisible);
            Assert.True(window.Center.Toolbar.IsVisible);
            Assert.True(window.Viewport.IsEffectivelyVisible);
            AssertBox(293, 86, 2818, 1142, BoxOf(window.Viewport, window));
        });
    }

    [Fact]
    public void The_workspace_tabs_are_2D_3D_Script_Graph_and_Game()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(["2D", "3D", "Script", "Graph", "Game"], window.Bar.Tabs.Select(tab => tab.Label));
        });
    }

    [Fact]
    public void Each_tab_raises_WorkspaceChosen_with_its_name()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var chosen = new List<string>();
            window.Bar.WorkspaceChosen += chosen.Add;

            ClickOn(Tab(window, "2D"), window);
            ClickOn(Tab(window, "Graph"), window);
            ClickOn(Tab(window, "Game"), window);
            ClickOn(Tab(window, "3D"), window);

            Assert.Equal(["2D", "Graph", "Game", "3D"], chosen);
        });
    }
}

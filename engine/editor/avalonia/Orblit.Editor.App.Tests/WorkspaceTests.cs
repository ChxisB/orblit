using Avalonia.Threading;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class WorkspaceTests
{
    const string OneKit = """{"formatVersion":1,"kits":[{"name":"Platformer","publisher":"ada","slug":"platformer","version":"1.2.0"}]}""";

    static WorkspaceTab Tab(EditorWindow window, string label) => window.Bar.Tabs.Single(tab => tab.Label == label);

    static string[] Lit(EditorWindow window) => [.. window.Bar.Tabs.Where(tab => tab.IsChecked == true).Select(tab => tab.Label)];

    [Fact]
    public void The_Asset_Store_tab_swaps_the_toolbar_and_viewport_for_the_store()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var before = window.Status;
            Assert.False(window.Store.IsVisible);

            ClickOn(Tab(window, "Asset Store"), window);
            window.UpdateLayout();

            Assert.Equal(["Asset Store"], Lit(window));
            Assert.True(window.Store.IsVisible);
            Assert.False(window.Center.Toolbar.IsVisible);
            Assert.False(window.Viewport.IsEffectivelyVisible);
            AssertBox(287, 74, 2866, 1157, BoxOf(window.Store, window));
            Assert.Equal(before, window.Status);
        });
    }

    [Fact]
    public void The_3D_tab_brings_the_toolbar_and_viewport_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            ClickOn(Tab(window, "Asset Store"), window);

            ClickOn(Tab(window, "3D"), window);
            window.UpdateLayout();

            Assert.Equal(["3D"], Lit(window));
            Assert.False(window.Store.IsVisible);
            Assert.True(window.Center.Toolbar.IsVisible);
            Assert.True(window.Viewport.IsEffectivelyVisible);
            AssertBox(287, 116, 2866, 1115, BoxOf(window.Viewport, window));
        });
    }

    [Fact]
    public void The_store_keeps_its_rows_across_a_switch_away_and_back()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene(), _ => Task.FromResult(OneKit));
            ClickOn(Tab(window, "Asset Store"), window);
            ClickOn(window.Store.LoadButton, window);
            Dispatcher.UIThread.RunJobs();

            ClickOn(Tab(window, "3D"), window);
            ClickOn(Tab(window, "Asset Store"), window);

            Assert.Equal("1 kit.", window.Store.Status.Text);
        });
    }

    [Fact]
    public void The_window_hands_the_store_the_reader_it_was_given()
    {
        UiThread.Run(() =>
        {
            var asked = 0;
            var window = OpenAtReferenceSize(new FakeScene(), _ => { asked++; return Task.FromResult(OneKit); });

            ClickOn(Tab(window, "Asset Store"), window);
            Assert.Equal(0, asked);
            ClickOn(window.Store.LoadButton, window);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, asked);
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
            ClickOn(Tab(window, "Asset Store"), window);
            ClickOn(Tab(window, "Game"), window);
            ClickOn(Tab(window, "3D"), window);

            Assert.Equal(["2D", "Asset Store", "Game", "3D"], chosen);
        });
    }
}

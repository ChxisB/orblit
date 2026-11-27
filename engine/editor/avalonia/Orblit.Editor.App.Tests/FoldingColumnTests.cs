using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Orblit.Editor.App.Launcher;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class FoldingColumnTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_columns_{Guid.NewGuid():N}");

    EditorSettings Settings => new(Path.Combine(_folder, EditorSettings.FileName));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    static void Choose(string workspace, EditorWindow window)
    {
        ClickOn(window.Bar.Tabs.Single(tab => tab.Label == workspace), window);
        window.UpdateLayout();
    }

    static IReadOnlyList<FoldingColumn> ColumnsOf(string workspace, EditorWindow window) => workspace switch
    {
        TitleBar.Script => window.Script.Columns,
        TitleBar.Graph => window.Graph.Columns,
        _ => window.Game.Columns,
    };

    [Theory]
    [InlineData(TitleBar.Script, 0, "Scripts")]
    [InlineData(TitleBar.Graph, 0, "Graphs")]
    [InlineData(TitleBar.Graph, 1, "Step")]
    [InlineData(TitleBar.Game, 0, "Play settings")]
    [InlineData(TitleBar.Game, 1, "Speed and live scene")]
    public void A_column_folds_to_a_rail_and_comes_back(string workspace, int at, string title)
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Choose(workspace, window);
            var column = ColumnsOf(workspace, window)[at];
            var width = column.Bounds.Width;
            var box = BoxOf(column, window);
            // The handle is half over the edge that faces the middle, as on a docked panel.
            var left = Grid.GetColumn(column) == 0;
            var edge = left ? box.Right : box.X;
            AssertBox(edge - 12, box.Y + DockTabs.StripHeight - 12, 24, 24, BoxOf(column.CollapseButton, window));
            Assert.Equal("Chevrons", Assert.IsType<EditorIcon>(column.CollapseButton.Content).IconName);

            var outside = new Point(edge + (left ? 10 : -10), box.Y + DockTabs.StripHeight);
            window.MouseDown(outside, MouseButton.Left);
            window.MouseUp(outside, MouseButton.Left);
            window.UpdateLayout();

            Assert.Equal(DockSide.RailWidth, column.Bounds.Width);
            Assert.False(column.CollapseButton.IsEffectivelyVisible);
            Assert.True(column.RailButton.IsEffectivelyVisible);
            Assert.Equal(title, AutomationProperties.GetName(column.RailButton));

            Click(column.RailButton);
            window.UpdateLayout();

            Assert.Equal(width, column.Bounds.Width);
            Assert.True(column.CollapseButton.IsEffectivelyVisible);
            Assert.False(column.RailButton.IsEffectivelyVisible);
        });
    }

    [Fact]
    public void A_folded_column_gives_its_room_to_the_middle()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Choose(TitleBar.Script, window);
            var column = window.Script.Columns[0];
            var editor = window.Script.Editor.Bounds.Width;
            var width = column.Bounds.Width;

            Click(column.CollapseButton);
            window.UpdateLayout();

            Assert.Equal(editor + width - DockSide.RailWidth, window.Script.Editor.Bounds.Width);
        });
    }

    [Fact]
    public void Folded_columns_come_back_on_the_next_run()
    {
        UiThread.Run(() =>
        {
            EditorWindow Open()
            {
                var window = new EditorWindow(settings: Settings) { Width = 1920, Height = 1080 };
                window.Show();
                window.Load(new FakeScene());
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                return window;
            }

            var first = Open();
            Click(first.Script.Columns[0].CollapseButton);
            Click(first.Game.Columns[1].CollapseButton);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            // Saved as soon as it changes, not only on close.
            Assert.Equal("Scripts,Speed and live scene", Settings.Section("layout")["folded"]);
            first.Close();

            var next = Open();

            Assert.True(next.Script.Columns[0].Folded);
            Assert.True(next.Game.Columns[1].Folded);
            Assert.False(next.Game.Columns[0].Folded);
            Assert.False(next.Graph.Columns[0].Folded);
        });
    }
}

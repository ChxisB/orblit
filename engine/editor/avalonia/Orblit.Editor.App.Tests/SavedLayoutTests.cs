using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Orblit.Editor.App.Launcher;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class SavedLayoutTests : IDisposable
{
    static readonly (string Title, DockSlot Home)[] s_panels =
    [
        ("Scene", DockSlot.UpperLeft),
        ("Import", DockSlot.UpperLeft),
        ("Files", DockSlot.LowerLeft),
        ("History", DockSlot.LowerLeft),
        ("Inspector", DockSlot.UpperRight),
        ("Signals", DockSlot.UpperRight),
    ];

    readonly string _folder = Path.Combine(Path.GetTempPath(), $"orblit_layout_{Guid.NewGuid():N}");

    EditorSettings Settings => new(Path.Combine(_folder, EditorSettings.FileName));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    static SavedLayout Read(params (string Key, string Value)[] values) =>
        SavedLayout.Read(values.ToDictionary(each => each.Key, each => each.Value), s_panels)!;

    static string Docked(SavedLayout layout) =>
        string.Join(" ", layout.Groups.Where(group => group.Window is null).Select(group => $"{group.Slot}={string.Join("+", group.Titles)}"));

    [Fact]
    public void No_section_leaves_the_layout_as_it_is()
    {
        Assert.Null(SavedLayout.Read(new Dictionary<string, string>(), s_panels));
    }

    [Fact]
    public void What_is_written_reads_back()
    {
        var written = new Dictionary<string, string>
        {
            ["upper_left"] = "Scene,Signals",
            ["lower_left"] = "Files,History",
            ["upper_right"] = "Inspector",
            ["float_1"] = "Import|LowerRight|120 80 320 400",
            ["chosen"] = "Signals,History,Inspector,Import",
            ["hidden"] = "Files",
            ["collapsed"] = "LowerLeft",
            ["locked"] = "true",
            ["folded"] = "Scripts,Step",
            ["left_width"] = "300",
            ["right_width"] = "420",
            ["left_split"] = "0.3",
            ["right_split"] = "0.5",
        };

        var layout = SavedLayout.Read(written, s_panels)!;

        Assert.Equal(written, layout.Values().ToDictionary(each => each.Key, each => each.Value));
        Assert.Equal(new FloatSpot(new PixelPoint(120, 80), new Size(320, 400)), layout.Groups.Single(group => group.Window is not null).Window);
    }

    [Fact]
    public void Every_panel_ends_up_exactly_once()
    {
        // Scene is named twice, Rubbish is no panel, and four panels are not named.
        var layout = Read(("lower_right", "Scene,Rubbish"), ("upper_left", "Scene,Inspector"));

        Assert.Equal("UpperLeft=Scene+Inspector+Import LowerLeft=Files+History UpperRight=Signals", Docked(layout));
    }

    [Theory]
    [InlineData("Files|Nowhere|1 2 300 300")]
    [InlineData("Files|LowerLeft|1 2 300")]
    [InlineData("Files|LowerLeft|a b c d")]
    [InlineData("Files")]
    public void A_window_that_cannot_be_read_sends_its_panels_home(string value)
    {
        var layout = Read(("float_1", value));

        Assert.All(layout.Groups, group => Assert.Null(group.Window));
        Assert.Contains("Files", layout.Groups.Single(group => group.Slot == DockSlot.LowerLeft).Titles);
    }

    [Fact]
    public void Bad_values_give_the_default_and_sizes_are_held_within_bounds()
    {
        var layout = Read(("locked", "maybe"), ("collapsed", "Middle,7"), ("left_width", "wide"), ("right_width", "999999"), ("left_split", "NaN"), ("right_split", "7"));

        Assert.False(layout.Locked);
        Assert.Empty(layout.Collapsed);
        Assert.Null(layout.LeftWidth);
        Assert.Equal(4000, layout.RightWidth);
        Assert.Null(layout.LeftSplit);
        Assert.Equal(0.9, layout.RightSplit);
    }

    [Theory]
    [InlineData(200, 100, 200, 100)]
    [InlineData(-30000, -30000, 50, 60)]
    [InlineData(1919, 1079, 50, 60)]
    public void A_window_off_every_screen_comes_back_over_the_editor(int x, int y, int atX, int atY)
    {
        PixelRect[] screens = [new(0, 0, 1920, 1080)];

        Assert.Equal(new PixelPoint(atX, atY), SavedLayout.OnScreen(new PixelPoint(x, y), screens, new PixelPoint(50, 60)));
    }

    EditorWindow Open()
    {
        var window = new EditorWindow(settings: Settings) { Width = 1920, Height = 1080 };
        window.Show();
        window.Load(new FakeScene());
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    static void Press(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    static string Groups(EditorWindow window) => string.Join(
        " ",
        new[] { "Scene", "Import", "Files", "History", "Inspector", "Signals" }
            .Select(window.Docks.GroupOf)
            .Distinct()
            .Select(group => $"{group.Slot}={string.Join("+", group.Titles)}"));

    [Fact]
    public void A_layout_comes_back_on_the_next_run()
    {
        UiThread.Run(() =>
        {
            var first = Open();
            // Signals goes in with Files, Import floats, the right column collapses, and the layout locks.
            first.Docks.Drop(first.InspectorDock.Tabs[1], new DropTarget.Join(first.FileDock, first.FileDock, default), default);
            first.Docks.Drop(first.SceneDock.Tabs[1], new DropTarget.Float(first, default), new PixelPoint(100, 90));
            Click(first.InspectorDock.CollapseButton);
            first.FileDock.Select(1);
            Click(first.SceneTabs.LockButton);
            Dispatcher.UIThread.RunJobs();
            first.UpdateLayout();
            var groups = Groups(first);
            first.Close();
            // The floating window closes with the editor and puts its panel back. That must not be saved.
            Dispatcher.UIThread.RunJobs();

            var next = Open();

            Assert.Equal(groups, Groups(next));
            Assert.Equal("History", next.Docks.GroupOf("History").Chosen.Title);
            Assert.True(next.RightSide.IsFolded);
            Assert.True(next.Docks.Locked);
            var floating = Assert.IsType<Window>(TopLevel.GetTopLevel(next.Docks.GroupOf("Import")));
            Assert.NotSame(next, floating);
            Assert.True(floating.IsVisible);
            Assert.False(floating.CanResize);
        });
    }

    [Fact]
    public void Column_widths_and_a_hidden_tab_come_back()
    {
        UiThread.Run(() =>
        {
            var first = Open();
            var splitter = first.LeftSide.Splitter;
            var from = splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, 200), first)!.Value;
            Avalonia.Headless.HeadlessWindowExtensions.MouseDown(first, from, Avalonia.Input.MouseButton.Left);
            Avalonia.Headless.HeadlessWindowExtensions.MouseMove(first, from + new Point(120, 0), Avalonia.Input.RawInputModifiers.LeftMouseButton);
            Avalonia.Headless.HeadlessWindowExtensions.MouseUp(first, from + new Point(120, 0), Avalonia.Input.MouseButton.Left);
            first.UpdateLayout();
            first.Docks.ToggleTab("History");
            var width = first.LeftSide.Bounds.Width;
            Assert.Equal(400, width);
            first.Close();

            var next = Open();

            Assert.Equal(width, next.LeftSide.Bounds.Width);
            Assert.False(next.Docks.GroupOf("History").IsShown("History"));
        });
    }

    [Fact]
    public void A_change_is_saved_before_the_editor_closes()
    {
        UiThread.Run(() =>
        {
            var window = Open();

            Click(window.FileDock.CollapseButton);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("LowerLeft", Settings.Section("layout")["collapsed"]);
        });
    }

    [Fact]
    public void The_editor_keys_work_in_a_floating_window()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Press(window.FileDock.FloatItem);
            var floating = Assert.IsType<Window>(TopLevel.GetTopLevel(window.FileDock));

            floating.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.OemPipe, KeyModifiers = window.CommandKey });
            window.UpdateLayout();

            Assert.True(window.Docks.AllCollapsed);
        });
    }
}

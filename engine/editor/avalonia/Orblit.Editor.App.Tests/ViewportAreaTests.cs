using System.Numerics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class ViewportAreaTests
{
    static bool NoFrame(out int width, out int height, ref byte[] rgba)
    {
        width = 0;
        height = 0;
        return false;
    }

    static (EditorWindow Window, ViewportControl Viewport) OpenWithViewport()
    {
        var window = OpenAtReferenceSize();
        var viewport = new ViewportControl(() => false, NoFrame);
        window.ShowViewport(viewport);
        window.UpdateLayout();
        viewport.Tick();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return (window, viewport);
    }

    static Control[] Items(Border pill) => [.. ((StackPanel)pill.Child!).Children];

    static Border[] Pills(EditorWindow window) => [.. window.Toolbar.Tools.Children.Cast<Border>()];

    static FixedToggle[] Tools(EditorWindow window) => [.. Pills(window).SelectMany(Items).OfType<FixedToggle>()];

    static bool[] Lit(EditorWindow window) => [.. Tools(window).Take(5).Select(mode => mode.IsChecked == true)];

    static MenuItem[] MoreItems(EditorWindow window) => [.. ((MenuFlyout)window.Toolbar.MoreButton.Flyout!).Items.Cast<MenuItem>()];

    static Color Backing(ToggleButton button) =>
        ((ISolidColorBrush)button.GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Name == "PART_ContentPresenter").Background!).Color;

    static Color IconColor(Visual owner) =>
        ((ISolidColorBrush)TextElement.GetForeground(owner.GetVisualDescendants().OfType<EditorIcon>().First())!).Color;

    [Fact]
    public void The_scene_tab_is_raised_on_a_fine_edge_and_shows_the_root()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var strip = window.SceneTabs;
            var tab = BoxOf(strip.Tab, window);

            Assert.True(strip.Tab.IsVisible);
            Assert.Equal([299.0, 53.0, 26.0], [tab.X, tab.Y, tab.Height]);
            Assert.Equal(
                (EditorBrushes.Raised, EditorBrushes.LineStrong, new Thickness(1), EditorTheme.Corners),
                (strip.Tab.Background, strip.Tab.BorderBrush, strip.Tab.BorderThickness, strip.Tab.CornerRadius));
            Assert.Equal("Field", strip.Label.Text);
            Assert.Equal("Entity3D", strip.Icon.GetVisualDescendants().OfType<EditorIcon>().Single().IconName);
            Assert.Equal(EditorColors.Accent, IconColor(strip.Icon));
            Assert.Equal(EditorColors.Text, ((ISolidColorBrush)strip.Label.Foreground!).Color);
        });
    }

    [Fact]
    public void The_icon_sits_10_inside_the_tab_and_the_name_7_after_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var strip = window.SceneTabs;
            var icon = BoxOf(strip.Icon.GetVisualDescendants().OfType<EditorIcon>().Single(), window);

            AssertBox(BoxOf(strip.Tab, window).X + 11, 58, 16, 16, icon);
            Assert.Equal(icon.Right + 7, BoxOf(strip.Label, window).X);
        });
    }

    [Fact]
    public void The_dim_close_button_fills_the_end_of_the_tab_and_the_plus_starts_4_after_it()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var strip = window.SceneTabs;
            var tab = BoxOf(strip.Tab, window);
            var close = BoxOf(strip.CloseButton, window);

            Assert.Equal(new Size(24, 24), close.Size);
            Assert.Equal(tab.Right - 1, close.Right);
            // The name's width follows the font, so its end can fall on a part pixel.
            Assert.InRange(close.X - BoxOf(strip.Label, window).Right, 6.5, 7.5);
            Assert.Equal(EditorColors.TextDim, IconColor(strip.CloseButton));
            Assert.Equal(tab.Right + 4, BoxOf(strip.AddButton, window).X);
            Assert.Equal(new Size(26, 26), strip.AddButton.Bounds.Size);
        });
    }

    [Fact]
    public void Hide_panels_sits_at_the_right_end_6_pixels_from_the_edge()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal(3111 - 6, BoxOf(window.SceneTabs.PanelsButton, window).Right);
            Assert.Equal(26, window.SceneTabs.PanelsButton.Bounds.Height);
        });
    }

    [Fact]
    public void With_no_scene_open_there_is_no_tab()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize(new FakeScene { StartsEmpty = true });

            Assert.False(window.SceneTabs.Tab.IsVisible);
        });
    }

    [Fact]
    public void Opening_a_scene_renames_the_tab()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            scene.Open("res://levels/second.oscn");

            window.Load(scene);

            Assert.Equal("second", window.SceneTabs.Label.Text);
        });
    }

    [Fact]
    public void The_tools_float_in_two_pills_10_pixels_in_from_the_top_left_of_the_viewport()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var (modes, options) = (Pills(window)[0], Pills(window)[1]);
            var first = BoxOf(modes, window);
            var floating = EditorColors.Floating;

            Assert.Equal([303.0, 96.0, 34.0], [first.X, first.Y, first.Height]);
            Assert.Equal([first.Right + 10, 96.0, 34.0], [BoxOf(options, window).X, BoxOf(options, window).Y, BoxOf(options, window).Height]);
            Assert.All(Pills(window), pill => Assert.Equal(
                (EditorBrushes.Floating, EditorBrushes.LineStrong, EditorTheme.IslandCorners),
                (pill.Background, pill.BorderBrush, pill.CornerRadius)));
            // The island fill at 90 percent.
            Assert.Equal((0xE6, EditorColors.Panel), (floating.A, Color.FromRgb(floating.R, floating.G, floating.B)));
        });
    }

    [Fact]
    public void The_modes_pill_ends_with_more_and_the_options_pill_holds_snap_flags_and_measure_between_rules()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var toolbar = window.Toolbar;
            static string Kind(Control item) => item switch
            {
                FixedToggle tool => tool.GetLogicalDescendants().OfType<EditorIcon>().Single().IconName,
                Button => "options",
                _ => "|",
            };

            Assert.Equal(["ToolSelect", "ToolMove", "ToolRotate", "ToolScale", "GuiTabMenuHl"], Items(Pills(window)[0]).Select(Kind));
            Assert.Equal(["Object", "|", "Snap", "options", "|", "Lock", "Group", "|", "Ruler"], Items(Pills(window)[1]).Select(Kind));
            Assert.All(Tools(window), tool => Assert.Equal(26, tool.Bounds.Height));
            Assert.All(Tools(window), tool => Assert.Equal(100, BoxOf(tool, window).Y));
            Assert.All(new[] { toolbar.LocalButton, toolbar.TrackballButton, toolbar.KeepChildrenButton }, option => Assert.False(option.IsChecked));
        });
    }

    [Fact]
    public void Every_tool_is_its_icon_alone_with_its_name_and_key_on_its_card()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var tools = Tools(window).ToArray();

            Assert.All(tools, tool => Assert.Empty(Texts(tool)));
            Assert.Equal(["Pick", "Move", "Turn", "Resize", "More", "Local", "Snap", "Lock", "Group", "Measure"], tools.Select(AutomationProperties.GetName));
            // Headless runs take Control as the command key.
            Assert.Equal(["Q", "W", "E", "R", null, "T", "Y", "⌃L", "⌃G", null], tools.Select(tool => ((HoverCard)ToolTip.GetTip(tool)!).Key));
        });
    }

    [Fact]
    public void Every_tool_has_a_card_that_says_what_it_does()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var cards = Tools(window).Select(tool => Assert.IsType<HoverCard>(ToolTip.GetTip(tool))).ToArray();

            Assert.Equal(["Pick", "Move", "Turn", "Resize", "More", "Local", "Snap", "Lock", "Group", "Measure"], cards.Select(card => card.Title));
            Assert.Contains("Drag an arrow to move the picked thing. Hold ⌃ to snap.", Texts(cards[1]));
            Assert.Contains("Move mode", Texts(cards[1]));
            Assert.Contains("Click two points to see how far apart they are.", Texts(cards[9]));
        });
    }

    [Fact]
    public void Lock_and_group_say_why_they_are_off_until_a_thing_is_picked()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var card = (HoverCard)ToolTip.GetTip(window.Toolbar.LockButton)!;

            Assert.Contains("Pick a thing you can move first. Lock stops it being moved by accident.", Texts(card));
            Assert.Contains("Pick a thing you can move first. Group makes its parts move as one.", Texts((HoverCard)ToolTip.GetTip(window.Toolbar.GroupButton)!));
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.Contains("Stops the picked thing being moved by accident.", Texts(card));
        });
    }

    [Fact]
    public void All_handles_is_the_starting_mode_so_more_is_lit()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.Equal([false, false, false, false, true], Lit(window));
            Assert.True(window.Toolbar.TransformButton.IsChecked);
            Assert.Same(window.Toolbar.MoreButton, Tools(window)[4]);
        });
    }

    [Fact]
    public void A_click_on_a_mode_lights_it_alone_and_all_handles_in_more_lights_more()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var modes = Tools(window).Take(5).ToArray();

            ClickOn(modes[1], window);
            Assert.Equal([false, true, false, false, false], Lit(window));
            ClickOn(modes[0], window);
            Assert.Equal([true, false, false, false, false], Lit(window));
            Assert.Equal(GizmoMode.Select, window.Toolbar.Mode);
            MoreItems(window)[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal([false, false, false, false, true], Lit(window));
            Assert.Equal(GizmoMode.Transform, window.Toolbar.Mode);
        });
    }

    [Fact]
    public void More_names_its_four_tools_with_a_note_and_ticks_the_ones_that_are_on()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var items = MoreItems(window);

            Assert.Equal(["MORE TOOLS"], Texts(items[0]));
            Assert.Equal(["All handles", "Free turn", "Move alone", "Pick from a list"], items[1..].Select(item => Texts(item).First()));
            Assert.Contains("Moves a parent and leaves the things inside where they are", Texts(items[3]));
            Assert.Equal([true, false, false, false], items[1..].Select(item => item.IsChecked));
            items[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.True(window.Toolbar.Trackball);
            Assert.True(items[2].IsChecked);
            Click(window.Toolbar.TrackballButton);
            Assert.False(items[2].IsChecked);
        });
    }

    [Fact]
    public void A_click_on_local_coordinates_flips_the_space_and_says_nothing_is_missing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var local = window.Toolbar.LocalButton;
            ClickOn(window.Toolbar.MoveButton, window);
            Assert.Equal(GizmoSpace.Global, window.Toolbar.Space);

            ClickOn(local, window);
            Assert.True(local.IsChecked);
            Assert.Equal(GizmoSpace.Local, window.Toolbar.Space);
            ClickOn(local, window);
            Assert.False(local.IsChecked);
            Assert.Equal(GizmoSpace.Global, window.Toolbar.Space);
            Assert.DoesNotContain("not built", window.Status);
        });
    }

    [Fact]
    public void A_click_on_rotate_lights_it_alone_and_says_nothing_is_missing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var modes = Tools(window).Take(5).ToArray();
            ClickOn(modes[1], window);

            ClickOn(modes[2], window);

            Assert.Same(window.Toolbar.RotateButton, modes[2]);
            Assert.Equal([false, false, true, false, false], Lit(window));
            Assert.Equal(GizmoMode.Rotate, window.Toolbar.Mode);
            Assert.DoesNotContain("not built", window.Status);
        });
    }

    [Fact]
    public void A_click_on_scale_lights_it_alone_and_says_nothing_is_missing()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var modes = Tools(window).Take(5).ToArray();

            ClickOn(modes[3], window);

            Assert.Same(window.Toolbar.ScaleButton, modes[3]);
            Assert.Equal([false, false, false, true, false], Lit(window));
            Assert.Equal(GizmoMode.Scale, window.Toolbar.Mode);
            Assert.DoesNotContain("not built", window.Status);
        });
    }

    [Fact]
    public void The_lock_and_group_buttons_are_off_without_a_selection()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.False(window.Toolbar.LockButton.IsEnabled);
            Assert.False(window.Toolbar.GroupButton.IsEnabled);
        });
    }

    [Fact]
    public void A_selection_turns_lock_and_group_on_and_clearing_it_turns_them_off()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.True(window.Toolbar.LockButton.IsEnabled);
            Assert.True(window.Toolbar.GroupButton.IsEnabled);

            window.Outline.SelectedItem = null;
            Assert.False(window.Toolbar.LockButton.IsEnabled);
            Assert.False(window.Toolbar.GroupButton.IsEnabled);
        });
    }

    [Fact]
    public void A_new_toolbar_starts_with_lock_and_group_off()
    {
        UiThread.Run(() =>
        {
            var toolbar = new ViewportToolbar(new Button(), KeyModifiers.Meta);

            Assert.False(toolbar.LockButton.IsEnabled);
            Assert.False(toolbar.GroupButton.IsEnabled);
        });
    }

    [Fact]
    public void The_snap_options_button_sits_2_after_the_snap_button()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var snap = BoxOf(window.Toolbar.SnapButton, window);
            var options = BoxOf(window.Toolbar.SnapOptionsButton, window);

            Assert.Equal(snap.Right + 2, options.X);
            Assert.Equal(snap.Center.Y, options.Center.Y);
        });
    }

    [Fact]
    public void The_view_pill_holds_the_previews_after_a_rule_with_both_lit_and_ends_with_zoom_to_it()
    {
        UiThread.Run(() =>
        {
            var (window, _) = OpenWithViewport();
            var items = Items(window.Center.Overlay.View);
            var previews = items.OfType<FixedToggle>().ToArray();

            Assert.Same(window.Center.Overlay.ProjectionButton, items[0]);
            Assert.IsType<Border>(items[1]);
            Assert.Equal(["PreviewSun", "PreviewEnvironment"], previews.Select(preview => preview.GetLogicalDescendants().OfType<EditorIcon>().Single().IconName));
            Assert.Equal([true, true], previews.Select(preview => preview.IsChecked == true));
            Assert.Equal(["Sun", "Sky"], previews.Select(AutomationProperties.GetName));
            Assert.IsType<Border>(items[^2]);
            Assert.Same(window.Center.Overlay.ZoomToButton, items[^1]);
            Assert.Equal("Zoom to it", AutomationProperties.GetName(window.Center.Overlay.ZoomToButton));
            Assert.Equal("F", ((HoverCard)ToolTip.GetTip(window.Center.Overlay.ZoomToButton)!).Key);
        });
    }

    [Fact]
    public void Preview_settings_opens_a_popover_with_a_switch_for_each_preview()
    {
        UiThread.Run(() =>
        {
            var (window, _) = OpenWithViewport();
            var button = window.Center.Overlay.PreviewSettingsButton;

            ClickOn(button, window);

            Assert.True(button.Flyout!.IsOpen);
            var body = (Control)((Flyout)button.Flyout).Content!;
            var texts = body.GetLogicalDescendants().OfType<TextBlock>().Where(text => text.IsVisible).Select(text => text.Text ?? "").ToArray();
            Assert.Equal(["Preview sun and sky", "Used only while your scene has no light or sky of its own. The game never sees them."], texts[..2]);
            Assert.Equal(["Sun", "A soft light from above, so shapes are easy to see.", "Sky", "A plain sky behind everything."], texts[2..]);
            var switches = body.GetLogicalDescendants().OfType<ToggleSwitch>().ToArray();
            Assert.Equal([window.Center.Overlay.SunSwitch, window.Center.Overlay.EnvironmentSwitch], switches);
            Assert.All(switches, toggle => Assert.True(toggle.IsChecked));
        });
    }

    static (EditorWindow Window, OutlineNode Mesh) OpenWithMesh()
    {
        var scene = new FakeScene();
        var handle = scene.AddChild(FakeScene.Field.Handle, "MeshInstance3D", "Box");
        var window = OpenAtReferenceSize(scene);
        return (window, scene.Outline()!.Find(handle)!);
    }

    [Fact]
    public void The_chip_waits_for_a_selection_and_names_it_with_its_class()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var toolbar = window.Toolbar;
            Assert.False(toolbar.Chip.IsVisible);

            window.Outline.SelectedItem = FakeScene.Lamp;

            Assert.True(toolbar.Chip.IsVisible);
            Assert.Equal(["Lamp", FakeScene.Lamp.ClassName], [toolbar.NameText.Text!, toolbar.ClassText.Text!]);
            Assert.Equal(EditorIcons.ForClass(FakeScene.Lamp.ClassName), toolbar.ClassIcon.IconName);
            window.Outline.SelectedItem = null;
            Assert.False(toolbar.Chip.IsVisible);
        });
    }

    [Fact]
    public void The_chip_floats_36_high_10_pixels_in_from_the_bottom_left_of_the_viewport()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            window.Outline.SelectedItem = FakeScene.Lamp;
            window.UpdateLayout();
            var chip = BoxOf(window.Toolbar.Chip, window);

            Assert.Equal([303.0, 1218.0, 36.0], [chip.X, chip.Bottom, chip.Height]);
            Assert.Equal(EditorBrushes.Floating, window.Toolbar.Chip.Background);
        });
    }

    [Fact]
    public void The_collision_tab_shows_for_a_mesh_alone()
    {
        UiThread.Run(() =>
        {
            var (window, mesh) = OpenWithMesh();
            var toolbar = window.Toolbar;
            bool[] Shown() => [toolbar.CollisionButton.IsVisible, toolbar.CollisionRule.IsVisible];

            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.Equal([false, false], Shown());
            window.Outline.SelectedItem = mesh;
            Assert.Equal([true, true], Shown());
            Assert.Equal("Box", toolbar.NameText.Text);
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.Equal([false, false], Shown());
        });
    }

    [Fact]
    public void The_projection_menu_offers_perspective_and_flat_then_zoom_to_it_under_a_rule()
    {
        UiThread.Run(() =>
        {
            var overlay = OpenAtReferenceSize().Center.Overlay;
            var items = ((MenuFlyout)overlay.ProjectionButton.Flyout!).Items;

            Assert.Equal([overlay.PerspectiveItem, overlay.OrthogonalItem], items.Take(2));
            Assert.Equal(["Perspective", "Flat"], items.Take(2).Select(item => Texts((MenuItem)item!).First()));
            Assert.Contains("Orthogonal", Texts(overlay.OrthogonalItem));
            Assert.IsType<Separator>(items[2]);
            Assert.Same(overlay.FocusItem, items[3]);
            Assert.Equal("Zoom to it", Texts(overlay.FocusItem).First());
        });
    }

    [Fact]
    public void Focus_selection_is_off_without_a_framable_selection()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var focus = window.Center.Overlay.FocusItem;

            Assert.False(focus.IsEnabled);
            window.Outline.SelectedItem = FakeScene.Lamp;
            Assert.True(focus.IsEnabled);
            window.Outline.SelectedItem = FakeScene.Sound;
            Assert.False(focus.IsEnabled);
        });
    }

    [Fact]
    public void Focus_selection_frames_the_selected_entity()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            window.Outline.SelectedItem = FakeScene.Lamp;

            window.Center.Overlay.FocusItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            window.Toolbar.FocusButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal([FakeScene.Lamp.Handle, FakeScene.Lamp.Handle], scene.Framed);
            Assert.Equal("Framed Lamp.", window.Status);
        });
    }

    [Fact]
    public void Save_and_frame_have_no_place_on_the_toolbar()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Assert.DoesNotContain(window.SaveButton, window.GetVisualDescendants());
            Assert.DoesNotContain(window.FrameButton, window.GetVisualDescendants());
        });
    }

    [Fact]
    public void The_overlay_waits_for_a_viewport()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            Assert.False(window.Center.Overlay.IsVisible);

            window.ShowViewport(new ViewportControl(() => false, NoFrame));

            Assert.True(window.Center.Overlay.IsVisible);
        });
    }

    [Fact]
    public void The_view_pill_floats_10_pixels_in_from_the_top_right_of_the_viewport()
    {
        UiThread.Run(() =>
        {
            var (window, _) = OpenWithViewport();
            var overlay = window.Center.Overlay;
            var pill = BoxOf(overlay.View, window);
            var chevron = overlay.ProjectionButton.GetVisualDescendants().OfType<EditorIcon>().Single();

            Assert.Equal([3101.0, 96.0, 34.0], [pill.Right, pill.Y, pill.Height]);
            Assert.Equal(EditorBrushes.Floating, overlay.View.Background);
            Assert.Equal(26, overlay.ProjectionButton.Bounds.Height);
            Assert.Equal("Perspective", overlay.ProjectionLabel);
            Assert.Equal("GuiDropdown", chevron.IconName);
            Assert.Equal(EditorColors.TextDim, IconColor(overlay.ProjectionButton));
        });
    }

    [Fact]
    public void The_axes_are_100_pixels_square_under_the_view_pill_at_the_right_edge()
    {
        UiThread.Run(() =>
        {
            var (window, _) = OpenWithViewport();

            AssertBox(3011, 140, 100, 100, BoxOf(window.Center.Overlay.Axes, window));
        });
    }

    [Theory]
    [InlineData(3061, 190)]
    [InlineData(1500, 113)]
    public void A_drag_that_starts_on_the_axes_or_between_the_pills_reaches_the_viewport(double x, double y)
    {
        UiThread.Run(() =>
        {
            var (window, viewport) = OpenWithViewport();
            var orbits = 0;
            viewport.Orbited += (_, _) => orbits++;

            window.MouseDown(new Point(x, y), MouseButton.Left);
            window.MouseMove(new Point(x + 30, y + 30));

            Assert.Equal(1, orbits);
        });
    }

    [Fact]
    public void A_drag_in_the_viewport_turns_the_axes_like_the_camera()
    {
        UiThread.Run(() =>
        {
            var (window, viewport) = OpenWithViewport();
            var from = viewport.TranslatePoint(new Point(500, 500), window)!.Value;

            window.MouseDown(from, MouseButton.Left);
            window.MouseMove(from + new Point(50, 10));

            Assert.Equal(OrbitView.Start.Orbited(-0.5f, 0.1f), window.Center.Overlay.Axes.View);
        });
    }

    [Fact]
    public void Looking_straight_ahead_puts_x_right_y_up_and_z_at_the_viewer()
    {
        var ends = AxisGizmo.Ends(OrbitView.Start with { Yaw = 0, Pitch = 0 });

        Near(new Point(79, 50), ends[0].At);
        Near(new Point(50, 21), ends[1].At);
        Near(new Point(50, 50), ends[2].At);
        Assert.Equal(["X", "Y", "Z", "-X", "-Y", "-Z"], ends.Select(end => end.Name));
        Assert.Equal(1f, ends[2].Depth, 1e-4f);
        Assert.Equal(-1f, ends[5].Depth, 1e-4f);
        Near(new Point(21, 50), ends[3].At);
        Near(new Point(50, 79), ends[4].At);
    }

    [Fact]
    public void A_quarter_turn_swings_z_to_the_left_and_x_to_the_viewer()
    {
        var ends = AxisGizmo.Ends(OrbitView.Start with { Yaw = MathF.PI / 2, Pitch = 0 });

        Near(new Point(21, 50), ends[2].At);
        Assert.Equal(1f, ends[0].Depth, 1e-4f);
    }

    [Fact]
    public void Looking_down_puts_z_below_the_middle_and_shortens_y()
    {
        var pitch = MathF.PI / 6;
        var ends = AxisGizmo.Ends(OrbitView.Start with { Yaw = 0, Pitch = pitch });

        Near(new Point(50, 50 + AxisGizmo.Reach * MathF.Sin(pitch)), ends[2].At);
        Near(new Point(50, 50 - AxisGizmo.Reach * MathF.Cos(pitch)), ends[1].At);
        Assert.Equal(MathF.Cos(pitch), ends[2].Depth, 1e-4f);
        Assert.Equal(MathF.Sin(pitch), ends[1].Depth, 1e-4f);
    }

    [Theory]
    [InlineData(0.6f, 0.4f)]
    [InlineData(-2f, 0.9f)]
    [InlineData(3f, -0.7f)]
    public void The_ends_agree_with_a_camera_that_looks_at_the_target(float yaw, float pitch)
    {
        var view = OrbitView.Start with { Yaw = yaw, Pitch = pitch };
        var back = Vector3.Normalize(view.Eye - view.Target);
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, back));
        var up = Vector3.Cross(back, right);
        Vector3[] directions = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitX, -Vector3.UnitY, -Vector3.UnitZ];

        foreach (var (end, direction) in AxisGizmo.Ends(view).Zip(directions))
        {
            Near(new Point(50 + Vector3.Dot(direction, right) * AxisGizmo.Reach, 50 - Vector3.Dot(direction, up) * AxisGizmo.Reach), end.At);
            Assert.Equal(Vector3.Dot(direction, back), end.Depth, 1e-4f);
        }
    }

    [Fact]
    public void A_scene_whose_class_has_no_icon_gets_the_plain_entity_icon()
    {
        UiThread.Run(() =>
        {
            var strip = new SceneTabStrip(new Button(), KeyModifiers.Meta);

            strip.Show(new OutlineNode(1, "Odd", "NoSuchClass", []), "odd", "");

            Assert.Equal("Entity", Assert.IsType<EditorIcon>(strip.Icon.Content).IconName);
        });
    }

    static void Near(Point expected, Point actual)
    {
        Assert.Equal(expected.X, actual.X, 1e-3);
        Assert.Equal(expected.Y, actual.Y, 1e-3);
    }
}

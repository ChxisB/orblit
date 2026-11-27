using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.PlanarViewportTests;
using static Orblit.Editor.App.Tests.ReferenceWindow;
using Vector2 = System.Numerics.Vector2;

namespace Orblit.Editor.App.Tests;

public sealed class ViewportToolsTests
{
    static void ClickFrame(EditorWindow window, ViewportControl viewport, Vector2 framePoint, RawInputModifiers keys = RawInputModifiers.None)
    {
        var at = InWindow(viewport, window, framePoint);
        window.MouseDown(at, MouseButton.Left, keys);
        window.MouseUp(at, MouseButton.Left, keys);
    }

    [Fact]
    public void The_sun_button_and_its_switch_turn_the_sun_preview_off_and_on_together()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _) = OpenIn3D();
            var overlay = window.Center.Overlay;

            ClickOn(overlay.SunButton, window);

            Assert.False(overlay.SunButton.IsChecked);
            Assert.False(overlay.SunSwitch.IsChecked);
            Assert.Equal((false, true), scene.Previews.Single());

            overlay.SunSwitch.IsChecked = true;

            Assert.True(overlay.SunButton.IsChecked);
            Assert.Equal([(false, true), (true, true)], scene.Previews);
        });
    }

    [Fact]
    public void The_environment_switch_puts_out_its_button()
    {
        UiThread.Run(() =>
        {
            var (window, scene, _) = OpenIn3D();
            var overlay = window.Center.Overlay;

            overlay.EnvironmentSwitch.IsChecked = false;

            Assert.False(overlay.EnvironmentButton.IsChecked);
            Assert.Equal((true, false), scene.Previews.Single());
        });
    }

    [Fact]
    public void Select_from_a_list_and_the_ruler_light_one_at_a_time()
    {
        UiThread.Run(() =>
        {
            var (window, _, _) = OpenIn3D();
            var toolbar = window.Toolbar;

            Click(toolbar.ListSelectButton);
            Assert.True(toolbar.ListSelects);

            ClickOn(toolbar.RulerButton, window);
            Assert.True(toolbar.Measures);
            Assert.False(toolbar.ListSelects);

            ClickOn(toolbar.RulerButton, window);
            Assert.False(toolbar.Measures);
        });
    }

    [Fact]
    public void The_ruler_measures_between_two_clicks_and_selects_nothing()
    {
        UiThread.Run(() =>
        {
            var (window, _, viewport) = OpenIn3D();
            ClickOn(window.Toolbar.RulerButton, window);

            ClickFrame(window, viewport, new Vector2(10, 5));

            Assert.Equal("Click a second point. Esc stops measuring.", window.Status);
            Assert.Null(viewport.RulerSource!()!.To);

            // The fake scene's ground point is the frame point, so the clicks lie 30 by 40 apart.
            ClickFrame(window, viewport, new Vector2(40, 45));

            var shown = viewport.RulerSource!()!;
            Assert.NotNull(shown.To);
            Assert.Equal($"Measured {shown.Label}.", window.Status);
            Assert.Equal(50, float.Parse(shown.Label.Split(' ')[0], CultureInfo.InvariantCulture), 1f);
            Assert.Null(window.Outline.SelectedItem);
        });
    }

    [Fact]
    public void Putting_the_ruler_out_clears_its_points()
    {
        UiThread.Run(() =>
        {
            var (window, _, viewport) = OpenIn3D();
            ClickOn(window.Toolbar.RulerButton, window);
            ClickFrame(window, viewport, new Vector2(10, 5));

            ClickOn(window.Toolbar.RulerButton, window);
            Assert.Null(viewport.RulerSource!());

            ClickOn(window.Toolbar.RulerButton, window);
            Assert.Null(viewport.RulerSource!());
        });
    }

    [Fact]
    public void An_alt_click_over_two_entities_lists_both_and_a_choice_selects_it()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn3D();
            scene.PickAllResult.AddRange([FakeScene.Lamp.Handle, FakeScene.Sound.Handle]);

            ClickFrame(window, viewport, new Vector2(40, 20), RawInputModifiers.Alt);

            var list = window.PickList!;
            Assert.True(list.IsOpen);
            var items = list.Items.OfType<MenuItem>().Skip(1).ToArray();
            Assert.Equal(
                [FakeScene.Lamp.Name, FakeScene.Sound.Name],
                items.Select(item => ((DockPanel)item.Header!).Children.OfType<TextBlock>().Last().Text));

            items[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Same(FakeScene.Sound, window.Outline.SelectedItem);
        });
    }

    [Fact]
    public void Select_from_a_list_over_one_entity_selects_it_with_no_list()
    {
        UiThread.Run(() =>
        {
            var (window, scene, viewport) = OpenIn3D();
            scene.PickAllResult.Add(FakeScene.Lamp.Handle);
            Click(window.Toolbar.ListSelectButton);

            ClickFrame(window, viewport, new Vector2(40, 20));

            Assert.Null(window.PickList);
            Assert.Same(FakeScene.Lamp, window.Outline.SelectedItem);
        });
    }

    [Fact]
    public void A_third_ruler_point_starts_a_new_measure()
    {
        var ruler = new RulerPoints();
        ruler.Add(Vector3.Zero);
        ruler.Add(new Vector3(3, 0, 4));
        Assert.Equal(5, ruler.Length);

        ruler.Add(Vector3.One);

        Assert.Null(ruler.Length);
        Assert.Equal(new Vector2(1, 1), ruler.Shown(world => new Vector2(world.X, world.Z))!.From);
    }

    [Fact]
    public void The_ruler_shows_nothing_when_its_first_point_is_behind_the_camera()
    {
        var ruler = new RulerPoints();
        ruler.Add(Vector3.Zero);
        ruler.Add(Vector3.One);

        Assert.Null(ruler.Shown(_ => null));
        Assert.Equal("1.73 m", ruler.Shown(world => new Vector2(world.X, world.Y))!.Label);
    }
}

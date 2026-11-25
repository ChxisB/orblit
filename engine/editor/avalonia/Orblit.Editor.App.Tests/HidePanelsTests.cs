using Avalonia.Controls;
using Avalonia.Input;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class HidePanelsTests
{
    static BottomBar BarOf(EditorWindow window) => Assert.IsType<BottomBar>(window.Center.BottomBar.Child);

    static HoverCard CardOf(Control control) => Assert.IsType<HoverCard>(ToolTip.GetTip(control));

    [Fact]
    public void The_button_says_hide_panels_with_its_key_and_a_card_that_says_what_happens()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            var button = window.SceneTabs.PanelsButton;

            Assert.Equal([SceneTabStrip.HidePanels, Parts.KeyText(new KeyGesture(Key.OemPipe, window.CommandKey))], Texts(button));
            Assert.Equal("The view fills the window. Panels wait one key away.", CardOf(button).Line);
        });
    }

    [Fact]
    public void A_press_folds_both_sides_and_the_button_then_says_show_panels()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            Click(window.SceneTabs.PanelsButton);

            Assert.True(window.LeftSide.IsFolded);
            Assert.True(window.RightSide.IsFolded);
            Assert.Equal(SceneTabStrip.ShowPanels, Texts(window.SceneTabs.PanelsButton).First());
            Assert.Equal("Brings the panels back as they were.", CardOf(window.SceneTabs.PanelsButton).Line);

            Click(window.SceneTabs.PanelsButton);

            Assert.Equal(SceneTabStrip.HidePanels, Texts(window.SceneTabs.PanelsButton).First());
        });
    }

    [Fact]
    public void An_open_bottom_panel_shuts_on_hide_and_comes_back_on_show()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();
            ClickOn(BarOf(window).OutputTab, window);
            Assert.True(window.Center.Output.IsVisible);

            Click(window.SceneTabs.PanelsButton);
            Assert.False(window.Center.Output.IsVisible);

            Click(window.SceneTabs.PanelsButton);
            Assert.True(window.Center.Output.IsVisible);
        });
    }

    [Fact]
    public void A_workspace_that_folds_both_sides_turns_the_button_to_show_panels()
    {
        UiThread.Run(() =>
        {
            var window = OpenAtReferenceSize();

            ClickOn(window.Bar.Tabs.Single(tab => tab.Label == TitleBar.Script), window);
            Assert.Equal(SceneTabStrip.ShowPanels, Texts(window.SceneTabs.PanelsButton).First());

            ClickOn(window.Bar.Tabs.Single(tab => tab.Label == TitleBar.Spatial), window);
            Assert.Equal(SceneTabStrip.HidePanels, Texts(window.SceneTabs.PanelsButton).First());
        });
    }
}

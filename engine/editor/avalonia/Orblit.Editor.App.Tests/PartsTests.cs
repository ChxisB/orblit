using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Orblit.Editor.App.Shell;

namespace Orblit.Editor.App.Tests;

public sealed class PartsTests
{
    static string[] Words(Control control) => [.. control.GetLogicalDescendants().OfType<TextBlock>().Select(text => text.Text ?? "")];

    [Fact]
    public void A_card_shows_its_key_name_line_and_engine_name()
    {
        UiThread.Run(() =>
        {
            var card = new HoverCard("Move", "Drag an arrow to move the picked thing.", "W", "Move mode");

            Assert.Equal(["W", "Move", "Drag an arrow to move the picked thing.", "Move mode"], Words(card));
        });
    }

    [Fact]
    public void A_card_is_its_control_s_tip_and_shows_while_the_control_is_off()
    {
        UiThread.Run(() =>
        {
            var button = new Button();
            var card = new HoverCard("Stop", "Closes the game window.");

            Assert.Same(button, card.On(button));
            Assert.Same(card, ToolTip.GetTip(button));
            Assert.True(ToolTip.GetShowOnDisabled(button));
        });
    }

    [Fact]
    public void An_off_control_s_card_says_why_and_says_what_it_does_once_it_is_on()
    {
        UiThread.Run(() =>
        {
            var button = new Button { IsEnabled = false };
            var card = new HoverCard("Stop", "Closes the game window.");

            card.SaysWhyOff(button, "Nothing is playing.");
            Assert.Equal("Nothing is playing.", card.Line);

            button.IsEnabled = true;
            Assert.Equal("Closes the game window.", card.Line);
        });
    }

    [Fact]
    public void A_noted_item_shows_its_name_over_its_note_and_carries_its_name()
    {
        UiThread.Run(() =>
        {
            var item = Parts.ActionItem("Play the game", () => { }, note: "Starts from the main scene");

            Assert.Equal(["Play the game", "Starts from the main scene"], Words((Control)item.Header!));
            Assert.Equal("Play the game", AutomationProperties.GetName(item));
        });
    }

    [Theory]
    [InlineData(Key.D1, "⌘1")]
    [InlineData(Key.D5, "⌘5")]
    [InlineData(Key.OemPeriod, "⌘.")]
    [InlineData(Key.OemComma, "⌘,")]
    [InlineData(Key.B, "⌘B")]
    public void A_key_reads_as_the_menus_write_it(Key key, string text) =>
        Assert.Equal(text, Parts.KeyText(new KeyGesture(key, KeyModifiers.Meta)));
}

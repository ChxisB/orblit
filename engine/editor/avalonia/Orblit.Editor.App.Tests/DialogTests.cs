using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class DialogTests
{
    static Dialog NewDialog() => new("New Scene", "Pick what the scene is for.", new TextBlock { Text = "Body" }) { MainText = "Create", CardWidth = 420 };

    static Dialog Opened(Dialog dialog)
    {
        var window = new Window { Width = 1000, Height = 700, Content = dialog };
        window.Show();
        dialog.Open();
        window.UpdateLayout();
        return dialog;
    }

    static bool Press(Control target, Key key)
    {
        var press = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };
        target.RaiseEvent(press);
        return press.Handled;
    }

    [Fact]
    public void A_dialog_waits_hidden_until_it_is_opened()
    {
        UiThread.Run(() => Assert.False(NewDialog().IsVisible));
    }

    [Fact]
    public void Escape_closes_the_dialog_and_says_it_was_cancelled()
    {
        UiThread.Run(() =>
        {
            var dialog = Opened(NewDialog());
            var cancels = 0;
            dialog.Cancelled += () => cancels++;

            Assert.True(Press(dialog.MainButton, Key.Escape));

            Assert.False(dialog.IsVisible);
            Assert.Equal(1, cancels);
        });
    }

    [Fact]
    public void Enter_in_the_body_runs_the_main_action()
    {
        UiThread.Run(() =>
        {
            var name = new TextBox();
            var dialog = Opened(new Dialog("New Scene", "Pick what the scene is for.", name) { MainText = "Create" });
            var runs = 0;
            dialog.MainButton.Click += (_, _) => runs++;

            Assert.True(Press(name, Key.Enter));

            Assert.Equal(1, runs);
        });
    }

    [Fact]
    public void Enter_does_nothing_while_the_main_action_is_off()
    {
        UiThread.Run(() =>
        {
            var dialog = Opened(NewDialog());
            dialog.MainButton.IsEnabled = false;
            var runs = 0;
            dialog.MainButton.Click += (_, _) => runs++;

            Press(dialog, Key.Enter);

            Assert.Equal(0, runs);
            Assert.True(dialog.IsVisible);
        });
    }

    [Fact]
    public void The_close_button_and_Cancel_both_cancel()
    {
        UiThread.Run(() =>
        {
            var dialog = Opened(NewDialog());
            var cancels = 0;
            dialog.Cancelled += () => cancels++;

            Click(dialog.CloseButton);
            Assert.False(dialog.IsVisible);
            dialog.Open();
            Click(dialog.CancelButton);

            Assert.False(dialog.IsVisible);
            Assert.Equal(2, cancels);
        });
    }

    [Fact]
    public void The_card_is_a_popover_in_the_middle_of_a_veil_that_takes_the_clicks()
    {
        UiThread.Run(() =>
        {
            var dialog = Opened(NewDialog());
            var card = dialog.Card;

            Assert.Equal(420, card.Bounds.Width);
            Assert.Equal((1000 - 420) / 2.0, BoxOf(card, dialog).X);
            Assert.Same(EditorBrushes.Popover, card.Background);
            Assert.Same(EditorBrushes.LineStrong, card.BorderBrush);
            Assert.Equal(EditorTheme.PopoverCorners, card.CornerRadius);
            // A click past the card must not reach the editor behind it.
            Assert.NotNull(dialog.Background);
        });
    }

    [Fact]
    public void The_title_is_bold_with_a_dim_line_on_why_under_it()
    {
        UiThread.Run(() =>
        {
            var dialog = Opened(NewDialog());

            Assert.Equal("New Scene", dialog.Title);
            Assert.Equal(FontWeight.SemiBold, dialog.Heading.FontWeight);
            Assert.Equal("Pick what the scene is for.", dialog.Why.Text);
            Assert.Equal(12, dialog.Why.FontSize);
            Assert.Same(EditorBrushes.TextDim, dialog.Why.Foreground);
            Assert.True(BoxOf(dialog.Why, dialog).Y > BoxOf(dialog.Heading, dialog).Bottom);
        });
    }

    [Fact]
    public void The_main_action_is_orange_at_the_right_with_a_quiet_Cancel_before_it()
    {
        UiThread.Run(() =>
        {
            var dialog = Opened(NewDialog());
            var main = dialog.MainButton;
            var cancel = dialog.CancelButton;

            Assert.Equal("Create", main.Content);
            Assert.Equal("Cancel", cancel.Content);
            Assert.Equal(EditorColors.Accent, ((ISolidColorBrush)main.Background!).Color);
            Assert.Equal(30, main.Bounds.Height);
            Assert.Same(EditorBrushes.TextSoft, cancel.Foreground);
            Assert.Equal(BoxOf(dialog.Card, dialog).Right - 1 - 12, BoxOf(main, dialog).Right);
            Assert.Equal(BoxOf(cancel, dialog).Right + 8, BoxOf(main, dialog).X);
        });
    }

    [Fact]
    public void A_lead_action_sits_at_the_left_of_the_footer_far_from_the_main_one()
    {
        UiThread.Run(() =>
        {
            var drop = new Button { Content = "Close without saving", Classes = { EditorTheme.TextButton, EditorTheme.Danger } };
            var dialog = Opened(new Dialog("Save changes?", "Closing without saving drops them.", new TextBlock()) { LeadButton = drop, Closable = false });

            Assert.Equal(BoxOf(dialog.Card, dialog).X + 1 + 12, BoxOf(drop, dialog).X);
            Assert.Same(EditorBrushes.FaultText, drop.Foreground);
            Assert.False(dialog.CloseButton.IsVisible);
        });
    }

    [Fact]
    public void Opening_puts_the_keys_on_the_main_action()
    {
        UiThread.Run(() =>
        {
            var dialog = Opened(NewDialog());

            Assert.True(dialog.MainButton.IsFocused);
        });
    }
}

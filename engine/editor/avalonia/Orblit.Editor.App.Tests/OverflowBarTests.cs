using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Orblit.Editor.App.Shell;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class OverflowBarTests
{
    // Room for two tools of 50 and the More button, not for three.
    const double Narrow = 150;

    sealed class Named : Border, IOverflowTool
    {
        public Named(string words)
        {
            MenuWords = words;
            Width = 50;
            Child = MenuButton;
        }

        public string MenuWords { get; }

        public Button MenuButton { get; } = new();
    }

    static T Tool<T>(string name) where T : Button, new()
    {
        var button = new T { Width = 50 };
        new HoverCard(name, "").On(button);
        return button;
    }

    static OverflowBar Show(double width, OverflowBar bar)
    {
        var window = new Window { Width = width, Height = 100, Content = bar };
        window.Show();
        window.UpdateLayout();
        return bar;
    }

    static Control[] Moved(OverflowBar bar) => [.. bar.Tools.Where(tool => tool.Classes.Contains(OverflowBar.Moved))];

    static MenuItem[] Menu(OverflowBar bar)
    {
        var menu = (MenuFlyout)bar.MoreButton.Flyout!;
        menu.ShowAt(bar.MoreButton);
        return [.. menu.Items.Cast<MenuItem>()];
    }

    static void Choose(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    [Fact]
    public void A_bar_with_room_keeps_every_tool_and_hides_the_more_button()
    {
        UiThread.Run(() =>
        {
            var bar = Show(600, new OverflowBar(Tool<Button>("Copy"), Tool<Button>("Clear")));

            Assert.Empty(Moved(bar));
            Assert.Contains(OverflowBar.Moved, bar.MoreButton.Classes);
        });
    }

    [Fact]
    public void A_bar_short_of_room_moves_its_last_tools_out_of_view_and_out_of_the_tab_order()
    {
        UiThread.Run(() =>
        {
            Button[] tools = [Tool<Button>("One"), Tool<Button>("Two"), Tool<Button>("Three"), Tool<Button>("Four")];

            var bar = Show(Narrow, new OverflowBar(tools));

            Assert.Equal(tools[2..], Moved(bar));
            Assert.Equal([true, true, false, false], tools.Select(tool => tool.Focusable));
            Assert.True(bar.MoreButton.Bounds.Right <= Narrow);
            Assert.All(tools[2..], tool => Assert.True(tool.Bounds.X >= Narrow));
        });
    }

    [Fact]
    public void A_menu_item_carries_its_tool_s_name_and_presses_or_ticks_it()
    {
        UiThread.Run(() =>
        {
            var (presses, kept) = (0, Tool<Button>("Kept"));
            var plain = Tool<Button>("Copy");
            plain.Click += (_, _) => presses++;
            var tick = Tool<ToggleButton>("Errors");
            var named = new Named("boss.ograph");
            named.MenuButton.Click += (_, _) => presses += 10;
            var off = Tool<Button>("Clear");
            off.IsEnabled = false;
            var bar = Show(90, new OverflowBar(kept, plain, tick, new Border { Width = 1 }, named, off));

            var items = Menu(bar);

            Assert.Equal(["Copy", "Errors", "boss.ograph", "Clear"], items.Select(item => item.Header));
            Assert.False(items[3].IsEnabled);
            Choose(items[0]);
            Choose(items[1]);
            Choose(items[2]);
            Assert.Equal(11, presses);
            Assert.True(tick.IsChecked);
            Assert.False(named.MenuButton.Focusable);
        });
    }

    [Fact]
    public void The_pinned_tool_stays_in_view_in_place_of_the_last_that_fits()
    {
        UiThread.Run(() =>
        {
            Button[] tools = [Tool<Button>("One"), Tool<Button>("Two"), Tool<Button>("Three"), Tool<Button>("Four")];
            var bar = Show(Narrow, new OverflowBar(tools));

            bar.Pinned = tools[3];
            bar.UpdateLayout();

            Assert.Equal(tools[1..3], Moved(bar));
        });
    }

    [Fact]
    public void A_first_tool_with_a_least_width_shrinks_before_any_tool_moves()
    {
        UiThread.Run(() =>
        {
            var filter = new TextBox { MinWidth = 60, MaxWidth = 120 };

            var bar = Show(Narrow, new OverflowBar(filter, Tool<Button>("Copy")));

            Assert.Empty(Moved(bar));
            Assert.Equal(Narrow - 4 - 50, filter.Bounds.Width);
        });
    }
}

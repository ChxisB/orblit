using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.App.Shell;

namespace Orblit.Editor.App.Tests;

public sealed class NamePairTests
{
    // A name that wants this width and takes less when that is all it is offered, as trimmed words do.
    sealed class Name(double wanted) : Control
    {
        protected override Size MeasureOverride(Size availableSize) => new(Math.Min(wanted, availableSize.Width), 10);
    }

    [Theory]
    [InlineData(300, 300, 95, 95)]
    [InlineData(40, 300, 40, 150)]
    [InlineData(300, 40, 150, 40)]
    [InlineData(40, 50, 40, 50)]
    public void Two_names_that_do_not_fit_share_the_room_and_a_short_one_leaves_the_rest(
        double first, double second, double firstGets, double secondGets)
    {
        UiThread.Run(() =>
        {
            var names = new[] { new Name(first), new Name(second) };
            var pair = new NamePair(names[0], new Name(10), names[1]);
            var window = new Window { Width = 200, Height = 100, Content = pair };
            window.Show();
            window.UpdateLayout();

            Assert.Equal([firstGets, secondGets], names.Select(name => name.Bounds.Width));
        });
    }
}

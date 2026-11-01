using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Ui.Avalonia.Tests;

public class FlexPanelTests
{
    private static Border Box(double width, double height = 20) => new() { Width = width, Height = height };

    // A size of its own beats the slot a flex hands out, so a child that grows or fills has none.
    private static Border Elastic(double minWidth = 0) => new() { MinWidth = minWidth, Height = 20 };

    private static FlexPanel Row(params Control[] children) => Lay(new FlexPanel { Orientation = Orientation.Horizontal }, children);

    private static FlexPanel Column(params Control[] children) => Lay(new FlexPanel { Orientation = Orientation.Vertical }, children);

    // Rounding would put a whole pixel under every position, and these tests are about fractions.
    private static FlexPanel Lay(FlexPanel panel, Control[] children)
    {
        panel.UseLayoutRounding = false;
        panel.Children.AddRange(children);
        return panel;
    }

    private static void Fit(Layoutable panel, double width, double height)
    {
        panel.Measure(new Size(width, height));
        panel.Arrange(new Rect(0, 0, width, height));
    }

    private static double[] Lefts(Panel panel) => [.. panel.Children.Select(child => child.Bounds.Left)];

    [Fact]
    public Task StartPacksChildrenAtTheStart() => Headless.Run(() =>
    {
        var row = Row(Box(50), Box(30));
        Fit(row, 300, 100);
        Assert.Equal([0, 50], Lefts(row));
    });

    [Fact]
    public Task CenterAndEndShareOutWhatIsLeft() => Headless.Run(() =>
    {
        var centred = Row(Box(50), Box(30));
        centred.MainAxis = FlexMain.Center;
        Fit(centred, 300, 100);
        Assert.Equal([110, 160], Lefts(centred));

        var ended = Row(Box(50), Box(30));
        ended.MainAxis = FlexMain.End;
        Fit(ended, 300, 100);
        Assert.Equal([220, 270], Lefts(ended));
    });

    [Fact]
    public Task BetweenAroundAndEvenlyDivideTheFreeSpace() => Headless.Run(() =>
    {
        (FlexMain How, double[] Expected)[] cases =
        [
            (FlexMain.Between, [0, 150, 280]),
            (FlexMain.Around, [100.0 / 3, 150, 740.0 / 3]),
            (FlexMain.Evenly, [50, 150, 230]),
        ];
        foreach (var (how, expected) in cases)
        {
            var row = Row(Box(50), Box(30), Box(20));
            row.MainAxis = how;
            Fit(row, 300, 100);
            Assert.Equal(expected, Lefts(row).Select(left => Math.Round(left, 6)), new RoundedComparer());
        }
    });

    [Fact]
    public Task AGapIsASlotTheFreeSpaceIsDividedAcrossToo() => Headless.Run(() =>
    {
        // A gap here sits where a spacer box sat in the original, so around and evenly count it.
        var row = Row(Box(50), Box(50));
        row.Gap = 10;
        row.MainAxis = FlexMain.Evenly;
        Fit(row, 300, 100);
        Assert.Equal([47.5, 202.5], Lefts(row));

        row.MainAxis = FlexMain.Between;
        Fit(row, 300, 100);
        Assert.Equal([0, 250], Lefts(row));
    });

    [Fact]
    public Task TooManyChildrenOverflowFromTheStart() => Headless.Run(() =>
    {
        var row = Row(Box(200), Box(200));
        row.MainAxis = FlexMain.Center;
        Fit(row, 300, 100);
        Assert.Equal([0, 200], Lefts(row));
    });

    [Fact]
    public Task GrowingChildrenShareWhatTheOthersLeave() => Headless.Run(() =>
    {
        var (fixedOne, small, large) = (Box(50), Elastic(), Elastic());
        FlexPanel.SetGrow(small, 1);
        FlexPanel.SetGrow(large, 3);
        var row = Row(fixedOne, small, large);
        Fit(row, 250, 100);

        Assert.Equal([0, 50, 100], Lefts(row));
        Assert.Equal(50, small.Bounds.Width);
        Assert.Equal(150, large.Bounds.Width);
    });

    [Fact]
    public Task WithNothingToShareAGrowingChildKeepsItsOwnSize() => Headless.Run(() =>
    {
        // An unbounded main axis is the case that took a whole frame down before.
        var (fixedOne, growing) = (Box(50), Box(30));
        FlexPanel.SetGrow(growing, 1);
        var row = Row(fixedOne, growing);

        row.Measure(Size.Infinity);
        Assert.Equal(80, row.DesiredSize.Width);
        row.Arrange(new Rect(0, 0, 80, 20));
        Assert.Equal(30, growing.Bounds.Width);
    });

    [Fact]
    public Task AFlexFillsTheMainAxisItIsOfferedAndShrinkWrapsWhenNotOffered() => Headless.Run(() =>
    {
        var row = Row(Box(50), Box(30));
        row.Measure(new Size(300, 100));
        Assert.Equal(300, row.DesiredSize.Width);

        row.Measure(Size.Infinity);
        Assert.Equal(80, row.DesiredSize.Width);
    });

    [Fact]
    public Task TheCrossAxisAlignsEachChild() => Headless.Run(() =>
    {
        (FlexCross How, double Top, double Height)[] cases =
        [
            (FlexCross.Start, 0, 20),
            (FlexCross.Center, 40, 20),
            (FlexCross.End, 80, 20),
            (FlexCross.Stretch, 0, 100),
        ];
        foreach (var (how, top, height) in cases)
        {
            var kid = new Border { Width = 50, MinHeight = 20 };
            var row = Row(kid);
            row.CrossAxis = how;
            Fit(row, 300, 100);
            Assert.Equal((top, height), (kid.Bounds.Top, kid.Bounds.Height));
        }
    });

    [Fact]
    public Task ACrossAxisThatIsNotBoundedCannotStretch() => Headless.Run(() =>
    {
        var kid = new Border { Width = 50, MinHeight = 20 };
        var row = Row(kid);
        row.CrossAxis = FlexCross.Stretch;
        row.Measure(new Size(300, double.PositiveInfinity));
        row.Arrange(new Rect(0, 0, 300, 20));
        Assert.Equal(20, kid.Bounds.Height);
    });

    [Fact]
    public Task AChildThatAsksToFillTheCrossAxisGetsAllOfIt() => Headless.Run(() =>
    {
        var kid = new Border { Width = 50 };
        FlexPanel.SetFillHeight(kid, true);
        var row = Row(kid);
        Fit(row, 300, 100);
        Assert.Equal(100, kid.Bounds.Height);

        var wide = Elastic();
        FlexPanel.SetFillWidth(wide, true);
        var column = Column(wide);
        Fit(column, 300, 100);
        Assert.Equal(300, wide.Bounds.Width);
    });

    [Fact]
    public Task AColumnIsARowTurnedTheOtherWay() => Headless.Run(() =>
    {
        var (top, rest) = (Box(50, 40), new Border { Width = 50 });
        FlexPanel.SetGrow(rest, 1);
        var column = Column(top, rest);
        column.Gap = 10;
        Fit(column, 100, 200);

        Assert.Equal(0, top.Bounds.Top);
        Assert.Equal(50, rest.Bounds.Top);
        Assert.Equal(150, rest.Bounds.Height);
    });

    [Fact]
    public Task ChangingAPropertyLaysTheChildrenOutAgain() => Headless.Run(() =>
    {
        var row = Row(Box(50), Elastic(30));
        var window = TestWindow.Show(row, 300, 100);
        Assert.Equal([0, 50], Lefts(row));

        row.Gap = 20;
        window.UpdateLayout();
        Assert.Equal([0, 70], Lefts(row));

        FlexPanel.SetGrow(row.Children[1], 1);
        window.UpdateLayout();
        Assert.Equal(230, row.Children[1].Bounds.Width);
    });

    private sealed class RoundedComparer : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-4;

        public int GetHashCode(double obj) => 0;
    }
}

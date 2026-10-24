namespace Orblit.Ui.Tests;

public class BreakpointTests
{
    private static readonly UiUtilities Utilities = new();
    private static readonly UiBreakpoints Breakpoints = new();

    [Fact]
    public void AWidthIsUnderEveryOneItHasNotReached()
    {
        Assert.Empty(Breakpoints.ActiveAt(320));
        Assert.Equal(["sm"], Breakpoints.ActiveAt(700));
        Assert.Equal(["sm", "md"], Breakpoints.ActiveAt(1000));
        Assert.Equal(["sm", "md", "lg", "xl"], Breakpoints.ActiveAt(4000));
    }

    [Fact]
    public void AWidthHasANameSomebodyCanBeShown()
    {
        Assert.Equal("base", Breakpoints.LabelAt(320));
        Assert.Equal("md", Breakpoints.LabelAt(1000));
        Assert.Equal("xl", Breakpoints.LabelAt(1920));
    }

    [Fact]
    public void APrefixedClassWaitsForTheWidthItNames()
    {
        Assert.Equal(14.0, Utilities.Parse("text-base md:text-xl", 400).FontSize);
        Assert.Equal(19.0, Utilities.Parse("text-base md:text-xl", 1000).FontSize);
    }

    [Fact]
    public void WithoutAWidthOnlyTheUnprefixedClassesApply() =>
        Assert.Equal(14.0, Utilities.Parse("text-base md:text-xl").FontSize);

    [Fact]
    public void TheWidestThatAppliesWinsWhateverOrderItWasTypedIn()
    {
        const string forwards = "md:text-xl lg:text-3xl";
        const string backwards = "lg:text-3xl md:text-xl";
        Assert.Equal(30.0, Utilities.Parse(forwards, 1400).FontSize);
        Assert.Equal(30.0, Utilities.Parse(backwards, 1400).FontSize);
        Assert.Equal(19.0, Utilities.Parse(backwards, 1000).FontSize);
    }

    [Fact]
    public void ALayoutCanChangeDirectionNotOnlySize()
    {
        Assert.Equal("column", Utilities.Parse("col md:row", 400).Direction);
        Assert.Equal("row", Utilities.Parse("col md:row", 1200).Direction);
    }

    [Fact]
    public void APrefixThatIsNotABreakpointIsNotAClass()
    {
        Assert.Null(Utilities.Parse("hover:text-xl", 4000).FontSize);
        Assert.Equal(["hover:text-xl"], Utilities.UnknownIn("hover:text-xl"));
        Assert.Empty(Utilities.UnknownIn("md:text-xl"));
        Assert.Equal(["md:nonsense"], Utilities.UnknownIn("md:nonsense"));
    }

    [Fact]
    public void AScaledThemeMovesEveryMeasurementTogether()
    {
        var theme = new UiTheme().Scaled(2);
        Assert.Equal(8.0, theme.Step);
        Assert.Equal(28.0, theme.Text["base"]);
        Assert.Equal(32.0, new UiUtilities(theme).Parse("p-4").PaddingTop);
        Assert.Equal(13.0, new UiUtilities(theme).Parse("p-[13]").PaddingTop);
    }

    [Fact]
    public void ScalingByOneIsTheSameTheme()
    {
        var theme = new UiTheme();
        Assert.Same(theme, theme.Scaled(1));
        Assert.Same(theme, theme.Scaled(0));
    }
}

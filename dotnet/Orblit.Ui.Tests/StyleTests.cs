namespace Orblit.Ui.Tests;

public class StyleTests
{
    private static readonly UiUtilities Utilities = new();
    private static readonly UiCss Css = new();

    [Fact]
    public void SpacingIsCountedInStepsNotPixels()
    {
        Assert.Equal(16.0, Utilities.Parse("p-4").PaddingTop);
        Assert.Equal(8.0, Utilities.Parse("px-2").PaddingLeft);
        Assert.Null(Utilities.Parse("px-2").PaddingTop);
        Assert.Equal(4.0, Utilities.Parse("mt-1").MarginTop);
    }

    [Fact]
    public void AMeasurementInBracketsIsInPixels()
    {
        Assert.Equal(13.0, Utilities.Parse("p-[13]").PaddingTop);
        Assert.Equal(220.0, Utilities.Parse("w-[220]").Width);
    }

    [Fact]
    public void LayoutReadsTheWayItIsWritten()
    {
        var style = Utilities.Parse("row items-center justify-between gap-3");
        Assert.Equal("row", style.Direction);
        Assert.Equal("center", style.CrossAxis);
        Assert.Equal("between", style.MainAxis);
        Assert.Equal(12.0, style.Gap);
    }

    [Fact]
    public void ColoursComeFromThePaletteOrAreWrittenOut()
    {
        Assert.NotNull(Utilities.Parse("bg-slate-800").Background);
        Assert.Equal(new UiColour(0xFFC25E22), Utilities.Parse("text-ember-500").Colour);
        Assert.Equal(new UiColour(0xFF123456), Utilities.Parse("bg-[#123456]").Background);
        Assert.Null(Utilities.Parse("bg-nonesuch-500").Background);
    }

    [Fact]
    public void TextSizesAndWeightsAreNamed()
    {
        Assert.Equal(16.0, Utilities.Parse("text-lg").FontSize);
        Assert.Equal(600L, Utilities.Parse("font-semibold").FontWeight);
        Assert.Equal("center", Utilities.Parse("text-center").TextAlign);
    }

    [Fact]
    public void LaterClassesWinOverEarlierOnes()
    {
        Assert.Equal(32.0, Utilities.Parse("p-2 p-8").PaddingTop);
        Assert.Equal(32.0, Utilities.Parse("p-2 pt-8").PaddingTop);
        Assert.Equal(8.0, Utilities.Parse("p-2 pt-8").PaddingBottom);
    }

    [Fact]
    public void AWordNobodyKnowsIsIgnoredRatherThanFatal()
    {
        var style = Utilities.Parse("p-4 rounded-lg wobbly bg-slate-800");
        Assert.Equal(16.0, style.PaddingTop);
        Assert.NotNull(style.Background);
        Assert.Equal(["wobbly", "nonsense-9"], Utilities.UnknownIn("p-4 wobbly nonsense-9"));
    }

    [Fact]
    public void OpacityIsAPercentage() => Assert.Equal(0.5, Utilities.Parse("opacity-50").Opacity);

    [Fact]
    public void TheShorthandsExpandTheWayCssExpandsThem()
    {
        var one = Css.Parse("padding: 8px");
        Assert.Equal([8.0, 8.0], [one.PaddingTop, one.PaddingRight]);

        var two = Css.Parse("padding: 8px 12px");
        Assert.Equal([8.0, 12.0, 12.0], [two.PaddingTop, two.PaddingRight, two.PaddingLeft]);

        var four = Css.Parse("padding: 1px 2px 3px 4px");
        Assert.Equal([1.0, 2.0, 3.0, 4.0], [four.PaddingTop, four.PaddingRight, four.PaddingBottom, four.PaddingLeft]);
    }

    [Fact]
    public void ColoursArriveInAnyOfTheNotations()
    {
        Assert.Equal(new UiColour(0xFFFFFFFF), Css.Parse("color: #fff").Colour);
        Assert.Equal(new UiColour(0xFF1A2B3C), Css.Parse("color: #1a2b3c").Colour);
        Assert.Equal(new UiColour(0x80112233), Css.Parse("color: #11223380").Colour);
        Assert.Equal(new UiColour(0xFFFF0000), Css.Parse("color: rgb(255, 0, 0)").Colour);
        Assert.Equal(new UiColour(0x80000000), Css.Parse("color: rgba(0, 0, 0, 0.5)").Colour);
        Assert.NotNull(Css.Parse("color: slate-500").Colour);
    }

    [Fact]
    public void FlexPropertiesMeanWhatTheyMeanInFlexbox()
    {
        var style = Css.Parse("display: flex; align-items: center; justify-content: space-between; gap: 12px; flex: 1");
        Assert.Equal("row", style.Direction);
        Assert.Equal("center", style.CrossAxis);
        Assert.Equal("between", style.MainAxis);
        Assert.Equal(12.0, style.Gap);
        Assert.Equal(1.0, style.Grow);
    }

    [Fact]
    public void ABorderIsReadOutOfTheShorthand()
    {
        var style = Css.Parse("border: 2px solid #334455");
        Assert.Equal(2.0, style.BorderWidth);
        Assert.Equal(new UiColour(0xFF334455), style.BorderColour);
    }

    [Fact]
    public void ASelectorAndBracesAroundItAreTolerated()
    {
        var style = Css.Parse(".card { padding: 6px; border-radius: 4px }");
        Assert.Equal(6.0, style.PaddingTop);
        Assert.Equal(4.0, style.Radius);
    }

    [Fact]
    public void WhatItDoesNotUnderstandItSays() =>
        Assert.Equal(["float"], Css.UnknownIn("float: left; padding: 4px"));
}

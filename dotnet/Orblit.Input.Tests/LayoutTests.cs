namespace Orblit.Input.Tests;

public class LayoutTests
{
    private static readonly PadIdentity Deck = new("Microsoft X-Box 360 pad 0", vendor: 0x28de, product: 0x1205);

    [Fact]
    public void AKnownPadIsFoundByVendorAndProduct() => Assert.Equal("Steam Deck", PadLayout.Of(Deck).Name);

    [Fact]
    public void ASonyPadGetsSonyLabels()
    {
        var dualSense = new PadIdentity("DualSense Wireless Controller", vendor: 0x054c, product: 0x0ce6);
        Assert.Equal("Cross", PadLayout.Of(dualSense).Labels.South);
        Assert.Equal("Circle", PadLayout.Of(dualSense).Labels.East);
    }

    [Fact]
    public void ANintendoPadSwapsTheLettersAndNotThePositions()
    {
        var pro = new PadIdentity("Nintendo Switch Pro Controller", vendor: 0x057e, product: 0x2009);
        var layout = PadLayout.Of(pro);
        Assert.Equal(PadButton.South, layout.Buttons[0x130]);
        Assert.Equal("B", layout.Labels.Of(PadButton.South));
        Assert.Equal("A", layout.Labels.Of(PadButton.East));
    }

    [Fact]
    public void AnUnknownPadFallsBackToTheName()
    {
        var clone = new PadIdentity("Generic X-Box pad", vendor: 0x1234, product: 0x5678);
        Assert.Equal("Xbox Controller", PadLayout.Of(clone).Name);
    }

    [Fact]
    public void APadInNoTableAtAllStillWorks()
    {
        var nobody = new PadIdentity("Handmade Thing", vendor: 1, product: 2);
        var layout = PadLayout.Of(nobody);
        Assert.Equal("Standard", layout.Name);
        Assert.Equal(PadButton.South, layout.Buttons[0x130]);
        Assert.Equal(PadButton.Start, layout.Buttons[0x13b]);
        Assert.Equal(PadAxis.LeftX, layout.Axes[0x00]);
        Assert.Equal(PadAxis.RightTrigger, layout.Axes[0x05]);
    }

    [Fact]
    public void APadWithNoRightStickAxesPutsTheStickWhereItReallyIs()
    {
        var older = new PadIdentity("Old Pad", vendor: 3, product: 4);
        var layout = PadLayout.Of(older, EvdevCapabilities.Parse(ev: "b", key: string.Empty, abs: "25"));
        Assert.Equal(PadAxis.RightX, layout.Axes[0x02]);
        Assert.Equal(PadAxis.RightY, layout.Axes[0x05]);
        Assert.False(layout.Axes.ContainsKey(0x03));
    }

    [Fact]
    public void APadThatHasTheModernAxesKeepsTheStandardMapping()
    {
        var modern = new PadIdentity("New Pad", vendor: 5, product: 6);
        var layout = PadLayout.Of(modern, EvdevCapabilities.Parse(ev: "b", key: string.Empty, abs: "3f"));
        Assert.Equal(PadAxis.LeftTrigger, layout.Axes[0x02]);
        Assert.Equal(PadAxis.RightX, layout.Axes[0x03]);
    }

    [Fact]
    public void ACapitalDottedIAndTheKelvinSignAreFoldedAsTheFixtureFoldsThem()
    {
        var dotted = new PadIdentity("N" + (char)0x130 + "NTENDO pad", vendor: 9, product: 9);
        var kelvin = new PadIdentity("DualShoc" + (char)0x212A + " pad", vendor: 9, product: 9);
        Assert.Equal("Nintendo Controller", PadLayout.Of(dotted).Name);
        Assert.Equal("DualShock", PadLayout.Of(kelvin).Name);
    }

    [Fact]
    public void AStickIsCentredOnTheMiddleOfItsTravel()
    {
        var range = new PadAxisRange(minimum: -32768, maximum: 32767);
        Assert.Equal(-1, range.ToSigned(-32768), 1e-3);
        Assert.Equal(1, range.ToSigned(32767), 1e-3);
        Assert.Equal(0, range.ToSigned(0), 1e-4);
    }

    [Fact]
    public void ATriggerRunsNoughtToOne()
    {
        var range = new PadAxisRange(minimum: 0, maximum: 1023);
        Assert.Equal(0, range.ToUnsigned(0));
        Assert.Equal(1, range.ToUnsigned(1023));
        Assert.Equal(0.5, range.ToUnsigned(512), 1e-2);
    }

    [Fact]
    public void ATenBitTriggerIsNotReadAsAnEightBitOne()
    {
        var wide = new PadAxisRange(minimum: 0, maximum: 1023);
        var narrow = new PadAxisRange(minimum: 0, maximum: 255);
        Assert.Equal(0.249, wide.ToUnsigned(255), 1e-3);
        Assert.Equal(1, narrow.ToUnsigned(255));
    }

    [Fact]
    public void ASymmetricRangeIsNotATrigger()
    {
        Assert.False(PadAxisRange.Stick.IsUnipolar);
        Assert.True(PadAxisRange.Trigger.IsUnipolar);
    }

    [Fact]
    public void ARangeOfNoWidthAnswersRatherThanDividingByNothing()
    {
        var broken = new PadAxisRange(minimum: 7, maximum: 7);
        Assert.Equal(0, broken.ToSigned(7));
        Assert.Equal(0, broken.ToUnsigned(7));
    }

    [Fact]
    public void AValueBeyondTheStatedRangeIsClamped()
    {
        var range = new PadAxisRange(minimum: -100, maximum: 100);
        Assert.Equal(1, range.ToSigned(1000));
        Assert.Equal(-1, range.ToSigned(-1000));
    }
}

namespace Orblit.Terrain.Tests;

public class CoverTests
{
    // Cover

    [Fact]
    public void EachFieldSitsInItsOwnBits()
    {
        Assert.Equal(0xF8000000u, Cover.Of(@base: 31).Word);
        Assert.Equal(0x07C00000u, Cover.Of(overlay: 31).Word);
        Assert.Equal(0x003FC000u, Cover.Of(blend: 1).Word);
        Assert.Equal(0x00003C00u, Cover.Of(angle: 15).Word);
        Assert.Equal(0x00000380u, Cover.Of(scale: -20).Word);
        Assert.Equal(0x4u, Cover.Of(hole: true).Word);
        Assert.Equal(0x2u, Cover.Of(navigation: true).Word);
        Assert.Equal(0x1u, Cover.Of(automatic: true).Word);
    }

    [Fact]
    public void ReadsBackWhatItWasMadeOf()
    {
        var cover = Cover.Of(
            @base: 7,
            overlay: 19,
            blend: 0.5,
            angle: 5,
            scale: 40,
            hole: true,
            automatic: true);
        Assert.Equal(7, cover.Base);
        Assert.Equal(19, cover.Overlay);
        Assert.Equal(128, cover.BlendStep);
        Assert.InRange(cover.Blend, 0.5 - (1.0 / 255), 0.5 + (1.0 / 255));
        Assert.Equal(5, cover.Angle);
        Assert.Equal(40, cover.Scale);
        Assert.True(cover.Hole);
        Assert.False(cover.Navigation);
        Assert.True(cover.Automatic);
    }

    [Fact]
    public void ScaleCodesRunUpTo80ThenBackFrom60()
    {
        foreach (var (code, percent) in Cover.Scales.Index())
        {
            var cover = Cover.Of(scale: percent);
            Assert.Equal(code, cover.ScaleCode);
            Assert.Equal(percent, cover.Scale);
        }

        Assert.InRange(Cover.Of(scale: 20).UvMultiplier, 0.8 - 1e-12, 0.8 + 1e-12);
        Assert.InRange(Cover.Of(scale: -20).UvMultiplier, 1.2 - 1e-12, 1.2 + 1e-12);
        Assert.Equal(40, Cover.Of(scale: 33).Scale);
        Assert.Equal(80, Cover.Of(scale: 500).Scale);
    }

    [Fact]
    public void AnAngleIsSixteenthsOfATurn()
    {
        Assert.InRange(Cover.Of(angle: 4).Radians, (3.14159265 / 2) - 1e-6, (3.14159265 / 2) + 1e-6);
    }

    [Fact]
    public void OutOfRangePartsWrapIntoTheirOwnBits()
    {
        var cover = Cover.Of(@base: 33, overlay: 32, angle: 17);
        Assert.Equal(1, cover.Base);
        Assert.Equal(0, cover.Overlay);
        Assert.Equal(1, cover.Angle);
        Assert.Equal(255, Cover.Of(blend: 3).BlendStep);
        Assert.Equal(0, Cover.Of(blend: -1).BlendStep);
    }

    [Fact]
    public void AChangeLeavesTheOtherFieldsAlone()
    {
        var full = new Cover(0xFFFFFFFF);
        Assert.Equal(0x07FFFFFFu, full.WithBase(0).Word);
        Assert.Equal(0xF83FFFFFu, full.WithOverlay(0).Word);
        Assert.Equal(0xFFC03FFFu, full.WithBlend(0).Word);
        Assert.Equal(0xFFFFC3FFu, full.WithAngle(0).Word);
        Assert.Equal(0xFFFFFC7Fu, full.WithScale(0).Word);
        Assert.Equal(0xFFFFFFFBu, full.WithHole(false).Word);
        Assert.Equal(0xFFFFFFFDu, full.WithNavigation(false).Word);
        Assert.Equal(0xFFFFFFFEu, full.WithAutomatic(false).Word);
        Assert.Equal(0xF8000004u, Cover.Plain.WithBase(31).WithHole(true).Word);
    }

    [Fact]
    public void StaysAThirtyTwoBitWordThroughAUint32List()
    {
        var cover = Cover.Of(@base: 31, overlay: 31, blend: 1, angle: 15);
        var map = new uint[1];
        map[0] = cover.Word;
        Assert.Equal(cover, new Cover(map[0]));
    }

    [Fact]
    public void NewGroundIsAutomatic()
    {
        Assert.True(Cover.Auto.Automatic);
        Assert.Equal(0, Cover.Auto.Base);
        Assert.False(Cover.Plain.Automatic);
    }

    // GroundColour

    [Fact]
    public void NoneIsWhiteAndLeavesRoughnessAlone()
    {
        var none = GroundColour.None;
        Assert.Equal((255, 255, 255), (none.Red, none.Green, none.Blue));
        Assert.Equal(128, none.RoughnessByte);
        Assert.InRange(none.Roughness, -0.005, 0.005);
        Assert.Equal(none, GroundColour.Of());
    }

    [Fact]
    public void RoughnessRunsMinus1ToPlus1AcrossTheByte()
    {
        Assert.Equal(0, GroundColour.Of(roughness: -1).RoughnessByte);
        Assert.Equal(255, GroundColour.Of(roughness: 1).RoughnessByte);
        Assert.Equal(255, GroundColour.Of(roughness: 9).RoughnessByte);
        Assert.Equal(1, GroundColour.Of(roughness: 1).Roughness);
    }

    [Fact]
    public void ChangesOnePartAtATime()
    {
        var colour = GroundColour.Of(red: 10, green: 20, blue: 30);
        Assert.Equal(10, colour.WithRoughness(-1).Red);
        Assert.Equal(128, colour.WithColour(1, 2, 3).RoughnessByte);
        Assert.Equal(0x01020304u, GroundColour.Bytes(1, 2, 3, 4).Rgba);
    }
}

namespace Orblit.Light.Tests;

public class PhotometryTests
{
    [Fact]
    public void WattsBecomeLumensAtTheEyesPeakEfficacy()
    {
        // 683 lumens per watt at 555 nm, which is what Blender's own glTF export assumes.
        Assert.Equal(683, Photometry.WattsToLumens(1), 1e-9);
        Assert.Equal(683000, Photometry.WattsToLumens(1000), 1e-6);
    }

    [Fact]
    public void ASunsIrradianceBecomesIlluminance()
    {
        // Blender's default sun is 1 W/m2. Real daylight is around 100,000 lux.
        Assert.Equal(683, Photometry.IrradianceToLux(1), 1e-9);
    }

    [Fact]
    public void LuminousPowerAndIntensityRoundTripThroughTheSphere()
    {
        const double lumens = 12566.0;
        var candela = Photometry.LumensToCandela(lumens);
        Assert.Equal(lumens, Photometry.CandelaToLumens(candela), 1e-9);
        Assert.Equal(lumens / (4 * Math.PI), candela, 1e-9);
    }

    [Fact]
    public void InfluenceFollowsTheInverseSquareLaw()
    {
        // Four times the light reaches twice as far.
        var near = Photometry.InfluenceRadius(1000);
        var far = Photometry.InfluenceRadius(4000);
        Assert.Equal(near * 2, far, 1e-6);
    }

    [Fact]
    public void ATighterCutoffMeansAWiderInfluence()
    {
        var loose = Photometry.InfluenceRadius(1000, cutoffLux: 1);
        var tight = Photometry.InfluenceRadius(1000, cutoffLux: 0.01);
        Assert.Equal(loose * 10, tight, 1e-6);
    }

    [Fact]
    public void NoLightReachesNowhere() => Assert.Equal(0, Photometry.InfluenceRadius(0));
}

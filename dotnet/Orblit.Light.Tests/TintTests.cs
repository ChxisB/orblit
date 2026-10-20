using Orblit.VectorMath;

namespace Orblit.Light.Tests;

public class TintTests
{
    [Fact]
    public void ReadsAColourTheWayOneIsWritten()
    {
        var tint = Tint.Hex(0xFF8A3D);
        Assert.Equal(1.0, tint.Red);
        Assert.Equal(0x8A / 255.0, tint.Green, 1e-9);
        Assert.Equal(0x3D / 255.0, tint.Blue, 1e-9);
    }

    [Fact]
    public void ConvertsByTheCurveSrgbIsDefinedByNotAGammaOf22()
    {
        // The dark end is where the two disagree enough to see. A gamma of 2.2
        // would put mid-grey at 0.218; the real transfer function puts it at
        // 0.216, and the straight segment near black is off by far more.
        Assert.Equal(0.2140, new Tint(0.5, 0.5, 0.5).Linear.X, 1e-3);
        Assert.Equal(0.02 / 12.92, new Tint(0.02, 0.02, 0.02).Linear.X, 1e-9);
        Assert.Equal(1.0, Tint.White.Linear.X, 1e-9);
        Assert.Equal(0.0, Tint.Black.Linear.X);
    }

    [Fact]
    public void ComesBackFromLinearTheColourItWentOutAs()
    {
        // A colour that survives the trip out and back is the difference
        // between a scene reopening as it was saved and one that shifts a
        // little every time somebody opens it.
        foreach (var tint in new[]
        {
            Tint.Hex(0xFF8A3D),
            new Tint(0.5, 0.5, 0.5),
            new Tint(0.02, 0, 0.004),
            Tint.White,
            Tint.Black,
        })
        {
            var back = Tint.FromLinear(tint.Linear);
            Assert.Equal(tint.Red, back.Red, 1e-9);
            Assert.Equal(tint.Green, back.Green, 1e-9);
            Assert.Equal(tint.Blue, back.Blue, 1e-9);
        }
    }

    [Fact]
    public void ALinearValueThatIsNotAColourIsBroughtBackToOne()
    {
        // An HDR factor or a NaN in somebody else's file is a colour that
        // cannot be shown, not a reason to refuse the scene it came in.
        var odd = Tint.FromLinear(new Vector3(4, -1, double.NaN));
        Assert.Equal(1.0, odd.Red, 1e-9);
        Assert.Equal(0.0, odd.Green);
        Assert.Equal(0.0, odd.Blue);
    }

    [Fact]
    public void MixesAsAnEyeReadsItNotAsLightAddsUp()
    {
        // Halfway from black to white is mid-grey to look at, which is a fifth
        // of the light. Interpolating linear values would put it at a half.
        var middle = Tint.Lerp(Tint.Black, Tint.White, 0.5);
        Assert.Equal(0.5, middle.Red);
        Assert.True(middle.Linear.X < 0.25);
    }
}

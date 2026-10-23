using static Orblit.Motion.Tests.Rigs;

namespace Orblit.Motion.Tests;

public class RetargetTests
{
    [Fact]
    public void MultipliesTurnsInTheOrderMatricesDo()
    {
        var a = Tip(20, 40, 0);
        var b = Tip(-30, 10, 50);
        var product = (a * b).AsRotationMatrix();
        var expected = a.AsRotationMatrix();
        expected.Multiply(b.AsRotationMatrix());
        for (var e = 0; e < 9; e++)
        {
            Assert.Equal(expected[e], product[e], 1e-12);
        }
    }
}

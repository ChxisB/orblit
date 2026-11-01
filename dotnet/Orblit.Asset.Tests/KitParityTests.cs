using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.KitCases;

namespace Orblit.Asset.Tests;

public class KitParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_asset", "kit");

    // The same bytes out of encode, and the same refusals, for good manifests and every way to break one.
    [Fact]
    public void ManifestsMatchTheFixture() =>
        Golden.Check("manifest", c => Dump.Attempt(() => ManifestOut(Convert.FromBase64String(Text(c, 0)), Text(c, 1))));

    [Fact]
    public void ManifestsAtTheSizeLimitMatchTheFixture() =>
        Golden.Check("manifestPadded", c => Dump.Attempt(() => ManifestOut(Padded(In(c, 0).GetInt32()), Text(c, 1))));

    [Fact]
    public void KitRulesMatchTheFixture() => Golden.Check("rules", c => Dump.Attempt(() => RulesOut(c)));

    [Fact]
    public void PlaceProblemsMatchTheFixture() => Golden.Check("placeProblem", c => Dump.Attempt(() => PlaceProblemOut(c)));

    [Fact]
    public void PlacesMatchTheFixture() => Golden.Check("place", c => Dump.Attempt(() => PlaceOut(c)));

    [Fact]
    public void EngineRangesMatchTheFixture() => Golden.Check("fits", c => Dump.Attempt(() => FitsOut(c)));

    [Fact]
    public void UpdatePlansMatchTheFixture() => Golden.Check("update", c => Dump.Attempt(() => UpdateOut(c)));

    [Fact]
    public void ScriptsMatchTheFixture() => Golden.Check("scripts", c => Dump.Attempt(() => ScriptsOut(Text(c, 0))));
}

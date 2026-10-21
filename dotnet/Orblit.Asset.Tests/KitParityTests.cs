using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.KitCases;

namespace Orblit.Asset.Tests;

public class KitParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_asset", "kit");

    // The same bytes out of encode, and the same refusals, for good manifests and every way to break one.
    [Fact]
    public void ManifestsMatchTheDartTwin() =>
        Dart.Check("manifest", c => Dump.Attempt(() => ManifestOut(Convert.FromBase64String(Text(c, 0)), Text(c, 1))));

    [Fact]
    public void ManifestsAtTheSizeLimitMatchTheDartTwin() =>
        Dart.Check("manifestPadded", c => Dump.Attempt(() => ManifestOut(Padded(In(c, 0).GetInt32()), Text(c, 1))));

    [Fact]
    public void KitRulesMatchTheDartTwin() => Dart.Check("rules", c => Dump.Attempt(() => RulesOut(c)));

    [Fact]
    public void PlaceProblemsMatchTheDartTwin() => Dart.Check("placeProblem", c => Dump.Attempt(() => PlaceProblemOut(c)));

    [Fact]
    public void PlacesMatchTheDartTwin() => Dart.Check("place", c => Dump.Attempt(() => PlaceOut(c)));

    [Fact]
    public void EngineRangesMatchTheDartTwin() => Dart.Check("fits", c => Dump.Attempt(() => FitsOut(c)));

    [Fact]
    public void UpdatePlansMatchTheDartTwin() => Dart.Check("update", c => Dump.Attempt(() => UpdateOut(c)));

    [Fact]
    public void ScriptsMatchTheDartTwin() => Dart.Check("scripts", c => Dump.Attempt(() => ScriptsOut(Text(c, 0))));
}

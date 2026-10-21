namespace Orblit.Asset.Tests;

public sealed class SplatImporterTests : NativeToolWork
{
    private static IReadOnlyDictionary<string, object?> Resolve(Dictionary<string, object?>? values = null) =>
        new SplatImporter().ResolveSettings(new ImportSettings(values: values));

    [ShellFact]
    public async Task CooksToOsplatKeepingEveryBandUnlessToldOtherwise()
    {
        var importer = new SplatImporter(Fake("writeArg=1"));
        var result = await importer.ImportAsync(RequestFor("capture.ply", Resolve()));
        Assert.Equal(["osplat"], result.Outputs.Keys);
        AssertInOrder(OnlyRun(), "--harmonics", "3");
        Assert.DoesNotContain("--limit", OnlyRun());
    }

    [ShellFact]
    public async Task ALimitIsPassedOnAndIsHowAPhoneGetsTheSmallFile()
    {
        var importer = new SplatImporter(Fake("writeArg=1"));
        var settings = Resolve(new Dictionary<string, object?> { ["harmonics"] = 1L, ["limit"] = 200000L });
        await importer.ImportAsync(RequestFor("capture.ply", settings));
        AssertInOrder(OnlyRun(), "--harmonics", "1");
        AssertInOrder(OnlyRun(), "--limit", "200000");
    }

    [Fact]
    public void RefusesAHarmonicsDegreeTheFormatDoesNotHave() =>
        Assert.Throws<FormatException>(() => Resolve(new Dictionary<string, object?> { ["harmonics"] = 4L }));

    [Fact]
    public void RefusesALimitOfNothingWhichWouldCookAnEmptyCloud() =>
        Assert.Throws<FormatException>(() => Resolve(new Dictionary<string, object?> { ["limit"] = 0L }));
}

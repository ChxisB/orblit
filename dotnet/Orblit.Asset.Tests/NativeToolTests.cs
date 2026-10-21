namespace Orblit.Asset.Tests;

public sealed class NativeToolTests : NativeToolWork
{
    [Fact]
    public async Task AnAbsentProgramIsAnErrorNamingWhereItLooked()
    {
        var missing = new NativeTool("not_a_program_anyone_has", environmentVariable: "ORBLIT_NOT_A_PROGRAM");
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => missing.RunAsync([], AssetId.Parse("a.png")));
        Assert.Contains("not built", failure.Reason);
        Assert.Contains("ORBLIT_NOT_A_PROGRAM", failure.Reason);
    }

    [ShellFact]
    public async Task ABadExitCarriesWhatTheProgramSaidWhichIsThePartWorthReading()
    {
        var tool = Fake("say=unsupported PNG bit depth;exit=2");
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => tool.RunAsync(["x"], AssetId.Parse("a.png")));
        Assert.Contains("exited 2", failure.Reason);
        Assert.Contains("unsupported PNG bit depth", failure.Cause?.ToString());
    }

    [ShellFact]
    public async Task AGoodExitSaysNothing()
    {
        await Fake("").RunAsync(["x"], AssetId.Parse("a.png"));
        Assert.Equal(["x"], OnlyRun());
    }
}

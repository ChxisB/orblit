namespace Orblit.Save.Tests;

public sealed class ReasonTests
{
    [Fact]
    public void EveryReasonReadsBackFromItsLabel()
    {
        SaveReason[] reasons =
        [
            new Autosave(),
            new Leave(),
            new Disconnect(),
            new Shutdown(),
            new Failed(),
            new Custom("pre-restore"),
        ];

        foreach (var reason in reasons)
        {
            Assert.Equal(reason, SaveReason.Parse(reason.Label));
        }

        Assert.Equal(reasons.Length, reasons.Select(r => r.Label).Distinct().Count());
    }

    [Fact]
    public void ALabelThatIsNotAReasonThrows()
    {
        Assert.Throws<FormatException>(() => SaveReason.Parse("nap"));
        Assert.Throws<FormatException>(() => SaveReason.Parse(""));
    }

    [Fact]
    public void ACustomReasonKeepsItsTextAndRefusesAnEmptyOrLongOne()
    {
        var custom = Assert.IsType<Custom>(SaveReason.Parse("custom:pre-restore"));
        Assert.Equal("pre-restore", custom.Text);
        Assert.ThrowsAny<ArgumentException>(() => new Custom(""));
        Assert.ThrowsAny<ArgumentException>(() => new Custom(new string('a', 65)));
        Assert.ThrowsAny<ArgumentException>(() => new Custom("a\nb"));
        Assert.Equal(64, new Custom(new string('a', 64)).Text.Length);
    }

    [Fact]
    public void ACustomReasonIsNotAnEngineReasonOfTheSameName()
    {
        Assert.NotEqual<SaveReason>(new Custom("autosave"), new Autosave());
    }
}

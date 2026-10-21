using System.Text;

namespace Orblit.Asset.Tests;

public class KitUpdateTests
{
    private static ContentHash HashOf(string text) => ContentHash.Of(Encoding.UTF8.GetBytes(text));

    private static KitUpdatePlan Plan(
        Dictionary<string, string>? locked = null,
        Dictionary<string, string>? incoming = null,
        Dictionary<string, string?>? onDisk = null) =>
        KitUpdatePlan.Plan(
            (locked ?? []).ToDictionary(e => AssetId.Parse(e.Key), e => HashOf(e.Value)),
            (incoming ?? []).ToDictionary(e => AssetId.Parse(e.Key), e => HashOf(e.Value)),
            (onDisk ?? []).ToDictionary(e => AssetId.Parse(e.Key), e => e.Value is null ? null : (ContentHash?)HashOf(e.Value)));

    private static string[] Names(IEnumerable<AssetId> ids) => [.. ids.Select(id => id.ToString())];

    [Fact]
    public void ReplacesAFileThatIsAsTheLockHasItAndChanged()
    {
        var result = Plan(
            locked: new() { ["a/x"] = "1" },
            incoming: new() { ["a/x"] = "2" },
            onDisk: new() { ["a/x"] = "1" });

        Assert.Equal(["a/x"], Names(result.Replace));
        Assert.Empty(result.Kept);
    }

    [Fact]
    public void KeepsAndListsAFileThePersonEdited()
    {
        var result = Plan(
            locked: new() { ["a/x"] = "1" },
            incoming: new() { ["a/x"] = "2" },
            onDisk: new() { ["a/x"] = "mine" });

        Assert.Empty(result.Replace);
        Assert.Equal(["a/x"], Names(result.Kept));
    }

    [Fact]
    public void SaysNothingOfAnEditedFileTheReleaseDidNotChange()
    {
        var result = Plan(
            locked: new() { ["a/x"] = "1" },
            incoming: new() { ["a/x"] = "1" },
            onDisk: new() { ["a/x"] = "mine" });

        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void LeavesAFileThePersonDeletedAndListsIt()
    {
        var result = Plan(
            locked: new() { ["a/x"] = "1" },
            incoming: new() { ["a/x"] = "2" },
            onDisk: new() { ["a/x"] = null });

        Assert.Empty(result.Replace);
        Assert.Equal(["a/x"], Names(result.Kept));
    }

    [Fact]
    public void RemovesADroppedFileOnlyWhenItIsUnchanged()
    {
        var result = Plan(
            locked: new() { ["a/gone"] = "1", ["a/edited"] = "1", ["a/missing"] = "1" },
            onDisk: new() { ["a/gone"] = "1", ["a/edited"] = "mine", ["a/missing"] = null });

        Assert.Equal(["a/gone"], Names(result.Remove));
        Assert.Equal(["a/edited"], Names(result.Kept));
    }

    [Fact]
    public void AddsANewFileUnlessAnotherOneIsInItsPlace()
    {
        var result = Plan(
            incoming: new() { ["a/new"] = "1", ["a/taken"] = "1", ["a/same"] = "1" },
            onDisk: new() { ["a/new"] = null, ["a/taken"] = "mine", ["a/same"] = "1" });

        Assert.Equal(["a/new"], Names(result.Add));
        Assert.Equal(["a/taken"], Names(result.Kept));
    }
}

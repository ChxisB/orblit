using Orblit.Scene;
using Orblit.VectorMath;

namespace Orblit.Test.Tests;

public sealed class GoldenTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("orblit_test_");

    public void Dispose() => _folder.Delete(recursive: true);

    private string Path(string name) => System.IO.Path.Combine(_folder.FullName, name);

    [Fact]
    public void NothingChangedIsAnEmptyList()
    {
        var scene = new SceneDocument(entities: [Things.Make("a")]);

        Assert.Equal("[]\n", Golden.DescribeChange(scene, scene));
    }

    [Fact]
    public void ADiffNamesTheEntityAndTheFieldThatMoved()
    {
        var before = new SceneDocument(entities: [Things.Make("a", at: Vector3.Zero())]);
        var after = new SceneDocument(entities: [Things.Make("a", at: new Vector3(1, 0, 0))]);

        var text = Golden.DescribeChange(before, after);

        Assert.EndsWith("\n", text);
        Assert.Contains("\"a\"", text);
        Assert.Contains("position", text);
        Assert.Contains("\n  {", text);
    }

    [Fact]
    public void TextThatEqualsTheFilePasses()
    {
        File.WriteAllText(Path("same.txt"), "one\ntwo\n");

        Golden.MatchesTextFile("one\ntwo\n", Path("same.txt"), update: false);
    }

    [Fact]
    public void AFileWithWindowsLineEndingsIsReadAsIfItHadNewlines()
    {
        File.WriteAllText(Path("crlf.txt"), "one\r\ntwo\r\n");

        Golden.MatchesTextFile("one\ntwo\n", Path("crlf.txt"), update: false);
    }

    [Fact]
    public void ADifferentLineIsNamedByNumberWithBothSides()
    {
        File.WriteAllText(Path("a.txt"), "one\ntwo\nthree\n");

        var error = Assert.Throws<ExpectationException>(
            () => Golden.MatchesTextFile("one\n2\nthree\n", Path("a.txt"), update: false));

        Assert.Contains("differs at line 2", error.Message);
        Assert.Contains("\"two\"", error.Message);
        Assert.Contains("\"2\"", error.Message);
    }

    [Fact]
    public void ALongerTextSaysTheFileHasNothingThere()
    {
        File.WriteAllText(Path("short.txt"), "one\n");

        var error = Assert.Throws<ExpectationException>(
            () => Golden.MatchesTextFile("one\ntwo\n", Path("short.txt"), update: false));

        Assert.Contains("differs at line 2", error.Message);
        Assert.Contains("the file has nothing", error.Message);
        Assert.Contains("\"two\"", error.Message);
    }

    [Fact]
    public void ATextThatOnlyDiffersInItsLineEndingsSaysSo()
    {
        File.WriteAllText(Path("lf.txt"), "one\ntwo\n");

        var error = Assert.Throws<ExpectationException>(
            () => Golden.MatchesTextFile("one\rtwo\r", Path("lf.txt"), update: false));

        Assert.Contains("differs only in its line endings", error.Message);
    }

    [Fact]
    public void AMissingFileFailsAndSaysHowToWriteIt()
    {
        var error = Assert.Throws<ExpectationException>(
            () => Golden.MatchesTextFile("text", Path("none.txt"), update: false));

        Assert.Contains("there is no such file", error.Message);
        Assert.Contains("ORBLIT_UPDATE_GOLDENS=1", error.Message);
        Assert.False(File.Exists(Path("none.txt")));
    }

    [Fact]
    public void UpdatingWritesTheFileAndItsFolderThenPasses()
    {
        var path = System.IO.Path.Combine(Path("deep"), "made", "new.txt");

        Golden.MatchesTextFile("fresh\n", path, update: true);

        Assert.Equal("fresh\n", File.ReadAllText(path));
    }

    [Fact]
    public void UpdatingReplacesAFileThatDiffers()
    {
        File.WriteAllText(Path("old.txt"), "old\n");

        Golden.MatchesTextFile("new\n", Path("old.txt"), update: true);

        Assert.Equal("new\n", File.ReadAllText(Path("old.txt")));
    }

    [Fact]
    public void ANullTextIsRefusedAndWritesNoFile()
    {
        Assert.Throws<ArgumentNullException>(() => Golden.MatchesTextFile(null!, Path("null.txt"), update: true));
        Assert.False(File.Exists(Path("null.txt")));
    }
}

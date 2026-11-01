using System.Runtime.Versioning;
using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

// The kit command: what it prints, what it writes and the exit code it gives.
public sealed class KitCliTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 10, 19, 23, 59, 0, DateTimeKind.Utc);

    private readonly KitWorkspace _work = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    // A file named after a url lands in the current folder, so each test gets a name of its own.
    private readonly string _hostedFile = $"crate-pack-{Guid.NewGuid():N}.opack";

    private readonly Dictionary<WebUrl, TransportPage> _pages = [];

    public static bool NotWindows => !OperatingSystem.IsWindows();

    public void Dispose()
    {
        File.Delete(HostedPath);
        _out.Dispose();
        _err.Dispose();
        _work.Dispose();
    }

    private string Hosted => $"https://example.com/kits/{_hostedFile}";

    private string HostedPath => Path.Combine(Directory.GetCurrentDirectory(), _hostedFile);

    private async Task<int> Run(params string[] args)
    {
        _out.GetStringBuilder().Clear();
        _err.GetStringBuilder().Clear();
        return await KitCli.RunAsync(
            args, new KitCliEnvironment(_out, _err, () => Day, () => new MapTransport(_pages)));
    }

    // Runs the command and checks that it worked, showing what it said when it did not.
    private async Task Succeeds(params string[] args)
    {
        var code = await Run(args);

        Assert.Equal("", _err.ToString());
        Assert.Equal(0, code);
    }

    // A folder holding a manifest and models/crate.obj.
    private async Task<string> SourceAsync()
    {
        var folder = _work.At("crates");
        await KitWorkspace.WriteAsync(
            folder,
            new() { ["kit.json"] = KitWorkspace.Authored().ToJsonString(), ["models/crate.obj"] = "v 0 0 0" });
        return folder;
    }

    private static Task WriteAsync(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
    }

    private async Task<JsonNode> ListedReleaseAsync()
    {
        var text = await File.ReadAllTextAsync(_work.At("kits/brass-lamp/crate-pack.json"), TestContext.Current.CancellationToken);
        var release = JsonNode.Parse(text)?["releases"]?.AsArray().Single();
        return Assert.IsAssignableFrom<JsonNode>(release);
    }

    [Fact]
    public async Task PacksChecksAndInstallsAKit()
    {
        await Succeeds("pack", await SourceAsync(), "-o", _work.At("crates.opack"));
        Assert.StartsWith("1 files, ", _out.ToString());
        Assert.EndsWith($"  {_work.At("crates.opack")}\n", _out.ToString());
        await Succeeds("check", _work.At("crates.opack"));
        Assert.Equal("Crate pack 1.0.0, 1 files, all intact\n", _out.ToString());
        Directory.CreateDirectory(_work.At("mine"));

        await Succeeds("extract", _work.At("crates.opack"), _work.At("mine"));

        Assert.Equal($"1 files written to {_work.At("mine")}\n", _out.ToString());
        Assert.Equal(
            "v 0 0 0", await File.ReadAllTextAsync(_work.At("mine/models/crate.obj"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnInstallOverAChangedFileExits1UntilToldToOverwrite()
    {
        await Succeeds("pack", await SourceAsync(), "-o", _work.At("crates.opack"));
        await WriteAsync(_work.At("mine/models/crate.obj"), "mine");

        Assert.Equal(1, await Run("extract", _work.At("crates.opack"), _work.At("mine")));
        Assert.Contains("models/crate.obj", _err.ToString());
        Assert.Contains("--overwrite", _err.ToString());

        await Succeeds("extract", _work.At("crates.opack"), _work.At("mine"), "--overwrite");
        Assert.Equal(
            "v 0 0 0", await File.ReadAllTextAsync(_work.At("mine/models/crate.obj"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AFolderThatBreaksARuleExits1AndNamesTheFile()
    {
        await WriteAsync($"{await SourceAsync()}/.DS_Store", "x");

        Assert.Equal(1, await Run("pack", _work.At("crates"), "-o", _work.At("crates.opack")));
        Assert.Contains(".DS_Store", _err.ToString());
    }

    [Fact]
    public async Task ACheckSaysWhenTheKitHoldsScripts()
    {
        var source = await SourceAsync();
        await WriteAsync($"{source}/tools/build.js", "run()");

        await Succeeds("pack", source, "-o", _work.At("crates.opack"));
        await Succeeds("check", _work.At("crates.opack"));

        Assert.Equal("Crate pack 1.0.0, 2 files, all intact, holds scripts\n", _out.ToString());
    }

    [Fact]
    public async Task ACheckOfAChangedFileListsItAndExits1()
    {
        await Succeeds("pack", await SourceAsync(), "-o", _work.At("crates.opack"));
        var bytes = await File.ReadAllBytesAsync(_work.At("crates.opack"), TestContext.Current.CancellationToken);
        bytes[bytes.AsSpan().IndexOf("v 0 0 0"u8)] ^= 1;
        await File.WriteAllBytesAsync(_work.At("crates.opack"), bytes, TestContext.Current.CancellationToken);

        Assert.Equal(1, await Run("check", _work.At("crates.opack")));

        Assert.Contains("models/crate.obj", _err.ToString());
        Assert.Contains("kit: 1 problems.", _err.ToString());
        Assert.Equal("", _out.ToString());
    }

    [Fact]
    public async Task AFileThatIsNotAPackExits1()
    {
        await WriteAsync(_work.At("plain.opack"), "not a pack, and long enough to read a header");

        Assert.Equal(1, await Run("check", _work.At("plain.opack")));
    }

    [Fact(Skip = "A mode of none does not stop a read on Windows.", SkipUnless = nameof(NotWindows))]
    [UnsupportedOSPlatform("windows")]
    public async Task AFileThatCannotBeReadExits1()
    {
        await Succeeds("pack", await SourceAsync(), "-o", _work.At("crates.opack"));
        File.SetUnixFileMode(_work.At("crates.opack"), UnixFileMode.None);

        Assert.Equal(1, await Run("check", _work.At("crates.opack")));
        Assert.StartsWith("kit: ", _err.ToString());
    }

    [Fact]
    public async Task ACheckOfAFileThatIsNotThereExits1AndSaysSo()
    {
        Assert.Equal(1, await Run("check", _work.At("nothing.opack")));
        Assert.Contains("There is no such file", _err.ToString());
        Assert.Contains("nothing.opack", _err.ToString());
    }

    [Fact]
    public async Task ArgumentsThatMakeNoSenseExit2()
    {
        Assert.Equal(2, await Run());
        Assert.Contains("Usage: orblit-kit", _out.ToString());
        Assert.Equal(2, await Run("spin"));
        Assert.Contains("unknown command \"spin\"", _err.ToString());
        Assert.Contains("Usage: orblit-kit", _err.ToString());
        Assert.Equal(2, await Run("pack", _work.At("crates")));
        Assert.Equal(2, await Run("check"));
        Assert.Equal(2, await Run("index", _work.At("kits")));
        Assert.Equal(0, await Run("--help"));
        Assert.Contains("Usage: orblit-kit", _out.ToString());
    }

    [Fact]
    public async Task PublishPacksTheKitAndWritesTheEntryForTheIndex()
    {
        Directory.CreateDirectory(_work.At("kits"));

        await Succeeds(
            "publish", await SourceAsync(), "--url", Hosted, "-o", _work.At(_hostedFile),
            "--entries", _work.At("kits"), "--notes", "The first release.");

        var release = await ListedReleaseAsync();
        Assert.Equal(Hosted, (string?)release["url"]);
        Assert.Equal(new FileInfo(_work.At(_hostedFile)).Length, (long?)release["bytes"]);
        Assert.Equal("The first release.", (string?)release["notes"]);
        Assert.Equal("2026-10-19", (string?)release["released"]);
        Assert.Contains((string?)release["hash"] ?? "", _out.ToString());
        Assert.Contains("Wrote the entry to", _out.ToString());
        Assert.Contains($"orblit-kit index {_work.At("kits")} -o <new folder>", _out.ToString());
        Assert.Contains($"Upload {_work.At(_hostedFile)}", _out.ToString());
        await Succeeds("check", _work.At(_hostedFile));
    }

    [Fact]
    public async Task PublishNamesTheFileAfterTheUrl()
    {
        await Succeeds("publish", await SourceAsync(), "--url", Hosted);

        Assert.True(File.Exists(HostedPath));
        Assert.Contains($"Packed {_hostedFile}: 1 files", _out.ToString());
        Assert.Contains($"Upload {_hostedFile} so that it is served at {Hosted}", _out.ToString());
    }

    [Fact]
    public async Task PublishTakesTheFileFromOutSpeltInFull()
    {
        await Succeeds("publish", await SourceAsync(), "--url", Hosted, "--out", _work.At("long.opack"));

        Assert.True(File.Exists(_work.At("long.opack")));
        Assert.False(File.Exists(HostedPath));
    }

    [Fact]
    public async Task PublishWithoutEntriesPrintsTheEntryOnly()
    {
        await Succeeds("publish", await SourceAsync(), "--url", Hosted, "-o", _work.At("mine.opack"));

        Assert.True(File.Exists(_work.At("mine.opack")));
        Assert.Contains("\"version\": \"1.0.0\"", _out.ToString());
        Assert.Contains("Save the entry above as kits/", _out.ToString());
        Assert.Contains("orblit-kit index kits -o <new folder>", _out.ToString());
        Assert.False(Directory.Exists(_work.At("kits")));
    }

    [Fact]
    public async Task PublishRefusesAVersionTheEntryListsAndKeepsTheEntry()
    {
        Directory.CreateDirectory(_work.At("kits"));
        var source = await SourceAsync();
        string[] arguments = ["publish", source, "--url", Hosted, "-o", _work.At(_hostedFile), "--entries", _work.At("kits")];
        await Succeeds(arguments);
        var entry = _work.At("kits/brass-lamp/crate-pack.json");
        var before = await File.ReadAllTextAsync(entry, TestContext.Current.CancellationToken);

        Assert.Equal(1, await Run(arguments));

        Assert.Contains("Raise \"version\" in kit.json", _err.ToString());
        Assert.Equal(before, await File.ReadAllTextAsync(entry, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishExits2WithoutAUrlOrAFileName()
    {
        var source = await SourceAsync();

        Assert.Equal(2, await Run("publish", source));
        Assert.Equal(2, await Run("publish", "--url", Hosted));
        Assert.Equal(2, await Run("publish", source, "--url", "https://e.com/"));
        Assert.Contains("give one with -o", _err.ToString());
        Assert.Equal(2, await Run("publish", source, "--url"));
        Assert.Equal(2, await Run("publish", source, "--nope", "x"));
        Assert.Contains("unknown option \"--nope\"", _err.ToString());
        Assert.Equal(2, await Run("publish", source, source));
        Assert.Contains("one folder at a time", _err.ToString());
    }

    [Fact]
    public async Task PublishExits1ForAFolderThatBreaksARule()
    {
        await WriteAsync($"{await SourceAsync()}/.DS_Store", "x");

        Assert.Equal(1, await Run("publish", _work.At("crates"), "--url", Hosted));

        Assert.Contains(".DS_Store", _err.ToString());
        Assert.False(File.Exists(HostedPath));
    }

    [Fact]
    public async Task AnEmptyEntriesFolderBuildsAnEmptyIndex()
    {
        await WriteAsync(_work.At("kits/.gitkeep"), "");

        await Succeeds("index", _work.At("kits"), "-o", _work.At("site"));

        Assert.Contains("0 kits, 0 releases", _out.ToString());
        Assert.True(File.Exists(_work.At("site/index.json")));
    }

    [Fact]
    public async Task AnIndexGoesToANewFolderAndNamesABadEntry()
    {
        Directory.CreateDirectory(_work.At("kits"));
        Directory.CreateDirectory(_work.At("site"));

        Assert.Equal(1, await Run("index", _work.At("kits"), "-o", _work.At("site")));
        Assert.Contains("is there already", _err.ToString());
        await WriteAsync(_work.At("kits/amber/rocks.json"), "{");

        Assert.Equal(1, await Run("index", _work.At("kits"), "-o", _work.At("fresh")));

        Assert.Contains("amber/rocks", _err.ToString());
        Assert.False(Directory.Exists(_work.At("fresh")));
    }

    [Fact]
    public async Task AnIndexDownloadsTheReleasesTheEntriesListOverItsNetwork()
    {
        Directory.CreateDirectory(_work.At("kits"));
        await Succeeds(
            "publish", await SourceAsync(), "--url", Hosted, "-o", _work.At(_hostedFile), "--entries", _work.At("kits"));
        _pages[WebUrl.Parse(Hosted)] = new TransportPage(
            await File.ReadAllBytesAsync(_work.At(_hostedFile), TestContext.Current.CancellationToken));

        await Succeeds("index", _work.At("kits"), "-o", _work.At("site"));

        Assert.Contains($"1 kits, 1 releases written to {_work.At("site")}", _out.ToString());
    }
}

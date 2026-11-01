namespace Orblit.Asset.Tests;

public sealed class DirectoryAssetSourceTests : IDisposable
{
    private const string NoLinks = "links need extra privileges on Windows";

    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_asset_source_");
    private readonly string _root;
    private readonly string _outside;

    public DirectoryAssetSourceTests()
    {
        _root = At(_temp.FullName, "project");
        _outside = At(_temp.FullName, "elsewhere");
        Write(At(_root, "models/robot.glb"), [1, 2, 3]);
        Write(At(_outside, "secret.txt"), [4, 5, 6]);
    }

    public void Dispose() => _temp.Delete(recursive: true);

    public static bool CanLink => !OperatingSystem.IsWindows();

    private static string At(string basePath, string relative) =>
        Path.Combine([basePath, .. relative.Split('/')]);

    private static void Write(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static async Task Refused(DirectoryAssetSource source, string id, string saying)
    {
        var error = await Assert.ThrowsAsync<AssetNotFoundException>(() => source.ReadAsync(AssetId.Parse(id)));
        Assert.Contains(saying, error.Message);
    }

    // a directory source

    [Fact]
    public async Task ReadsAFileByItsIdSlashesBeingFolders()
    {
        var source = new DirectoryAssetSource(_root);
        var id = AssetId.Parse("models/robot.glb");
        Assert.Equal(new byte[] { 1, 2, 3 }, await source.ReadAsync(id));
        Assert.True(await source.ExistsAsync(id));
    }

    [Fact]
    public async Task AFileThatIsNotThereIsNotFound()
    {
        var source = new DirectoryAssetSource(_root);
        var id = AssetId.Parse("models/missing.glb");
        await Assert.ThrowsAsync<AssetNotFoundException>(() => source.ReadAsync(id));
        Assert.False(await source.ExistsAsync(id));
    }

    [Fact]
    public async Task AFolderIsNotAnAsset()
    {
        var source = new DirectoryAssetSource(_root);
        var id = AssetId.Parse("models");
        await Assert.ThrowsAsync<AssetNotFoundException>(() => source.ReadAsync(id));
        Assert.False(await source.ExistsAsync(id));
    }

    [Fact]
    public async Task ADirectoryThatDoesNotExistHasNothingInItAndSaysSo()
    {
        var source = new DirectoryAssetSource(At(_temp.FullName, "nowhere"));
        var id = AssetId.Parse("models/robot.glb");
        await Refused(source, "models/robot.glb", "does not exist");
        Assert.False(await source.ExistsAsync(id));
    }

    [Fact(Skip = NoLinks, SkipUnless = nameof(CanLink))]
    public async Task ARootReachedThroughALinkStillReads()
    {
        // Every temporary directory on macOS already is one, /var being a link to /private/var;
        // this makes the same true on every platform.
        var linked = At(_temp.FullName, "linked-project");
        Directory.CreateSymbolicLink(linked, _root);
        Assert.Equal(
            new byte[] { 1, 2, 3 },
            await new DirectoryAssetSource(linked).ReadAsync(AssetId.Parse("models/robot.glb")));
    }

    [Fact(Skip = NoLinks, SkipUnless = nameof(CanLink))]
    public async Task ALinkToAFileElsewhereInsideTheRootIsFollowed()
    {
        File.CreateSymbolicLink(At(_root, "models/alias.glb"), At(_root, "models/robot.glb"));
        var source = new DirectoryAssetSource(_root);
        Assert.Equal(new byte[] { 1, 2, 3 }, await source.ReadAsync(AssetId.Parse("models/alias.glb")));
    }

    // a directory source refuses to read outside its root
    // These are checked by the reason rather than just the exception, so that none of them can pass
    // merely because the link was broken and there was no file to find.

    [Fact(Skip = NoLinks, SkipUnless = nameof(CanLink))]
    public async Task ThroughALinkedFolderThatLeadsOut()
    {
        Directory.CreateSymbolicLink(At(_root, "leak"), _outside);
        var source = new DirectoryAssetSource(_root);

        await Refused(source, "leak/secret.txt", "outside");
        Assert.False(await source.ExistsAsync(AssetId.Parse("leak/secret.txt")));
    }

    [Fact(Skip = NoLinks, SkipUnless = nameof(CanLink))]
    public async Task ThroughALinkedFileThatLeadsOut()
    {
        File.CreateSymbolicLink(At(_root, "secret.txt"), At(_outside, "secret.txt"));
        var source = new DirectoryAssetSource(_root);

        await Refused(source, "secret.txt", "outside");
        Assert.False(await source.ExistsAsync(AssetId.Parse("secret.txt")));
    }

    [Fact(Skip = NoLinks, SkipUnless = nameof(CanLink))]
    public async Task ThroughARelativeLinkThatClimbsOutWithDotDot()
    {
        Directory.CreateSymbolicLink(At(_root, "models/up"), "../../elsewhere");
        await Refused(new DirectoryAssetSource(_root), "models/up/secret.txt", "outside");
    }

    [Fact(Skip = NoLinks, SkipUnless = nameof(CanLink))]
    public async Task ThroughASiblingWhoseNameOnlyStartsWithTheRoots()
    {
        // "project-evil" begins with "project", so a check on the bare prefix without the separator
        // after it would let this through.
        var sibling = $"{_root}-evil";
        Write(At(sibling, "secret.txt"), [7]);
        Directory.CreateSymbolicLink(At(_root, "next-door"), sibling);
        await Refused(new DirectoryAssetSource(_root), "next-door/secret.txt", "outside");
    }
}

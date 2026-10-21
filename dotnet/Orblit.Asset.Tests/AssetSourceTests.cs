namespace Orblit.Asset.Tests;

public class AssetSourceTests
{
    private static readonly AssetId Robot = AssetId.Parse("models/robot.glb");
    private static readonly AssetId Crate = AssetId.Parse("models/crate.glb");
    private static readonly AssetId Missing = AssetId.Parse("models/missing.glb");

    private static async Task NotFound(AssetId id, Func<Task> read)
    {
        var error = await Assert.ThrowsAsync<AssetNotFoundException>(read);
        Assert.Equal(id, error.Id);
        Assert.Contains($"{id}", error.Message);
    }

    private static Task<byte[]?> Broken(string saying) =>
        Task.FromException<byte[]?>(new InvalidOperationException(saying));

    // a memory source

    [Fact]
    public async Task AMemorySourceReadsWhatItWasGiven()
    {
        var source = new MemoryAssetSource(new Dictionary<AssetId, byte[]> { [Robot] = [1, 2, 3] });
        Assert.Equal(new byte[] { 1, 2, 3 }, await source.ReadAsync(Robot));
        Assert.True(await source.ExistsAsync(Robot));
    }

    [Fact]
    public async Task SaysWhichAssetItDoesNotHave()
    {
        var source = new MemoryAssetSource();
        await NotFound(Missing, () => source.ReadAsync(Missing));
        Assert.False(await source.ExistsAsync(Missing));
    }

    [Fact]
    public async Task ChangingWhatWasReadDoesNotChangeWhatIsStored()
    {
        byte[] given = [1, 2, 3];
        var source = new MemoryAssetSource(new Dictionary<AssetId, byte[]> { [Robot] = given });
        given[0] = 9;

        var first = await source.ReadAsync(Robot);
        first[1] = 9;
        Assert.Equal(new byte[] { 1, 2, 3 }, await source.ReadAsync(Robot));
    }

    // a callback source

    [Fact]
    public async Task ReadsWhateverTheFunctionGives()
    {
        var source = new CallbackAssetSource(id => Task.FromResult<byte[]?>(id == Robot ? [4, 5] : null));
        Assert.Equal(new byte[] { 4, 5 }, await source.ReadAsync(Robot));
        Assert.True(await source.ExistsAsync(Robot));
    }

    [Fact]
    public async Task TakesNullToMeanTheAssetIsNotThere()
    {
        var source = new CallbackAssetSource(_ => Task.FromResult<byte[]?>(null));
        await NotFound(Missing, () => source.ReadAsync(Missing));
        Assert.False(await source.ExistsAsync(Missing));
    }

    [Fact]
    public async Task PassesOnAnyOtherFailureRatherThanCallingItMissing()
    {
        var source = new CallbackAssetSource(_ => Broken("the bundle is broken"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.ReadAsync(Robot));
    }

    // a layered source

    private readonly MemoryAssetSource _top = new(new Dictionary<AssetId, byte[]> { [Robot] = [1] });

    private readonly MemoryAssetSource _bottom = new(new Dictionary<AssetId, byte[]> { [Robot] = [2], [Crate] = [3] });

    private LayeredAssetSource Layered() => new([_top, _bottom]);

    [Fact]
    public async Task AnAssetInAnEarlierLayerHidesTheSameOneFurtherDown() =>
        Assert.Equal(new byte[] { 1 }, await Layered().ReadAsync(Robot));

    [Fact]
    public async Task AnAssetOnlyFurtherDownIsStillFound()
    {
        Assert.Equal(new byte[] { 3 }, await Layered().ReadAsync(Crate));
        Assert.True(await Layered().ExistsAsync(Crate));
    }

    [Fact]
    public async Task AnAssetInNoLayerIsNotFound()
    {
        await NotFound(Missing, () => Layered().ReadAsync(Missing));
        Assert.False(await Layered().ExistsAsync(Missing));
    }

    [Fact]
    public async Task WithNoLayersAtAllNothingIsFound()
    {
        var empty = new LayeredAssetSource([]);
        await NotFound(Robot, () => empty.ReadAsync(Robot));
        Assert.False(await empty.ExistsAsync(Robot));
    }

    [Fact]
    public async Task ALayerThatFailsToReadIsNotSkippedOver()
    {
        var broken = new CallbackAssetSource(_ => Broken("the disk is on fire"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LayeredAssetSource([broken, _bottom]).ReadAsync(Robot));
    }

    [Fact]
    public async Task ChangingTheListItWasBuiltFromChangesNothing()
    {
        List<IAssetSource> layers = [_bottom];
        var source = new LayeredAssetSource(layers);
        layers.Insert(0, _top);
        Assert.Equal(new byte[] { 2 }, await source.ReadAsync(Robot));
    }
}

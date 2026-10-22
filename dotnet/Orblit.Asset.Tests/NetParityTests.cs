using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.NetCases;
using static Orblit.Asset.Tests.PackCases;

namespace Orblit.Asset.Tests;

public class NetParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_asset", "net");

    [Fact]
    public void UrlsParseLikeTheFixture() => Golden.Check("url.parse", c => Dump.Attempt(() => ParseOut(c)));

    [Fact]
    public void UrlsResolveLikeTheFixture() => Golden.Check("url.resolve", c => Dump.Attempt(() => ResolveOut(c)));

    [Fact]
    public void ComponentsAreEscapedLikeTheFixture() => Golden.Check("url.encode", c => Dump.Attempt(() => EncodeOut(c)));

    [Fact]
    public void OriginsParseLikeTheFixture() => Golden.Check("origin.parse", c => Dump.Attempt(() => OriginParseOut(c)));

    [Fact]
    public void OriginsPlaceAssetsLikeTheFixture() => Golden.Check("origin.url", c => Dump.Attempt(() => OriginUrlOut(c)));

    [Fact]
    public void OriginsResolveReferencesLikeTheFixture() =>
        Golden.Check("origin.beside", c => Dump.Attempt(() => OriginBesideOut(c)));

    [Fact]
    public void OriginsAnswerAllowsLikeTheFixture() =>
        Golden.Check("origin.allows", c => Dump.Attempt(() => OriginAllowsOut(c)));

    [Fact]
    public void OriginsRequireLikeTheFixture() =>
        Golden.Check("origin.require", c => Dump.Attempt(() => OriginRequireOut(c)));

    [Fact]
    public Task MapTransportsAnswerLikeTheFixture() => CheckAsync(Golden, "transport", TransportOut);

    [Fact]
    public void RecordsParseLikeTheFixture() => Golden.Check("record.parse", c => Dump.Attempt(() => RecordParseOut(c)));

    [Fact]
    public void RecordSetsEncodeLikeTheFixture() => Golden.Check("records.encode", c => Dump.Attempt(() => RecordsEncodeOut(c)));

    [Fact]
    public void RecordSetsDecodeLikeTheFixture() => Golden.Check("records.decode", c => Dump.Attempt(() => RecordsDecodeOut(c)));

    [Fact]
    public void RecordSetsReencodeLikeTheFixture() =>
        Golden.Check("records.reencode", c => Dump.Attempt(() => RecordsReencodeOut(c)));

    [Fact]
    public void PictureSizesReadLikeTheFixture() => Golden.Check("picture", c => Dump.Attempt(() => PictureOut(c)));

    [Fact]
    public void Ktx2ChainsReadAndCutLikeTheFixture() => Golden.Check("ktx2", c => Dump.Attempt(() => Ktx2Out(c)));

    [Fact]
    public Task RangeQueuesSendTheSameRequestsAsTheFixture() => CheckAsync(Golden, "queue", QueueOut);

    [Fact]
    public void RangeQueuesRefuseTheSameSettingsAsTheFixture() =>
        Golden.Check("queue.new", c => Dump.Attempt(() => QueueNewOut(c)));
}

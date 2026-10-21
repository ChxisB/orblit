using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.NetCases;
using static Orblit.Asset.Tests.PackCases;

namespace Orblit.Asset.Tests;

public class NetParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_asset", "net");

    [Fact]
    public void UrlsParseLikeTheDartTwin() => Dart.Check("url.parse", c => Dump.Attempt(() => ParseOut(c)));

    [Fact]
    public void UrlsResolveLikeTheDartTwin() => Dart.Check("url.resolve", c => Dump.Attempt(() => ResolveOut(c)));

    [Fact]
    public void ComponentsAreEscapedLikeTheDartTwin() => Dart.Check("url.encode", c => Dump.Attempt(() => EncodeOut(c)));

    [Fact]
    public void OriginsParseLikeTheDartTwin() => Dart.Check("origin.parse", c => Dump.Attempt(() => OriginParseOut(c)));

    [Fact]
    public void OriginsPlaceAssetsLikeTheDartTwin() => Dart.Check("origin.url", c => Dump.Attempt(() => OriginUrlOut(c)));

    [Fact]
    public void OriginsResolveReferencesLikeTheDartTwin() =>
        Dart.Check("origin.beside", c => Dump.Attempt(() => OriginBesideOut(c)));

    [Fact]
    public void OriginsAnswerAllowsLikeTheDartTwin() =>
        Dart.Check("origin.allows", c => Dump.Attempt(() => OriginAllowsOut(c)));

    [Fact]
    public void OriginsRequireLikeTheDartTwin() =>
        Dart.Check("origin.require", c => Dump.Attempt(() => OriginRequireOut(c)));

    [Fact]
    public Task MapTransportsAnswerLikeTheDartTwin() => CheckAsync(Dart, "transport", TransportOut);

    [Fact]
    public void RecordsParseLikeTheDartTwin() => Dart.Check("record.parse", c => Dump.Attempt(() => RecordParseOut(c)));

    [Fact]
    public void RecordSetsEncodeLikeTheDartTwin() => Dart.Check("records.encode", c => Dump.Attempt(() => RecordsEncodeOut(c)));

    [Fact]
    public void RecordSetsDecodeLikeTheDartTwin() => Dart.Check("records.decode", c => Dump.Attempt(() => RecordsDecodeOut(c)));

    [Fact]
    public void RecordSetsReencodeLikeTheDartTwin() =>
        Dart.Check("records.reencode", c => Dump.Attempt(() => RecordsReencodeOut(c)));

    [Fact]
    public void PictureSizesReadLikeTheDartTwin() => Dart.Check("picture", c => Dump.Attempt(() => PictureOut(c)));

    [Fact]
    public void Ktx2ChainsReadAndCutLikeTheDartTwin() => Dart.Check("ktx2", c => Dump.Attempt(() => Ktx2Out(c)));

    [Fact]
    public Task RangeQueuesSendTheSameRequestsAsTheDartTwin() => CheckAsync(Dart, "queue", QueueOut);

    [Fact]
    public void RangeQueuesRefuseTheSameSettingsAsTheDartTwin() =>
        Dart.Check("queue.new", c => Dump.Attempt(() => QueueNewOut(c)));
}

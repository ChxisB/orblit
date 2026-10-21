using System.Text;

namespace Orblit.Asset.Tests;

// What the network tests share: one origin, the names it serves, and a fetcher wired for a test.
internal static class NetWorld
{
    public static readonly AssetOrigin Origin = AssetOrigin.Parse("https://cdn.example.com/game/");

    // No noise in the backoff, so a test that counts waits gets the same wait every run.
    public static readonly Steady NoNoise = new();

    public static AssetId Id(string path) => AssetId.Parse(path);

    public static WebUrl Url(string path) => Origin.UrlOf(Id(path));

    public static byte[] Body(string text) => Encoding.UTF8.GetBytes(text);

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    public static MapTransport Map(params (string Path, TransportPage Page)[] pages) =>
        new(pages.ToDictionary(p => Url(p.Path), p => p.Page));

    public static string[] Names(MapTransport transport) => [.. transport.Sent.Select(Transports.NameOf)];

    // F2: only the transport is required, and the rest are named at the call.
    public static AssetFetcher FetcherFor(
        IAssetTransport transport,
        IContentStore? store = null,
        IFetchRecords? records = null,
        AssetManifest? manifest = null,
        int concurrent = 4,
        int attempts = 3,
        AssetOrigin? from = null) =>
        new(from ?? Origin, transport, store, records, manifest, concurrent, attempts, TimeSpan.Zero, NoNoise);

    public static Task<byte[]> Read(AssetFetcher fetcher, string path) =>
        fetcher.ReadAsync(Id(path), cancellation: TestContext.Current.CancellationToken);

    public static byte[] Counting(int length) => [.. Enumerable.Range(0, length).Select(i => (byte)(i % 251))];

    public static byte[] Filled(int length, byte value) => [.. Enumerable.Repeat(value, length)];
}

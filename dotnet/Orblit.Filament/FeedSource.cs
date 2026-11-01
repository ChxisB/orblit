namespace Orblit.Filament;

/// <summary>
/// A picture the application draws, which a scene can show on a screen.
/// </summary>
/// <remarks>
/// A video is a file the renderer decodes. A feed is the other case: the application makes the
/// pictures itself, from a camera, a decoder of its own, another view, or a game running somewhere
/// else, and hands each one over as it is finished. The picture stays where its producer wrote it
/// and the GPU reads it there, so a feed costs a screen about what a video does.
/// <para>
/// Making one is two steps. <see cref="Create(int, int)"/> makes the source, and its
/// <see cref="Id"/> goes to whatever draws, which calls <see cref="Submit"/> with each finished
/// picture. A scene shows it by naming a <see cref="Feed"/> in <see cref="RenderScene.Feeds"/> and
/// pointing a screen at its key with <see cref="RenderMaterial.Video"/>.
/// </para>
/// <para>
/// Where it is supported: macOS and iOS take pixel buffers. Linux and Windows say so when one is
/// made.
/// </para>
/// </remarks>
public sealed class FeedSource : IDisposable
{
    private readonly IFeedHost _host;

    private FeedSource(long id, int width, int height, IFeedHost host)
    {
        Id = id;
        Width = width;
        Height = height;
        _host = host;
    }

    /// <summary>The number the producer writes to, and the scene shows.</summary>
    public long Id { get; }

    /// <summary>
    /// What the producer was first asked to draw at. The picture's own size is the producer's, and
    /// one that draws at another size is shown at that one. The renderer does not resize or crop a feed.
    /// </summary>
    public int Width { get; }

    public int Height { get; }

    /// <summary>Whether <see cref="Dispose"/> has been called.</summary>
    public bool IsReleased { get; private set; }

    /// <summary>
    /// Makes a feed. <paramref name="width"/> and <paramref name="height"/> are what the producer
    /// is first asked to draw at, where the platform needs to be told. Apple platforms take each
    /// buffer's own size.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">The platform has no feeds.</exception>
    public static FeedSource Create(int width, int height) => Create(width, height, NativeFeedHost.Instance);

    internal static FeedSource Create(int width, int height, IFeedHost host)
    {
        if (width < 1 || height < 1)
        {
            throw new ArgumentException($"A feed needs a size of at least 1 by 1, not {width} by {height}.");
        }

        if (!host.IsSupported)
        {
            throw new PlatformNotSupportedException("Feeds are not supported on this platform yet.");
        }

        var id = host.Create(width, height);
        return id < 1
            ? throw new InvalidOperationException("The platform did not make a feed.")
            : new FeedSource(id, width, height, host);
    }

    /// <summary>
    /// Makes <paramref name="pixelBuffer"/> the newest picture. It is a <c>CVPixelBufferRef</c> on
    /// Apple, IOSurface backed and 32BGRA or biplanar 4:2:0. It is retained and not copied, and the
    /// producer may reuse it once the next picture is in.
    /// </summary>
    public void Submit(nint pixelBuffer)
    {
        ObjectDisposedException.ThrowIf(IsReleased, this);
        Renderer.Check(_host.Submit(Id, pixelBuffer), "submit_feed_buffer");
    }

    /// <summary>
    /// Lets go of the feed. A screen still showing it keeps its last picture until the scene stops
    /// naming it. A second call does nothing.
    /// </summary>
    public void Dispose()
    {
        if (IsReleased)
        {
            return;
        }

        IsReleased = true;
        _host.Release(Id);
    }
}

/// <summary>What makes a feed's pictures reach the renderer. The renderer's library is the one that ships.</summary>
internal interface IFeedHost
{
    bool IsSupported { get; }

    /// <summary>The feed's number, or nought where one could not be made.</summary>
    long Create(int width, int height);

    void Release(long id);

    /// <summary>The renderer's result code for the picture.</summary>
    int Submit(long id, nint pixelBuffer);
}

internal sealed class NativeFeedHost : IFeedHost
{
    public static readonly NativeFeedHost Instance = new();

    public bool IsSupported => OperatingSystem.IsMacOS() || OperatingSystem.IsIOS();

    public long Create(int width, int height) => NativeMethods.CreateFeed(width, height);

    public void Release(long id) => NativeMethods.ReleaseFeed(id);

    public int Submit(long id, nint pixelBuffer) => NativeMethods.SubmitFeedBuffer(id, pixelBuffer);
}

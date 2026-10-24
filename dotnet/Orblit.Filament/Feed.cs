namespace Orblit.Filament;

/// <summary>
/// A feed, shown: its key, where it is drawn, and how to turn it upright.
/// </summary>
/// <remarks>
/// Stated in <see cref="RenderScene.Feeds"/> and pointed at by a screen's
/// <see cref="RenderMaterial.Video"/>, in the same key space as <see cref="Video"/>, so a key is
/// one or the other and not both in one scene.
/// </remarks>
public sealed class Feed
{
    public Feed(long key, FeedSource source, int rotation = 0, bool mirror = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rotation);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rotation, 3);
        Key = key;
        Source = source;
        Rotation = rotation;
        Mirror = mirror;
    }

    /// <summary>This feed's identity in the scene, stable for as long as it is shown.</summary>
    public long Key { get; }

    /// <summary>What is drawn into, made by <see cref="FeedSource.Create(int, int)"/>.</summary>
    public FeedSource Source { get; }

    /// <summary>
    /// How many quarter turns clockwise the picture needs to be upright. A phone's camera sensor is
    /// fixed in the phone, so its frames arrive turned by however the phone is held. The producer
    /// knows that and says so here, and the screen shows the frames the right way up. The picture
    /// is mirrored first if <see cref="Mirror"/> is set, then turned.
    /// </summary>
    public int Rotation { get; }

    /// <summary>Whether the picture is flipped left to right before it is turned, as a front camera is.</summary>
    public bool Mirror { get; }

    /// <summary>
    /// The name the renderer reads a feed by. Not a path to anything, and never to be passed as
    /// one: a file called this is a feed.
    /// </summary>
    internal string Path => $"feed:{Source.Id}";

    /// <summary>
    /// The orientation's place in a video's flags: bits 2 and 3 are the turns and bit 4 is the
    /// mirror. Bits 0 and 1 are playing and looping, which a feed has no use for.
    /// </summary>
    internal int Flags => (Rotation << 2) | (Mirror ? 16 : 0);
}

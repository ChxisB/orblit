namespace Orblit.Filament;

/// <summary>
/// A moving picture, playing. The host says what state the video should be in and the renderer
/// works out what to do about it. A video already playing the right file at the right rate costs
/// nothing to say again, which is what lets a scene be published whole on every frame.
/// </summary>
public sealed record Video
{
    /// <summary>Floats one video contributes to the message.</summary>
    public const int Stride = 4;

    /// <summary>This video's identity, stable for as long as it exists.</summary>
    public required long Key { get; init; }

    /// <summary>An absolute path to the file, or a URL. What plays is whatever the platform decodes.</summary>
    public required string Path { get; init; }

    public bool Playing { get; init; } = true;

    /// <summary>Whether it starts again at the end. False leaves it on its last frame.</summary>
    public bool Loop { get; init; } = true;

    /// <summary>How fast, as a multiple. Half is slow motion.</summary>
    public double Rate { get; init; } = 1;

    public double Volume { get; init; } = 1;

    /// <summary>
    /// Where to jump to, in seconds. A jump is an event and a scene is a description, so the two
    /// are reconciled by <see cref="SeekToken"/>: the renderer acts on this only when the token
    /// has moved. Saying the same seek again is not a second seek.
    /// </summary>
    public double? SeekTo { get; init; }

    public long SeekToken { get; init; }

    internal int Flags => (Playing ? 1 : 0) | (Loop ? 2 : 0);
}

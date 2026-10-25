namespace Orblit.AudioDevice;

/// <summary>How a <see cref="DeviceAudio"/> finds and reads its clips.</summary>
public sealed record DeviceAudioOptions
{
    public const long DefaultStreamAbove = 1 << 20;

    /// <summary>The folder clips are found under. Empty means a clip is a path as it stands.</summary>
    public string Directory { get; init; } = "";

    /// <summary>
    /// A file larger than this many bytes is read as it plays, which suits music. A smaller
    /// one is decoded once and shared by every voice playing it, which suits an effect.
    /// </summary>
    public long StreamAbove { get; init; } = DefaultStreamAbove;

    /// <summary>
    /// Called with the clip and the reason when a start fails, and the voice then never plays.
    /// Without it the failure is thrown from <see cref="DeviceAudio.Start"/>.
    /// </summary>
    public Action<string, string>? OnError { get; init; }
}

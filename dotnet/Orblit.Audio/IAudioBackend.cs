namespace Orblit.Audio;

/// <summary>
/// What <see cref="AudioPlayer"/> needs from a device. All the arithmetic stays
/// above this line, so a backend plays the numbers it is given and nothing else.
/// </summary>
public interface IAudioBackend
{
    /// <summary>
    /// Begins playing <paramref name="clip"/> as <paramref name="voice"/>,
    /// <paramref name="offset"/> seconds into it. A clip that is shorter than that
    /// ends at once. The id is the caller's and is never reused. A backend that
    /// cannot play the clip either throws, and the voice is dropped, or returns
    /// and answers false from <see cref="IsPlaying"/>, and the voice ends at the
    /// next advance.
    /// </summary>
    void Start(long voice, string clip, bool loop, Level level, double offset = 0);

    /// <summary>Changes how a playing voice sounds. Only called for a voice that is playing.</summary>
    void Update(long voice, Level level);

    /// <summary>Ends a voice. Only called for a voice that is playing.</summary>
    void Stop(long voice);

    /// <summary>
    /// Whether <paramref name="voice"/> is still sounding. It is true from the
    /// moment <see cref="Start"/> returns, even if the device has not confirmed
    /// the start yet, and false once the clip has played to its end. It is asked
    /// of every voice on every advance, so a backend keeps the answer cheap.
    /// </summary>
    bool IsPlaying(long voice);
}

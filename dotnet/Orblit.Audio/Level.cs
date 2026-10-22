namespace Orblit.Audio;

/// <summary>
/// How a voice sounds once the mixer has decided. <see cref="Gain"/> is linear
/// and already includes the voice, its buses and its distance.
/// <see cref="Pitch"/> is a playback rate and includes Doppler.
/// <see cref="Pan"/> runs from -1 (left) to 1 (right).
/// </summary>
public sealed class Level(double gain = 1, double pitch = 1, double pan = 0) : IEquatable<Level>
{
    public double Gain { get; } = gain;

    public double Pitch { get; } = pitch;

    public double Pan { get; } = pan;

    /// <summary>Number by number, so NaN is not equal to NaN.</summary>
    public bool Equals(Level? other) => other is not null && other.Gain == Gain && other.Pitch == Pitch && other.Pan == Pan;

    public override bool Equals(object? obj) => Equals(obj as Level);

    public override int GetHashCode() => HashCode.Combine(Gain, Pitch, Pan);

    public override string ToString() => FormattableString.Invariant($"Level(gain: {Gain}, pitch: {Pitch}, pan: {Pan})");
}

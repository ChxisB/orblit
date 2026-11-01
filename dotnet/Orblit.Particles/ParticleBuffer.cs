namespace Orblit.Particles;

/// <summary>
/// The particles alive at one moment, as flat arrays a renderer can read
/// without unpacking. Slot <c>i</c> of every array is the same particle.
/// </summary>
public sealed class ParticleBuffer
{
    public ParticleBuffer(int capacity)
    {
        Require.That(capacity >= 1, nameof(capacity), capacity, "must be 1 or more");
        Capacity = capacity;
        Positions = new float[capacity * 3];
        Sizes = new float[capacity];
        Colours = new float[capacity * 3];
        Angles = new float[capacity];
    }

    public int Capacity { get; }

    /// <summary>x, y, z per particle.</summary>
    public float[] Positions { get; }

    /// <summary>Edge length per particle.</summary>
    public float[] Sizes { get; }

    /// <summary>r, g, b per particle.</summary>
    public float[] Colours { get; }

    /// <summary>Turn about the view axis in radians, per particle.</summary>
    public float[] Angles { get; }

    /// <summary>How many slots hold a live particle.</summary>
    public int Count { get; set; }
}

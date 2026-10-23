namespace Orblit.Motion;

/// <summary>
/// How far root motion carried the character between two moments.
/// </summary>
/// <remarks>
/// <para>
/// In the character's own frame as it stood at the first moment, so a step forward is forward whichever way
/// the character faces. Taking one is moving by <see cref="Position"/> turned the way the character faces,
/// and then turning by <see cref="Rotation"/>, which is <see cref="Then"/> with the character as the first
/// step.
/// </para>
/// <para>
/// The space is the one the root's own channels are in: the model's, for a skeleton's root bone, and the
/// parent's, for an entity. A model scaled down to half size walks half as far, and scaling the step is the
/// job of whoever knows the scale.
/// </para>
/// </remarks>
public sealed class RootStep(Vector3? position = null, Quaternion? rotation = null)
{
    public Vector3 Position { get; } = position ?? Vector3.Zero();

    public Quaternion Rotation { get; } = rotation ?? Quaternion.Identity();

    /// <summary>Whether it goes nowhere and turns nothing.</summary>
    public bool IsNone => Position.Length2 < 1e-18 && 1 - Math.Abs(Rotation.W) < 1e-12;

    /// <summary>This step, and then <paramref name="next"/> from wherever this one ends.</summary>
    public RootStep Then(RootStep next) => new(Position + Turned(Rotation, next.Position), (Rotation * next.Rotation).Normalized());

    public override string ToString() => $"RootStep({Position}, {Rotation})";

    /// <summary><paramref name="step"/> taken <paramref name="times"/> times over, in as few compositions as it can.</summary>
    internal static RootStep Repeated(RootStep step, int times)
    {
        var done = new RootStep();
        var power = step;
        for (var left = times; left > 0; left >>= 1)
        {
            if ((left & 1) == 1)
            {
                done = done.Then(power);
            }

            power = power.Then(power);
        }

        return done;
    }

    /// <summary>
    /// <paramref name="vector"/> turned by <paramref name="rotation"/>.
    /// </summary>
    /// <remarks>
    /// Not by the inverse of the rotation, which is what turning a vector through the quaternion itself
    /// would give: multiplying rotations and their matrices both go the other way.
    /// </remarks>
    internal static Vector3 Turned(Quaternion rotation, Vector3 vector) => rotation.AsRotationMatrix().Transformed(vector);
}

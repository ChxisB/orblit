namespace Orblit.Asset;

/// <summary>The lowest and highest <c>orblit</c> package versions a kit was tried with.</summary>
public readonly record struct KitEngine(string From, string To)
{
    /// <summary>
    /// Whether a kit written for this range is meant for engine <paramref name="version"/>. Both ends
    /// count. A <paramref name="version"/> that is not three whole numbers fits, because the answer is a
    /// guess and a hidden kit is worse than one the person tries.
    /// </summary>
    public bool Fits(string version) =>
        !KitVersion.IsValid(version) || (KitVersion.Compare(From, version) <= 0 && KitVersion.Compare(version, To) <= 0);

    /// <summary>Throws a <see cref="FormatException"/> unless both ends are versions and the range does not run backwards.</summary>
    internal void Check()
    {
        KitVersion.Check("engine.from", From);
        KitVersion.Check("engine.to", To);
        if (KitVersion.Compare(From, To) > 0)
        {
            throw new FormatException($"The engine range runs from {From} down to {To}.");
        }
    }
}

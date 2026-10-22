using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>How one shot gives way to the next.</summary>
public enum BlendStyle
{
    /// <summary>Instant. What a film cut is, and what most transitions should be.</summary>
    Cut,

    Linear,

    /// <summary>Slow to leave, slow to arrive. The default, because it is what a camera operator's hands do.</summary>
    EaseInOut,

    /// <summary>Slow to leave, arrives at speed.</summary>
    EaseIn,

    /// <summary>Leaves at speed, slows to arrive.</summary>
    EaseOut,
}

/// <summary>A transition between two shots.</summary>
/// <param name="Style">How progress is eased.</param>
/// <param name="Duration">Seconds. Ignored for a cut.</param>
public sealed record Blend(BlendStyle Style, double Duration)
{
    public static Blend Cut() => new(BlendStyle.Cut, 0);

    /// <summary>Eased progress from a linear one.</summary>
    public double Ease(double t)
    {
        var clamped = Doubles.Clamp(t, 0.0, 1.0);
        return Style switch
        {
            BlendStyle.Cut => 1,
            BlendStyle.Linear => clamped,
            BlendStyle.EaseIn => clamped * clamped,
            BlendStyle.EaseOut => 1 - ((1 - clamped) * (1 - clamped)),
            BlendStyle.EaseInOut => 0.5 - (0.5 * Math.Cos(clamped * Math.PI)),
            _ => throw new InvalidOperationException($"Not a blend style: {Style}"),
        };
    }
}

/// <summary>
/// Which blend to use between two named cameras. A default with exceptions rather than a
/// table anyone has to fill in. The exceptions are where the direction matters: leaving a
/// cutscene is often a cut, while entering it is a slow ease.
/// </summary>
public sealed class BlendTable(Blend? defaultBlend = null)
{
    private readonly Dictionary<string, Blend> _specific = [];

    /// <summary>
    /// What any transition without a rule of its own uses. Not fixed, because a game changes
    /// it without throwing away every specific rule alongside it.
    /// </summary>
    public Blend DefaultBlend { get; set; } = defaultBlend ?? new Blend(BlendStyle.EaseInOut, 0.7);

    /// <summary>Sets the blend for one transition. A null name means "any".</summary>
    public void Set(string? from, string? to, Blend blend) =>
        _specific[$"{from ?? "*"}->{to ?? "*"}"] = blend;

    /// <summary>
    /// The blend for a transition, most specific rule first. A null name is looked up as the
    /// text "null", so a rule set with a null name is only found through its "*".
    /// </summary>
    public Blend Between(string? from, string? to)
    {
        var f = from ?? "null";
        var t = to ?? "null";
        return Find($"{f}->{t}") ?? Find($"{f}->*") ?? Find($"*->{t}") ?? DefaultBlend;
    }

    private Blend? Find(string key) => _specific.GetValueOrDefault(key);
}

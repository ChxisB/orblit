namespace Orblit.Motion;

/// <summary>A one-shot over a moving pose. Its clock is an explicit <see cref="LayerPlace"/>.</summary>
public sealed class ClipLayer
{
    private readonly ClipFrame? _reference;

    /// <exception cref="ArgumentException">
    /// When the influence is not between zero and one, or a fade is not a finite time at least zero.
    /// </exception>
    // F2: seven parameters, all but the first optional and called by name.
    public ClipLayer(
        ClipDocument clip,
        BoneMask? mask = null,
        double fadeIn = 0.1,
        double fadeOut = 0.1,
        double influence = 1,
        bool additive = false,
        ClipFrame? reference = null)
    {
        Clip = clip;
        Mask = mask;
        FadeIn = fadeIn;
        FadeOut = fadeOut;
        Influence = influence;
        Additive = additive;
        _reference = reference is null ? null : FrameLayers.Complete(reference, new ClipFrame(0));

        FrameLayers.CheckWeight(influence, nameof(influence));
        CheckFade(fadeIn, nameof(fadeIn));
        CheckFade(fadeOut, nameof(fadeOut));
    }

    public ClipDocument Clip { get; }

    public BoneMask? Mask { get; }

    public double FadeIn { get; }

    public double FadeOut { get; }

    public double Influence { get; }

    public bool Additive { get; }

    /// <summary>How much say the one-shot has at <paramref name="place"/>: in over its fade in, out over its fade out.</summary>
    public double WeightAt(LayerPlace place)
    {
        if (place.At >= Clip.Duration || Clip.Duration <= 0)
        {
            return 0;
        }

        var into = FadeIn > 0 ? place.At / FadeIn : 1.0;
        var outOf = FadeOut > 0 ? (Clip.Duration - place.At) / FadeOut : 1.0;
        return Doubles.Clamp(Math.Min(into, outOf), 0.0, 1.0) * Influence;
    }

    /// <summary><paramref name="base"/> with the one-shot laid over it, or added to it when it is additive.</summary>
    public ClipFrame SampleAt(ClipFrame @base, LayerPlace place, ClipFrame rest)
    {
        var frame = Clip.SampleAt(Doubles.Clamp(place.At, 0.0, Clip.Duration), inPlace: true);
        var weight = WeightAt(place);
        return Additive
            ? FrameLayers.Add(@base, frame, rest, reference: _reference, mask: Mask, weight: weight)
            : FrameLayers.Layer(@base, frame, rest, mask: Mask, weight: weight);
    }

    /// <summary>The place <paramref name="seconds"/> on from <paramref name="place"/>, and the marks it passed.</summary>
    public LayerStep Advance(LayerPlace place, double seconds)
    {
        var step = double.IsFinite(seconds) && seconds > 0 ? seconds : 0.0;
        var at = Math.Min(place.At + step, Clip.Duration);

        // A mark at the very start has to sound the first time through, and a range is open at its start.
        var marks = at > place.At ? Clip.MarksBetween(place.At == 0 ? -1e-9 : place.At, at).ToList() : [];
        return new LayerStep(new LayerPlace(at), marks, at >= Clip.Duration);
    }

    private static void CheckFade(double fade, string paramName)
    {
        if (!double.IsFinite(fade) || fade < 0)
        {
            throw new ArgumentException("A finite time at least zero.", paramName);
        }
    }
}

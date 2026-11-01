namespace Orblit.Scene;

/// <summary>How a sound fades between <see cref="SoundSourceComponent.Near"/> and <see cref="SoundSourceComponent.Far"/>.</summary>
public enum SoundCurve
{
    Linear,
    Inverse,
}

/// <summary>
/// An entity that makes a sound, heard from where the entity is.
/// </summary>
/// <remarks>
/// A looping source plays for as long as the entity is in the scene and shown. One that does not
/// loop plays once when it appears, and again when the game asks. The sound is full volume within
/// <see cref="Near"/> metres and silent from <see cref="Far"/>, and a source with no
/// <see cref="Clip"/> is silent, so a source can be placed before its sound is chosen.
/// <para>
/// A file whose range is not a range, with far no further than near, near not above nothing or far
/// without an end, reads as the default range instead, and a volume below nothing or without an
/// end reads as 1, since a sound with no falloff is better than a scene that will not open.
/// </para>
/// </remarks>
public sealed class SoundSourceComponent : SceneComponent
{
    // F2: seven fields of one source, called by name.
    public SoundSourceComponent(
        string? clip = null,
        string bus = "master",
        bool loop = true,
        double volume = 1,
        double near = 1,
        double far = 50,
        SoundCurve curve = SoundCurve.Inverse)
    {
        if (!(near > 0 && far > near))
        {
            throw new ArgumentException("need 0 < near < far");
        }

        Clip = clip;
        Bus = bus;
        Loop = loop;
        Volume = volume;
        Near = near;
        Far = far;
        Curve = curve;
    }

    /// <summary>Project-relative, or null for no sound yet.</summary>
    public string? Clip { get; }

    /// <summary>The mixer bus it plays on. A name the game's mixer does not have is the game's to refuse.</summary>
    public string Bus { get; }

    public bool Loop { get; }

    /// <summary>A linear gain, before the bus and the distance.</summary>
    public double Volume { get; }

    /// <summary>Metres.</summary>
    public double Near { get; }

    public double Far { get; }

    public SoundCurve Curve { get; }

    public override string Type => SceneComponents.SoundSource;

    public static SoundSourceComponent FromJson(JsonMap json)
    {
        var near = Values.Number(json, "near", 1);
        var far = Values.Number(json, "far", 50);
        var ranged = near > 0 && far > near && double.IsFinite(far);
        var volume = Values.Number(json, "volume", 1);
        return new SoundSourceComponent(
            // A name that is empty is no name: a clip of '' would be asked of the asset store, and
            // a bus of '' is one no mixer has.
            NonEmpty(Values.Text(json, "clip")),
            NonEmpty(Values.Text(json, "bus")) ?? "master",
            Values.Flag(json, "loop", true),
            double.IsFinite(volume) && volume >= 0 ? volume : 1,
            ranged ? near : 1,
            ranged ? far : 50,
            Values.Named<SoundCurve>(Values.At(json, "curve")) ?? SoundCurve.Inverse);
    }

    /// <summary>
    /// The same source with some of it changed. A clip cannot be cleared here, only set; build a new
    /// source to leave it out.
    /// </summary>
    // F2: seven fields of one source, called by name.
    public SoundSourceComponent CopyWith(
        string? clip = null,
        string? bus = null,
        bool? loop = null,
        double? volume = null,
        double? near = null,
        double? far = null,
        SoundCurve? curve = null) => new(
        clip ?? Clip,
        bus ?? Bus,
        loop ?? Loop,
        volume ?? Volume,
        near ?? Near,
        far ?? Far,
        curve ?? Curve);

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["clip"] = Clip,
        ["bus"] = Bus,
        ["loop"] = Loop,
        ["volume"] = Volume,
        ["near"] = Near,
        ["far"] = Far,
        ["curve"] = EnumNames.Of(Curve),
    });

    private static string? NonEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}

/// <summary>
/// This entity is where sound is heard from, facing down its own -Z.
/// </summary>
/// <remarks>
/// Without one anywhere in the scene the sound is heard from the camera. With one, from the first
/// entity that has it, which is how a game hears from its player rather than from a camera that sits
/// behind them.
/// </remarks>
public sealed class SoundListenerComponent : SceneComponent
{
    public override string Type => SceneComponents.SoundListener;

    public static SoundListenerComponent FromJson(JsonMap _) => new();

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>();
}

/// <summary>
/// What this entity is made of, as far as a sound is concerned: wood, stone, glass. Two surfaces
/// meeting pick the clip a touch makes.
/// </summary>
/// <remarks>
/// Not a material component, which is what the renderer draws, and not the friction and bounce of a
/// body. A crate can look like one thing, grip like another and ring like a third.
/// </remarks>
public sealed class SoundSurfaceComponent(string name = "") : SceneComponent
{
    public string Name { get; } = name;

    public override string Type => SceneComponents.SoundSurface;

    public static SoundSurfaceComponent FromJson(JsonMap json) => new(Values.Text(json, "name") ?? "");

    public override JsonMap ToJson() => new OrderedDictionary<string, object?> { ["name"] = Name };
}

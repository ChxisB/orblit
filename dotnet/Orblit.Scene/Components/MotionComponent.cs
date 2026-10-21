namespace Orblit.Scene;

/// <summary>
/// The clips an entity plays.
/// </summary>
/// <remarks>
/// Each is a <c>.oclip</c> file, by its path in the project. A clip names what it moves from where
/// this entity stands: the entity itself, the parts of it, the bones of the model it draws. So the
/// walk made for one character plays on every instance of it, and a clip is an asset rather than a
/// recording of one scene.
/// <para>
/// Which clip plays, how fast and blended with what is the game's to decide while it runs. What the
/// file says is only what this entity has to play, and which of them, if any, starts on its own.
/// </para>
/// </remarks>
public sealed class MotionComponent(IReadOnlyList<string>? clips = null, string? autoplay = null) : SceneComponent
{
    /// <summary>Project-relative, in the order somebody put them in.</summary>
    public IReadOnlyList<string> Clips { get; } = clips ?? [];

    /// <summary>
    /// The clip that starts when the scene does, or null for none. One of <see cref="Clips"/>; a
    /// path that is not is ignored rather than played, because a clip that was taken off the list
    /// was taken off for a reason.
    /// </summary>
    public string? Autoplay { get; } = autoplay;

    public override string Type => SceneComponents.Motion;

    public static MotionComponent FromJson(JsonMap json) =>
        new(Values.Texts(Values.At(json, "clips")), Values.Text(json, "autoplay"));

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["clips"] = Clips,
        ["autoplay"] = Autoplay,
    });
}

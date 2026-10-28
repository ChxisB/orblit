using Orblit.Filament;

namespace Orblit.Examples;

/// <summary>
/// One thing the engine does, shown on its own. Each example owns its own state
/// and hands back a whole scene when asked for one. Nothing is shared between
/// them but the surface they are drawn on, so what any of them needs is in its
/// own file, and that is what the code panel shows.
/// </summary>
public abstract class Example
{
    /// <summary>What it is called in the list.</summary>
    public abstract string Name { get; }

    /// <summary>One line on what it shows.</summary>
    public abstract string Blurb { get; }

    /// <summary>
    /// The heading it is listed under. Asked of every example rather than
    /// defaulted, so a new one cannot arrive under no heading or a wrong one.
    /// </summary>
    public abstract ExampleSection Section { get; }

    /// <summary>Where the camera starts, so an example opens framed on its subject.</summary>
    public virtual ViewPoint ViewPoint => new();

    /// <summary>
    /// What the device under the view can do, once the gallery has asked, or
    /// null before then. On the base class because the gallery owns the view,
    /// and an example that suits itself to its device should not need a way of
    /// its own to ask.
    /// </summary>
    public DeviceProfile? Device { get; set; }

    /// <summary>The lines that matter, as somebody would write them.</summary>
    public abstract string Code { get; }

    /// <summary>
    /// The scene as it stands, given where the camera is and how long this
    /// example has been on screen. Called every frame. The description is
    /// whole and its objects are keyed, so the renderer works out what changed.
    /// </summary>
    public abstract RenderScene Scene(RenderCamera camera, double seconds);

    /// <summary>The controls that change it, as they stand now. The host draws them.</summary>
    public abstract IReadOnlyList<ExampleSetting> Settings();

    /// <summary>
    /// <paramref name="examples"/> under the heading each belongs to. Headings
    /// come in <see cref="ExampleSection"/> order, each keeps the order it was
    /// given, and a heading with none under it is left out.
    /// </summary>
    public static IReadOnlyList<(ExampleSection Section, IReadOnlyList<Example> Examples)> BySection(
        IEnumerable<Example> examples)
    {
        var all = examples.ToList();
        return
        [
            .. Enum.GetValues<ExampleSection>()
                .Select(section => (section, (IReadOnlyList<Example>)[.. all.Where(one => one.Section == section)]))
                .Where(group => group.Item2.Count > 0),
        ];
    }
}

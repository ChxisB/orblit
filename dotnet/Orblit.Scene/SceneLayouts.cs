using Orblit.Json;

namespace Orblit.Scene;

/// <summary>
/// Which way a window is held, which is all a layout is chosen by.
/// </summary>
public enum LayoutShape
{
    Portrait,
    Landscape,
}

/// <summary>Picks the <see cref="LayoutShape"/> for a window.</summary>
public static class LayoutShapes
{
    /// <summary>
    /// Taller than wide is portrait. Anything else is landscape, a square and a window with no size
    /// yet included, so there is always one layout that applies and never a gap between two.
    /// </summary>
    public static LayoutShape Of(double width, double height) =>
        height > width ? LayoutShape.Portrait : LayoutShape.Landscape;
}

/// <summary>
/// The edits that turn a scene into how it looks held one way or the other.
/// </summary>
/// <remarks>
/// Each shape is a <see cref="SceneDiff"/> against the base scene, so a layout says only what differs
/// and everything else stays shared. A change to the base reaches both layouts.
/// <para>
/// Limited to the edits that change how something looks: a field, a whole component, and shown or
/// hidden. An object added, removed, renamed or moved in one layout would not exist in the other,
/// and code that looks things up by id would find them in one orientation and not the other.
/// </para>
/// <para>
/// An edit is addressed by id, so one aimed at an object that is not there does nothing. The
/// parts of a folded prefab instance are not in the scene as stored, which puts them in that
/// position until the instance is opened.
/// </para>
/// </remarks>
public sealed class SceneLayouts
{
    public static readonly SceneLayouts None = new(SceneDiff.None, SceneDiff.None);

    private const string PortraitKey = "portrait";
    private const string LandscapeKey = "landscape";

    public SceneLayouts(SceneDiff portrait, SceneDiff landscape)
    {
        Portrait = Allowed(portrait, nameof(portrait));
        Landscape = Allowed(landscape, nameof(landscape));
    }

    public SceneDiff Portrait { get; }

    public SceneDiff Landscape { get; }

    public bool IsEmpty => Portrait.IsEmpty && Landscape.IsEmpty;

    public SceneDiff this[LayoutShape shape] => shape == LayoutShape.Portrait ? Portrait : Landscape;

    /// <summary>Whether a layout may hold this edit.</summary>
    public static bool Allows(SceneOp op) => op is SetField or SetComponent or SetVisible;

    /// <summary>
    /// The layouts a file holds. What a layout cannot hold is left out and reported in
    /// <paramref name="problems"/>, so a hand-edited file loses the one edit and not the scene.
    /// </summary>
    public static SceneLayouts FromJson(object? raw, List<string> problems)
    {
        if (raw is not JsonMap map)
        {
            if (raw is not null)
            {
                problems.Add("The layouts are not an object, and were left out.");
            }

            return None;
        }

        return new SceneLayouts(
            ShapeFrom(map, PortraitKey, problems),
            ShapeFrom(map, LandscapeKey, problems));
    }

    /// <summary>The same layouts with one shape's edits replaced.</summary>
    public SceneLayouts With(LayoutShape shape, SceneDiff diff) =>
        shape == LayoutShape.Portrait ? new SceneLayouts(diff, Landscape) : new SceneLayouts(Portrait, diff);

    /// <summary>
    /// How many of a shape's edits would find nothing to change in <paramref name="document"/>.
    /// <para>
    /// Counted against a running copy, because an edit may add the component a later one changes.
    /// </para>
    /// </summary>
    public int CountMissed(SceneDocument document, LayoutShape shape)
    {
        var running = document;
        var missed = 0;
        foreach (var op in this[shape].Operations)
        {
            if (!Lands(op, running))
            {
                missed++;
            }

            running = op.ApplyTo(running);
        }

        return missed;
    }

    /// <summary>Only the shapes that say something, so a scene with none writes no key.</summary>
    public JsonMap ToJson()
    {
        var json = new OrderedDictionary<string, object?>();
        if (Portrait.IsNotEmpty)
        {
            json[PortraitKey] = Portrait.ToJson();
        }

        if (Landscape.IsNotEmpty)
        {
            json[LandscapeKey] = Landscape.ToJson();
        }

        return json;
    }

    private static SceneDiff Allowed(SceneDiff diff, string name)
    {
        var refused = diff.Operations.FirstOrDefault(op => !Allows(op));
        return refused is null
            ? diff
            : throw new ArgumentException(
                $"A layout can set a field, set a component, or show and hide an object, and this has a \"{NameOf(refused)}\" edit.",
                name);
    }

    private static SceneDiff ShapeFrom(JsonMap map, string key, List<string> problems)
    {
        var raw = Values.At(map, key);
        var read = SceneDiff.FromJson(raw);
        if (raw is not null and not JsonList)
        {
            problems.Add($"The {key} layout is not a list of edits, and was left out.");
        }
        else if (raw is JsonList list && list.Count > read.Operations.Count)
        {
            problems.Add($"{list.Count - read.Operations.Count} of the {key} layout's edits could not be read, and were left out.");
        }

        foreach (var op in read.Operations.Where(op => !Allows(op)))
        {
            problems.Add($"The {key} layout has a \"{NameOf(op)}\" edit, which a layout cannot hold, and it was left out.");
        }

        return new SceneDiff([.. read.Operations.Where(Allows)]);
    }

    private static string NameOf(SceneOp op) => Values.Text(op.ToJson(), "op") ?? string.Empty;

    private static bool Lands(SceneOp op, SceneDocument document) => op switch
    {
        SetField field => document[field.Id]?.Has(field.Type) == true,
        SetComponent component => document.Contains(component.Id),
        SetVisible visible => document.Contains(visible.Id),
        _ => false,
    };
}

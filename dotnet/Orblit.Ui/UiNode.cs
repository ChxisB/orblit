using System.Collections.ObjectModel;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Ui;

/// <summary>
/// One element of an interface, as script describes it. A description rather than a control: script
/// says what it wants and the host builds it, so one crossing carries a whole change.
/// </summary>
public sealed record UiNode
{
    /// <summary>What to build, such as <c>column</c>, <c>text</c> or <c>button</c>, or a name a host has registered.</summary>
    public required string Type { get; init; }

    /// <summary>A utility class list, applied first.</summary>
    public string Classes { get; init; } = "";

    /// <summary>CSS declarations, applied over the classes.</summary>
    public string Css { get; init; } = "";

    /// <summary>The words, for the elements that have any.</summary>
    public string? Text { get; init; }

    /// <summary>Everything else the element needs: an image source, a placeholder, the name of a callback.</summary>
    public IReadOnlyDictionary<string, object?> Props { get; init; } = ReadOnlyDictionary<string, object?>.Empty;

    public IReadOnlyList<UiNode> Children { get; init; } = [];

    /// <summary>What this element is across rebuilds, so a reordered list keeps state on the right control.</summary>
    public string? Key { get; init; }

    /// <summary>
    /// Reads a description sent as JSON. It forgives on purpose: script is reloaded on save, so a
    /// half-finished tree is the normal case. An element with no type becomes an empty box.
    /// </summary>
    public static UiNode FromJson(object? value) => value switch
    {
        string text => new UiNode { Type = "text", Text = text },
        IReadOnlyDictionary<string, object?> map => FromMap(map),
        _ => new UiNode { Type = "box" },
    };

    /// <summary>Reads a whole description from the text script returned.</summary>
    public static UiNode Decode(string source)
    {
        try
        {
            return FromJson(JsonText.Decode(source));
        }
        catch (JsonFormatException error)
        {
            return new UiNode { Type = "text", Text = $"The interface could not be read: {error.Reason}" };
        }
    }

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?> { ["type"] = Type };
        AddIf(json, Classes.Length > 0, "class", Classes);
        AddIf(json, Css.Length > 0, "style", Css);
        AddIf(json, Text is not null, "text", Text);
        AddIf(json, Props.Count > 0, "props", Props);
        AddIf(json, Key is not null, "key", Key);
        AddIf(json, Children.Count > 0, "children", Children.Select(child => (object?)child.ToJson()).ToList());
        return json;
    }

    // A path is a list of child indices, not a pointer: after an edit the old nodes are still there
    // and a pointer would name one of them.

    /// <summary>The node at <paramref name="path"/>, or null if the path leads nowhere. An empty path is this node.</summary>
    public UiNode? At(IReadOnlyList<int> path)
    {
        var here = this;
        foreach (var index in path)
        {
            if (index < 0 || index >= here.Children.Count)
            {
                return null;
            }

            here = here.Children[index];
        }

        return here;
    }

    /// <summary>
    /// This tree with the node at <paramref name="path"/> replaced. Every ancestor is rebuilt and every
    /// sibling is shared.
    /// </summary>
    public UiNode ReplaceAt(IReadOnlyList<int> path, UiNode replacement)
    {
        if (path.Count == 0)
        {
            return replacement;
        }

        var index = path[0];
        if (index < 0 || index >= Children.Count)
        {
            return this;
        }

        var next = Children.ToList();
        next[index] = Children[index].ReplaceAt([.. path.Skip(1)], replacement);
        return this with { Children = next };
    }

    /// <summary>
    /// This tree with a child inserted under <paramref name="parent"/>. An index past the end appends,
    /// which is what "add to this" means when nothing inside it was selected.
    /// </summary>
    public UiNode InsertAt(IReadOnlyList<int> parent, int index, UiNode child)
    {
        if (At(parent) is not { } into)
        {
            return this;
        }

        var next = into.Children.ToList();
        next.Insert(Math.Clamp(index, 0, next.Count), child);
        return ReplaceAt(parent, into with { Children = next });
    }

    /// <summary>This tree with the node at <paramref name="path"/> taken out. The root cannot be removed.</summary>
    public UiNode RemoveAt(IReadOnlyList<int> path)
    {
        if (SiblingsOf(path) is not var (parentPath, parent, index))
        {
            return this;
        }

        var next = parent.Children.ToList();
        next.RemoveAt(index);
        return ReplaceAt(parentPath, parent with { Children = next });
    }

    /// <summary>This tree with the node at <paramref name="path"/> moved among its siblings.</summary>
    public UiNode MoveAt(IReadOnlyList<int> path, int to)
    {
        if (SiblingsOf(path) is not var (parentPath, parent, index))
        {
            return this;
        }

        var next = parent.Children.ToList();
        var moving = next[index];
        next.RemoveAt(index);
        next.Insert(Math.Clamp(to, 0, next.Count), moving);
        return ReplaceAt(parentPath, parent with { Children = next });
    }

    /// <summary>
    /// Everything under this node, deepest last, with the path to each. The last match in this order is
    /// the one drawn on top.
    /// </summary>
    public IEnumerable<(UiNode Node, IReadOnlyList<int> Path)> Walk() => Walk([]);

    /// <summary>Where this element was put, read from the CSS because the CSS is what positions it.</summary>
    public (double Left, double Top)? Placed
    {
        get
        {
            var left = LengthOf("left");
            var top = LengthOf("top");
            return left is null && top is null ? null : (left ?? 0, top ?? 0);
        }
    }

    /// <summary>This element with no place of its own, so a parent that lays out its children is not given a position it cannot use.</summary>
    public UiNode Unplaced => this with { Css = string.Join("; ", Without("left", "top", "right", "bottom")) };

    /// <summary>
    /// This element moved to a place, keeping the rest of its style. Whole pixels by default, so the file
    /// does not change on every nudge. Pass false while dragging: rounding each frame loses a fraction
    /// of a pixel each time and a slow drag falls behind the pointer.
    /// </summary>
    public UiNode PlaceAt(double left, double top, bool round = true) => this with
    {
        Css = string.Join("; ", [.. Without("left", "top"), $"left: {Pixels(left, round)}px", $"top: {Pixels(top, round)}px"]),
    };

    /// <summary>The key of this element's words in a string table, when they come from one rather than from <see cref="Text"/>.</summary>
    public string? TextKey => NonEmpty("textKey");

    /// <summary>The arguments the words are filled in with: a <c>{name}</c> in the text, and a whole <c>count</c> to choose the plural.</summary>
    public IReadOnlyDictionary<string, object?> TextArgs =>
        Props.GetValueOrDefault("textArgs") as IReadOnlyDictionary<string, object?> ?? ReadOnlyDictionary<string, object?>.Empty;

    /// <summary>Every key this tree asks a string table for.</summary>
    public IReadOnlySet<string> TextKeys => Walk().Select(one => one.Node.TextKey).OfType<string>().ToHashSet();

    /// <summary>
    /// The name of the callback for an event, if script gave one. A function cannot be serialised, and a
    /// name is what the host calls back with when the button is pressed.
    /// </summary>
    public string? HandlerFor(string eventName) => NonEmpty(eventName);

    private static UiNode FromMap(IReadOnlyDictionary<string, object?> map) => new()
    {
        Type = Str(map, "type") ?? "box",
        Classes = Str(map, "class") ?? Str(map, "className") ?? "",
        Css = Str(map, "style") ?? "",
        Text = Str(map, "text"),
        Props = map.GetValueOrDefault("props") as IReadOnlyDictionary<string, object?> ?? ReadOnlyDictionary<string, object?>.Empty,
        Key = Str(map, "key"),
        Children = map.GetValueOrDefault("children") is IReadOnlyList<object?> children ? [.. children.Select(FromJson)] : [],
    };

    private static string? Str(IReadOnlyDictionary<string, object?> map, string key) => map.GetValueOrDefault(key) as string;

    private static void AddIf(OrderedDictionary<string, object?> json, bool when, string key, object? value)
    {
        if (when)
        {
            json[key] = value;
        }
    }

    private (IReadOnlyList<int> Parent, UiNode Node, int Index)? SiblingsOf(IReadOnlyList<int> path)
    {
        if (path.Count == 0)
        {
            return null;
        }

        IReadOnlyList<int> parentPath = [.. path.Take(path.Count - 1)];
        var index = path[^1];
        return At(parentPath) is { } parent && index >= 0 && index < parent.Children.Count
            ? (parentPath, parent, index)
            : null;
    }

    private IEnumerable<(UiNode Node, IReadOnlyList<int> Path)> Walk(IReadOnlyList<int> path)
    {
        yield return (this, path);
        for (var i = 0; i < Children.Count; i++)
        {
            foreach (var below in Children[i].Walk([.. path, i]))
            {
                yield return below;
            }
        }
    }

    private IEnumerable<string> Without(params string[] properties) =>
        Css.Split(';')
            .Select(declaration => PlainText.Trim(declaration))
            .Where(declaration => declaration.Length > 0 && !properties.Contains(PropertyOf(declaration)));

    private static string PropertyOf(string declaration) => PlainText.ToLower(PlainText.Trim(declaration.Split(':')[0]));

    // Two decimals at most when a length is not whole, so a position reads well instead of being exact to seventeen digits.
    private static string Pixels(double value, bool round) =>
        round || value == Math.Round(value, MidpointRounding.AwayFromZero)
            ? Doubles.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : Doubles.ToStringAsFixed(value, 2).TrimEnd('0').TrimEnd('.');

    private double? LengthOf(string property)
    {
        foreach (var declaration in Css.Split(';'))
        {
            var parts = declaration.Split(':');
            if (parts.Length < 2 || PropertyOf(declaration) != property)
            {
                continue;
            }

            var value = PlainText.Trim(parts[1]);
            return PlainText.ParseDouble(value.EndsWith("px", StringComparison.Ordinal) ? value[..^2] : value);
        }

        return null;
    }

    private string? NonEmpty(string prop) => Props.GetValueOrDefault(prop) is string { Length: > 0 } value ? value : null;
}

using Orblit.Json;

namespace Orblit.Ui;

/// <summary>
/// An interface saved as a file: the canvas plus the tree on it. It is written as the same JSON a
/// script sends, so a tree from a script can be saved as a document and a document can be handed to
/// a script.
/// </summary>
public sealed record UiDocument
{
    /// <summary>The <c>kind</c> a file carries.</summary>
    public const string Marker = "orblit.ui";

    public const int FormatVersion = 1;

    public const string Extension = ".oui";

    public UiCanvas Canvas { get; init; } = new();

    public UiNode Root { get; init; } = new() { Type = "box" };

    /// <summary>What it is called, which need not match the file name.</summary>
    public string Name { get; init; } = "Interface";

    /// <summary>
    /// A new one: a free-positioned root with something visible on it. An empty canvas gives nothing
    /// to drag, select or learn from. The root is a stack, so what goes on it stays where it was put.
    /// It is responsive from the first frame, because a canvas that starts fixed and is made
    /// responsive later has every position already assuming a width.
    /// </summary>
    public static UiDocument Blank(string name) => new()
    {
        Name = name,
        Canvas = new UiCanvas { Fit = CanvasFit.Responsive },
        Root = new UiNode
        {
            Type = "stack",
            Classes = "w-full h-full",
            Children =
            [
                new UiNode
                {
                    Type = "text",
                    Classes = "text-3xl font-bold text-white",
                    Css = "left: 96px; top: 84px",
                    Text = "Title",
                },
                new UiNode
                {
                    Type = "text",
                    Classes = "text-base text-slate-300",
                    Css = "left: 96px; top: 136px",
                    Text = "Say what this screen is for.",
                },
            ],
        },
    };

    public string ToText() => JsonText.Encode(
        new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = FormatVersion,
            ["name"] = Name,
            ["canvas"] = Canvas.ToJson(),
            ["root"] = Root.ToJson(),
        },
        "  ") + "\n";

    /// <summary>
    /// Reads one, or null if it is not an interface. It is null and not a throw, because a file may
    /// come from a newer build or have been edited by hand, and a browser that throws on one bad file
    /// shows nothing at all.
    /// </summary>
    public static UiDocument? Read(string text)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (JsonFormatException)
        {
            return null;
        }

        if (parsed is not IReadOnlyDictionary<string, object?> map || !Marker.Equals(map.GetValueOrDefault("kind")))
        {
            return null;
        }

        return new UiDocument
        {
            Name = map.GetValueOrDefault("name") as string ?? "Interface",
            Canvas = UiCanvas.FromJson(map.GetValueOrDefault("canvas")),
            Root = UiNode.FromJson(map.GetValueOrDefault("root")),
        };
    }
}

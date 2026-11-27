using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>What Search looks through: everything, the project's scenes alone, or the menu's actions alone.</summary>
internal enum SearchScope { All, Scenes, Actions }

/// <summary>
/// Search: one box over the project's scenes and scripts, the open scene's things, the menu's actions, the settings
/// and the guide's pages. Empty, it shows the recent scenes and a few things to try. A query that starts with &gt;
/// looks at the actions alone.
/// </summary>
internal sealed class Search
{
    internal const string Recent = "Recent";
    internal const string Try = "Try";
    internal const string Scenes = "Scenes";
    internal const string Scripts = "Scripts";
    internal const string Things = "Things";
    internal const string Actions = "Actions";
    internal const string Settings = "Settings";
    internal const string Help = "Help";
    internal const string InProject = "Scenes in this project";

    public Search()
    {
        var box = new TextBox { PlaceholderText = "Search files, things, settings and actions", FontSize = 15, Padding = new Thickness(0), MinHeight = 0, VerticalAlignment = VerticalAlignment.Center, Classes = { EditorTheme.Bare } };
        var icon = new EditorIcon("Lens") { Width = 17, Height = 17, Margin = new Thickness(0, 0, Gaps.Item, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, EditorBrushes.TextDim);
        ScenesChip = new Border
        {
            IsVisible = false,
            Padding = new Thickness(Gaps.Item, Gaps.Joined),
            Margin = new Thickness(0, 0, Gaps.Item, 0),
            CornerRadius = new CornerRadius(4),
            Background = EditorBrushes.AccentSoft,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = Scenes, FontSize = 12, Foreground = EditorBrushes.Text },
        };
        var escape = Parts.Kbd("esc");
        escape.Margin = new Thickness(Gaps.Item, 0, 0, 0);
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(ScenesChip, Dock.Left);
        DockPanel.SetDock(escape, Dock.Right);
        var top = new Border
        {
            Height = 48,
            Padding = new Thickness(Gaps.Dialog, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new DockPanel { Children = { icon, ScenesChip, escape, box } },
        };
        var footer = new Border
        {
            Padding = new Thickness(Gaps.Dialog, Gaps.Item),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Group, Children = { Parts.KeyHint("move", "↑", "↓"), Parts.KeyHint("use it", "↵"), Parts.KeyHint("actions only", ">") } },
        };
        Picker = new Picker("Search", box, top, footer);
    }

    internal Picker Picker { get; }

    /// <summary>The chip before the box that says Search looks at scenes alone.</summary>
    internal Border ScenesChip { get; }

    /// <summary>Shows the card over these rows. The Actions scope starts the box with &gt;, so a backspace widens it.</summary>
    public void Open(IReadOnlyList<PickerRow> rows, SearchScope scope)
    {
        ScenesChip.IsVisible = scope == SearchScope.Scenes;
        Picker.Open(Finder(rows, scope), scope == SearchScope.Actions ? ">" : "");
    }

    /// <summary>
    /// The rows for a query. Empty, it gives Recent and Try. Typed, it gives the Scenes, Scripts, Things, Actions,
    /// Settings and Help that match. A leading &gt; keeps the actions alone. The Scenes scope keeps the scenes alone, under one heading.
    /// </summary>
    internal static Func<string, IReadOnlyList<PickerRow>> Finder(IReadOnlyList<PickerRow> rows, SearchScope scope) => query =>
    {
        var text = query.TrimStart();
        if (scope == SearchScope.Scenes)
        {
            return Picker.Matching(rows.Where(row => row.Group == Scenes).Select(row => row with { Group = InProject }), text);
        }

        return text.StartsWith('>') ? Picker.Matching(rows.Where(row => row.Group == Actions), text[1..])
            : text == "" ? [.. rows.Where(row => row.Group is Recent or Try)]
            : Picker.Matching(rows.Where(row => row.Group is not (Recent or Try)), text);
    };

    /// <summary>
    /// Every item of the bar that runs and is on, so a new menu item joins Search with no code here. Each notes what
    /// it does and, inside a submenu, says the submenu's name. Open recent is left out, since its scenes show as
    /// scenes.
    /// </summary>
    internal static IEnumerable<PickerRow> ActionsOf(NativeMenu bar) =>
        bar.Items.OfType<NativeMenuItem>().SelectMany(top => top.Menu is { } menu ? Leaves(menu, "") : []);

    /// <summary>A row for a scene or script file, named without its folder or extension, noting its path.</summary>
    internal static PickerRow FileRow(string group, string path, string kind, Action<string> open)
    {
        var file = FileTree.KindOf(path);
        return new PickerRow(group, ScenesPanel.IconOf(file), Path.GetFileNameWithoutExtension(path), () => open(path)) { Tint = ScenesPanel.TintOf(file), Note = path, Kind = kind };
    }

    /// <summary>
    /// What a file is, in words: "3D scene", "2D scene" or "Scene" by <paramref name="sceneKind"/>, the kind of
    /// script, or what an imported file holds. An imported file of a kind not named here has no words.
    /// <paramref name="sceneKind"/> reads the file, so only a scene calls it.
    /// </summary>
    internal static string KindText(string path, Func<string, string> sceneKind) => FileTree.KindOf(path) switch
    {
        FileKind.Script => "C# script",
        FileKind.Graph => "Graph script",
        FileKind.Imported => ImportedText(Path.GetExtension(path).ToLowerInvariant()),
        _ => sceneKind(path) is { Length: > 0 } kind ? $"{kind} scene" : "Scene",
    };

    static string ImportedText(string extension) => extension switch
    {
        ".png" or ".jpg" or ".jpeg" or ".webp" or ".svg" or ".bmp" or ".tga" or ".exr" or ".hdr" => "Picture",
        ".glb" or ".gltf" or ".fbx" or ".obj" or ".blend" or ".dae" => "Model",
        ".wav" or ".ogg" or ".mp3" => "Sound",
        ".ttf" or ".otf" or ".woff" or ".woff2" => "Font",
        _ => "",
    };

    /// <summary>The things in the open scene, parents first. Each notes the path to its parent and gives its class as the engine's name.</summary>
    internal static IEnumerable<PickerRow> ThingsOf(OutlineNode root, Action<nint> select)
    {
        IEnumerable<PickerRow> Walk(OutlineNode node, string note) =>
            node.Children.SelectMany(child => Walk(child, note == "" ? node.Name : $"{note}/{node.Name}"))
                .Prepend(new PickerRow(Things, EditorIcons.ForClass(node.ClassName), node.Name, () => select(node.Handle)) { Tint = EditorBrushes.TreeIcon, Note = note, Engine = node.ClassName });

        return Walk(root, "");
    }

    // The guide's pages on the site, with the minutes each takes to read at about 250 words a minute.
    static readonly (string Title, string Path, string Line, int Minutes)[] s_guide =
    [
        ("What Orblit is", "start/what-orblit-is/", "A game engine and an editor in one application", 3),
        ("Installing", "start/installing/", "Download Orblit, unzip it and open the editor", 2),
        ("Your first project", "start/your-first-project/", "Make a project with a spinning cube and run it", 2),
        ("Your first graph script", "start/your-first-graph-script/", "Joins steps with wires to print and send a signal", 5),
        ("Export a game", "start/export-a-game/", "Build your game as a program for Linux, Windows or macOS", 3),
        ("Platform support", "reference/platform-support/", "Where the editor runs and where it exports to", 2),
    ];

    /// <summary>The guide's pages, each opening in the browser through these links.</summary>
    internal static IEnumerable<PickerRow> HelpOf(ILinkOpener links) =>
        s_guide.Select(page => new PickerRow(Help, "Book", page.Title, () => links.Open(new Uri(HelpMenu.Docs, page.Path)))
        {
            Note = page.Line,
            Kind = $"The guide · {page.Minutes} minutes",
        });

    static IEnumerable<PickerRow> Leaves(NativeMenu menu, string kind) =>
        menu.Items.OfType<NativeMenuItem>().SelectMany(item => item switch
        {
            { Header: EditorMenu.OpenRecent } => [],
            { Menu: { } under } => Leaves(under, item.Header ?? ""),
            { Command: { } command, IsEnabled: true } => [new PickerRow(Actions, "Terminal", item.Header ?? "", () => command.Execute(item.CommandParameter))
            {
                Note = EditorMenu.NoteOf(item) ?? "",
                Engine = EditorMenu.EngineOf(item) ?? "",
                Kind = kind,
                Key = item.Gesture is { } gesture ? Parts.KeyText(gesture) : "",
            }],
            _ => [],
        });
}

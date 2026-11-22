using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The command palette: the menu's commands with their keys, the project's scene files and the open scene's
/// entities in one list. A query that starts with &gt; shows the commands alone.
/// </summary>
internal static class CommandPalette
{
    internal const string Commands = "Commands";
    internal const string Files = "Files";

    internal static Picker Build()
    {
        var search = new TextBox { PlaceholderText = "Search", FontSize = 15, Padding = new Thickness(0), MinHeight = 0, VerticalAlignment = VerticalAlignment.Center, Classes = { EditorTheme.Bare } };
        var icon = new EditorIcon("Lens") { Width = 17, Height = 17, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        TextElement.SetForeground(icon, EditorBrushes.TextDim);
        var escape = Parts.Kbd("esc");
        escape.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(icon, Dock.Left);
        DockPanel.SetDock(escape, Dock.Right);
        var top = new Border
        {
            Height = 48,
            Padding = new Thickness(14, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new DockPanel { Children = { icon, escape, search } },
        };
        var footer = new Border
        {
            Padding = new Thickness(14, 8),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { Hint("move", "↑", "↓"), Hint("run", "↵"), Hint("commands only", ">") } },
        };
        return new Picker("Command palette", search, top, footer);
    }

    /// <summary>Finds the rows whose name holds the query. A leading &gt; keeps the commands alone.</summary>
    internal static Func<string, IReadOnlyList<PickerRow>> Finder(IReadOnlyList<PickerRow> rows) => query =>
    {
        var text = query.TrimStart();
        return text.StartsWith('>') ? Picker.Matching(rows.Where(row => row.Group == Commands), text[1..]) : Picker.Matching(rows, text);
    };

    /// <summary>
    /// Every item of the bar that runs and is on, so a new menu item joins the palette with no code here. An item
    /// in a submenu notes the submenu's name.
    /// </summary>
    internal static IEnumerable<PickerRow> CommandsOf(NativeMenu bar) =>
        bar.Items.OfType<NativeMenuItem>().SelectMany(top => top.Menu is { } menu ? Leaves(menu, "") : []);

    /// <summary>The scene files, each noting its folder. Folders end in a slash and are left out.</summary>
    internal static IEnumerable<PickerRow> FilesOf(IEnumerable<string> scenes, Action<string> open) =>
        scenes.Where(path => !path.EndsWith('/')).Select(path => new PickerRow(Files, "SceneFile", Path.GetFileName(path), () => open(path))
        {
            Tint = EditorBrushes.SceneIcon,
            Note = path[..(path.LastIndexOf('/') + 1)],
        });

    /// <summary>The entities of the open scene, parents first, each noting the path to its parent.</summary>
    internal static IEnumerable<PickerRow> EntitiesOf(OutlineNode root, string sceneFile, Action<nint> select)
    {
        var group = sceneFile == "" ? "Entities" : $"Entities in {Path.GetFileName(sceneFile)}";

        IEnumerable<PickerRow> Walk(OutlineNode node, string note) =>
            node.Children.SelectMany(child => Walk(child, note == "" ? node.Name : $"{note}/{node.Name}"))
                .Prepend(new PickerRow(group, EditorIcons.ForClass(node.ClassName), node.Name, () => select(node.Handle)) { Tint = EditorBrushes.TreeIcon, Note = note });

        return Walk(root, "");
    }

    static IEnumerable<PickerRow> Leaves(NativeMenu menu, string note) =>
        menu.Items.OfType<NativeMenuItem>().SelectMany(item => item switch
        {
            { Menu: { } under } => Leaves(under, item.Header ?? ""),
            { Command: { } command, IsEnabled: true } => [new PickerRow(Commands, "Terminal", item.Header ?? "", () => command.Execute(item.CommandParameter))
            {
                Note = note,
                Key = item.Gesture is { } gesture ? Parts.KeyText(gesture) : "",
                Soon = EditorTheme.IsSoon(item),
            }],
            _ => [],
        });

    static StackPanel Hint(string what, params string[] keys)
    {
        var hint = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        hint.Children.AddRange(keys.Select(Parts.Kbd));
        hint.Children.Add(new TextBlock { Text = what, FontSize = 11, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center });
        return hint;
    }
}

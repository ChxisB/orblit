namespace Orblit.Editor.App.Theme;

/// <summary>The editor's icons by file name, generated from <c>engine/editor/icons</c>.</summary>
internal static partial class EditorIcons
{
    public static bool Has(string name) => s_icons.ContainsKey(name);

    /// <summary>The icon a class draws. A class with no icon of its own draws the one of Entity.</summary>
    public static string ForClass(string className) => Has(className) ? className : "Entity";

    public static IconDrawing Get(string name) =>
        s_icons.TryGetValue(name, out var drawing) ? drawing : throw new ArgumentException($"No icon is named {name}.", nameof(name));
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A bottom panel that holds a note where its tool will go: the island and the titled strip of the Output panel,
/// with the note in the middle.
/// </summary>
internal sealed class NotePanel : Border
{
    public NotePanel(string title, Control note)
    {
        Height = OutputPanel.PanelHeight;
        Classes.Add(EditorTheme.Island);
        var strip = new Border
        {
            Height = OutputPanel.StripHeight,
            Padding = new Thickness(12, 0, 6, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
        };
        DockPanel.SetDock(strip, Dock.Top);
        note.VerticalAlignment = VerticalAlignment.Center;
        Note = note;
        Child = new DockPanel { Children = { strip, note } };
    }

    internal Control Note { get; }

    /// <summary>The panel of a tool that is not built: its mark, its name with Soon, and a line on what it will do.</summary>
    public static NotePanel Soon(string name, string icon, string line)
    {
        var note = Parts.EmptyState(icon, name, line);
        var title = note.Children[1];
        note.Children.RemoveAt(1);
        var soon = new Border
        {
            Padding = new Thickness(6, 1),
            CornerRadius = new CornerRadius(999),
            Background = EditorBrushes.WarnSoft,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "Soon", FontSize = 10.5, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.Warn },
        };
        note.Children.Insert(1, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, Children = { title, soon } });
        return new NotePanel(name, note);
    }
}

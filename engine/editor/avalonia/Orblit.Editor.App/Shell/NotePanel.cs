using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A bottom panel that holds a note: the island and the titled strip of the Output panel, with the note in the
/// middle.
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
            Padding = new Thickness(Gaps.Group, 0, Gaps.Item, 0),
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
}

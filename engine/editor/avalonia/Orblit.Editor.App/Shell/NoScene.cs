using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// What the middle shows while no scene is open: what a scene is, the two ways to get one, and the scenes opened
/// this session to open again. The window opens the scene, so a row only asks.
/// </summary>
internal sealed class NoScene : Border
{
    internal const string Title = "No scene is open";

    readonly StackPanel _rows = new() { Spacing = Gaps.Joined };
    readonly StackPanel _again;

    public NoScene(KeyModifiers command)
    {
        Background = EditorBrushes.Panel;
        NewButton = Keyed("New scene", new KeyGesture(Key.N, command), EditorTheme.Primary, "Pick what it is for and where it goes.");
        FindButton = Keyed("Find a scene", new KeyGesture(Key.O, command | KeyModifiers.Shift), EditorTheme.Ghost, "Type part of a name to open it.");
        var mark = new EditorIcon("SceneFile") { Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Center };
        TextElement.SetForeground(mark, EditorBrushes.Disabled);
        _again = new StackPanel { Spacing = Gaps.Tight, Margin = new Thickness(0, Gaps.Group, 0, 0), Children = { Heading("Open again"), _rows } };
        Child = new StackPanel
        {
            Width = 360,
            Spacing = Gaps.Group,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                mark,
                new TextBlock { Text = Title, FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock
                {
                    Text = "A scene is a level, a menu or a part you reuse. Open one, or make a new one.",
                    Foreground = EditorBrushes.TextSoft,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Item, HorizontalAlignment = HorizontalAlignment.Center, Children = { NewButton, FindButton } },
                _again,
            },
        };
    }

    /// <summary>Raised with the path of the scene whose row was pressed. Nothing has opened yet.</summary>
    public event Action<string>? OpenRequested;

    internal Button NewButton { get; }

    internal Button FindButton { get; }

    /// <summary>The rows under Open again, newest first.</summary>
    internal IEnumerable<Button> Rows => _rows.Children.OfType<Button>();

    /// <summary>Lists these scenes under Open again, each with when it was opened. None hides the list.</summary>
    public void ShowRecent(IEnumerable<(string Path, string When)> scenes)
    {
        _rows.Children.Clear();
        _rows.Children.AddRange(scenes.Select(scene => Row(scene.Path, scene.When)));
        _again.IsVisible = _rows.Children.Count > 0;
    }

    /// <summary>When a scene was opened, in words, such as "opened 10 minutes ago" or "opened yesterday".</summary>
    internal static string Opened(DateTimeOffset then, DateTimeOffset now)
    {
        var gap = now - then;
        return gap.TotalMinutes < 1 ? "opened just now"
            : gap.TotalHours < 1 ? $"opened {Count((int)gap.TotalMinutes, "minute")} ago"
            : then.Date == now.Date ? $"opened {Count((int)gap.TotalHours, "hour")} ago"
            : then.Date == now.Date.AddDays(-1) ? "opened yesterday"
            : $"opened {then.ToString("d MMMM", CultureInfo.InvariantCulture)}";
    }

    static string Count(int count, string unit) => count == 1 ? $"1 {unit}" : $"{count} {unit}s";

    Button Row(string path, string when)
    {
        var name = new TextBlock { Text = Path.GetFileNameWithoutExtension(path), VerticalAlignment = VerticalAlignment.Center };
        var stamp = new TextBlock { Text = when, FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(stamp, Dock.Right);
        var row = new Button
        {
            Classes = { EditorTheme.TextButton, EditorTheme.Ghost },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new DockPanel { Children = { stamp, Parts.RowIcon("SceneFile", EditorBrushes.Accent), name } },
        };
        ToolTip.SetTip(row, path.Replace("res://", ""));
        row.Click += (_, _) => OpenRequested?.Invoke(path);
        return row;
    }

    static Button Keyed(string word, KeyGesture key, string look, string line)
    {
        var card = new HoverCard(word, line, Parts.KeyText(key));
        return card.On(new Button { Classes = { EditorTheme.TextButton, look }, Content = Parts.Keyed(word, card.Key!) });
    }

    static TextBlock Heading(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 10.5,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 0.6,
        Foreground = EditorBrushes.Unit,
    };
}

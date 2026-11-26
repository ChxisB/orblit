using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What this graph does: a card over the canvas that reads the graph back as sentences, one block for each start.
/// It reads the graph again on each change, never while it draws. A step's number picks that step.
/// </summary>
internal sealed class SentencesCard : Border
{
    /// <summary>What the card says while the graph has no start.</summary>
    internal const string NoStart = "Add a start to see what this graph does.";

    /// <summary>What the card says under a start that runs no step yet.</summary>
    internal const string NoStep = "Nothing yet. Add a step after the start.";

    const double Indent = 16;

    readonly IScriptDeclarations _script;
    readonly StackPanel _body = new() { Spacing = Gaps.Tight };
    HashSet<string>? _starts;

    public SentencesCard(IScriptDeclarations script)
    {
        _script = script;
        Background = EditorBrushes.Popover;
        BorderBrush = EditorBrushes.Line;
        BorderThickness = new Thickness(1);
        CornerRadius = EditorTheme.PopoverCorners;
        Padding = new Thickness(Gaps.Group);
        MaxWidth = 420;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(Gaps.Group);
        var title = new TextBlock { Text = "What this graph does", FontWeight = FontWeight.Bold };
        var updates = new TextBlock { Text = "Updates as you edit", FontSize = 11, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };
        var header = new WrapPanel { ItemSpacing = Gaps.Dialog, Margin = new Thickness(0, 0, 0, Gaps.Item), Children = { title, updates } };
        Child = new DockPanel { Children = { Top(header), new ScrollViewer { MaxHeight = 180, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = _body } } };
        _script.Changed += Read;
        Read();
    }

    /// <summary>Raised with a function and a step of it when a person picks the step's number.</summary>
    public event Action<string, int>? StepPicked;

    /// <summary>Each line on show as text, a start's sentence on a line of its own.</summary>
    internal IEnumerable<string> Lines => _body.Children.Select(line => line.Tag as string ?? "");

    /// <summary>The number button of a step of a function, or null when the card shows none.</summary>
    internal Button? NumberOf(string function, int step) =>
        _body.Children.OfType<StackPanel>().SelectMany(line => line.Children.OfType<Button>()).FirstOrDefault(button => Equals(button.Tag, (function, step)));

    static Control Top(Control control)
    {
        DockPanel.SetDock(control, Dock.Top);
        return control;
    }

    static TextBlock Dim(string text) => new() { Text = text, FontSize = 12, Foreground = EditorBrushes.TextDim, VerticalAlignment = VerticalAlignment.Center };

    // The starts are the virtuals of the base type, which an edit of the graph does not change, so they are asked once.
    void Read()
    {
        _starts ??= [.. _script.FindOverrides().Select(entry => entry.Label)];
        _body.Children.Clear();
        var blocks = GraphSentences.Of(_script.Document, _starts);
        if (blocks.Count == 0)
        {
            _body.Children.Add(new TextBlock { Text = NoStart, Tag = NoStart, FontSize = 12, Foreground = EditorBrushes.TextDim, TextWrapping = TextWrapping.Wrap });
        }

        foreach (var block in blocks)
        {
            _body.Children.Add(new TextBlock { Text = block.Start, Tag = block.Start, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, Gaps.Tight, 0, 0) });
            if (block.Lines.Count == 0)
            {
                _body.Children.Add(new TextBlock { Text = NoStep, Tag = NoStep, FontSize = 12, Foreground = EditorBrushes.TextDim });
            }

            foreach (var line in block.Lines)
            {
                _body.Children.Add(Line(block.Function, line));
            }
        }
    }

    // The lead in dim, then the number, then the step's plain name. A way back names no step, so it says where it goes.
    StackPanel Line(string function, SentenceLine line)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Gaps.Tight, Margin = new Thickness(line.Depth * Indent, 0, 0, 0), Tag = line.Text };
        if (line.Again)
        {
            row.Children.AddRange([Dim(line.Lead == "" ? "Back to step" : $"{line.Lead} back to step"), Number(function, line.Step)]);
            return row;
        }

        if (line.Lead != "")
        {
            row.Children.Add(Dim(line.Lead));
        }

        row.Children.AddRange([Number(function, line.Step), new TextBlock { Text = line.Name, VerticalAlignment = VerticalAlignment.Center }]);
        return row;
    }

    Button Number(string function, int step)
    {
        var button = new Button
        {
            Tag = (function, step),
            Padding = new Thickness(0),
            MinHeight = 0,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = new Border
            {
                MinWidth = 20,
                Height = 20,
                Padding = new Thickness(Gaps.Tight, 0),
                CornerRadius = new CornerRadius(10),
                Background = EditorBrushes.AccentSoft,
                Child = new TextBlock { Text = $"{step}", FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = EditorBrushes.Accent, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            },
        };
        button.Click += (_, _) => StepPicked?.Invoke(function, step);
        return new HoverCard($"Step {step}", "Picks this step on the canvas.").On(button);
    }
}

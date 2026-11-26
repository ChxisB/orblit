using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// What a hover shows: the name with its key at the right, a line on what the control does, and the engine's
/// name for it in small dim mono under that. While the control is off, the line says why.
/// </summary>
internal sealed class HoverCard : StackPanel
{
    readonly TextBlock _line;
    readonly string _does;

    public HoverCard(string title, string line, string? key = null, string? engine = null)
    {
        Title = title;
        Key = key;
        Engine = engine;
        _does = line;
        Spacing = Gaps.Tight;
        MaxWidth = 260;
        var top = new DockPanel();
        if (key is not null)
        {
            var chip = Parts.Kbd(key);
            chip.Margin = new Thickness(Gaps.Group, 0, 0, 0);
            DockPanel.SetDock(chip, Dock.Right);
            top.Children.Add(chip);
        }

        top.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Medium, Foreground = EditorBrushes.Text, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        _line = new TextBlock { Text = line, FontSize = 12, Foreground = EditorBrushes.TextSoft, TextWrapping = TextWrapping.Wrap };
        Children.Add(top);
        Children.Add(_line);
        if (engine is not null)
        {
            Children.Add(new TextBlock { Text = engine, FontFamily = EditorTheme.Mono, FontSize = 10.5, Foreground = EditorBrushes.Unit, Margin = new Thickness(0, Gaps.Joined, 0, 0) });
        }
    }

    internal string Title { get; }

    internal string? Key { get; }

    internal string? Engine { get; }

    internal string Line => _line.Text ?? "";

    /// <summary>Puts the card on the control as its tip. It shows while the control is off too, so it can say why.</summary>
    internal T On<T>(T control) where T : Control
    {
        ToolTip.SetTip(control, this);
        ToolTip.SetShowOnDisabled(control, true);
        return control;
    }

    /// <summary>While the control is off, the line gives this reason in place of what the control does.</summary>
    internal void SaysWhyOff(Control control, string reason) =>
        control.GetObservable(InputElement.IsEnabledProperty).Subscribe(new AnonymousObserver<bool>(on => _line.Text = on ? _does : reason));
}

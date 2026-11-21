using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A titled card in the middle of a veil over the whole window. The header says what the dialog is for and why,
/// the body is the owner's, and the footer ends with Cancel and the main action. Escape cancels and Enter runs
/// the main action. It waits hidden until <see cref="Open"/>.
/// </summary>
internal sealed class Dialog : Border
{
    readonly DockPanel _footer;

    public Dialog(string title, string why, Control body)
    {
        var heading = new CardHead(title, why);
        Heading = heading.Heading;
        Why = heading.Why;
        CloseButton = heading.CloseButton;
        CancelButton = new Button { Content = "Cancel", Classes = { EditorTheme.TextButton, EditorTheme.Ghost }, Margin = new Thickness(0, 0, 8, 0) };
        MainButton = new Button { Classes = { EditorTheme.TextButton, EditorTheme.Primary } };
        DockPanel.SetDock(MainButton, Dock.Right);
        DockPanel.SetDock(CancelButton, Dock.Right);
        _footer = new DockPanel { LastChildFill = false, Children = { MainButton, CancelButton } };

        var footer = new Border
        {
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 4, 0, 0),
            BorderBrush = EditorBrushes.Line,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = _footer,
        };
        Card = new Border
        {
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = EditorBrushes.Popover,
            BorderBrush = EditorBrushes.LineStrong,
            BorderThickness = new Thickness(1),
            CornerRadius = EditorTheme.PopoverCorners,
            BoxShadow = BoxShadows.Parse("0 20 50 0 #8C000000, 0 2 6 0 #59000000"),
            Child = new StackPanel { Children = { heading, new Border { Padding = new Thickness(14, 12), Child = body }, footer } },
        };
        // Tab goes round the card and never reaches the editor under the veil.
        KeyboardNavigation.SetTabNavigation(Card, KeyboardNavigationMode.Cycle);

        Background = EditorBrushes.Veil;
        IsVisible = false;
        Child = Card;
        CloseButton.Click += (_, _) => Cancel();
        CancelButton.Click += (_, _) => Cancel();
    }

    /// <summary>Raised when the person leaves the dialog without its main action.</summary>
    public event Action? Cancelled;

    internal Border Card { get; }

    internal TextBlock Heading { get; }

    internal TextBlock Why { get; }

    internal Button CloseButton { get; }

    internal Button CancelButton { get; }

    internal Button MainButton { get; }

    public string Title
    {
        get => Heading.Text ?? "";
        set => Heading.Text = value;
    }

    public string MainText
    {
        get => MainButton.Content as string ?? "";
        init => MainButton.Content = value;
    }

    public string CancelText
    {
        get => CancelButton.Content as string ?? "";
        init => CancelButton.Content = value;
    }

    public bool Closable
    {
        get => CloseButton.IsVisible;
        init => CloseButton.IsVisible = value;
    }

    public double CardWidth
    {
        get => Card.Width;
        init => Card.Width = value;
    }

    /// <summary>An action at the far left of the footer, away from the main one, such as one that drops work.</summary>
    public Button? LeadButton
    {
        get;
        init
        {
            field = value;
            if (value is not null)
            {
                _footer.Children.Add(value);
            }
        }
    }

    /// <summary>Dim words at the far left of the footer, such as the keys the body takes.</summary>
    public string Hint
    {
        get;
        init
        {
            field = value;
            _footer.Children.Add(new TextBlock { Text = value, FontSize = 11, Foreground = EditorBrushes.Unit, VerticalAlignment = VerticalAlignment.Center });
        }
    } = "";

    /// <summary>Shows the dialog with the keys on the main action, so Enter runs it and Escape leaves.</summary>
    public void Open()
    {
        IsVisible = true;
        MainButton.Focus();
    }

    /// <summary>Hides the dialog once its main action went through.</summary>
    public void Close() => IsVisible = false;

    public void Cancel()
    {
        IsVisible = false;
        Cancelled?.Invoke();
    }

    // A focused button takes Enter itself, so this sees Enter only from the body, such as a name box.
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancel();
        }
        else if (e.Key == Key.Enter && MainButton.IsEffectivelyEnabled)
        {
            e.Handled = true;
            MainButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }

        base.OnKeyDown(e);
    }
}

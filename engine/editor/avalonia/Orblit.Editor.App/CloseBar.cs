using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor.App;

/// <summary>The bar that asks what to do with edits when the window is closed. It stays hidden until asked.</summary>
internal sealed class CloseBar : StackPanel
{
    readonly Button _saveAndClose = new() { Content = "Save and close" };
    readonly Button _closeWithout = new() { Content = "Close without saving" };
    readonly Button _keepEditing = new() { Content = "Keep editing" };

    public CloseBar()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        Margin = new Thickness(8, 0, 8, 8);
        IsVisible = false;
        Children.Add(new TextBlock { Text = "The scene has edits that no save holds.", VerticalAlignment = VerticalAlignment.Center });
        Children.Add(_saveAndClose);
        Children.Add(_closeWithout);
        Children.Add(_keepEditing);
        _saveAndClose.Click += (_, _) => SaveRequested?.Invoke();
        _closeWithout.Click += (_, _) => DiscardRequested?.Invoke();
        _keepEditing.Click += (_, _) => IsVisible = false;
    }

    /// <summary>Raised when the person asks to save the edits and close.</summary>
    public event Action? SaveRequested;

    /// <summary>Raised when the person asks to close and drop the edits.</summary>
    public event Action? DiscardRequested;

    internal Button SaveAndCloseButton => _saveAndClose;

    internal Button CloseWithoutButton => _closeWithout;

    internal Button KeepEditingButton => _keepEditing;
}

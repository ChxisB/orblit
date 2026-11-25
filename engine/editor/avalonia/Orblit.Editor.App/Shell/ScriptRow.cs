using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// The Script row at the foot of the inspector: a label and a field that names the attached script's file. The
/// field opens the slot menu: the project's scripts, then Load, Edit and Clear.
/// </summary>
internal sealed class ScriptRow : Grid
{
    const double RowHeight = 28;
    const string Empty = "None";

    readonly TextBlock _value = new() { Text = Empty, VerticalAlignment = VerticalAlignment.Center };
    readonly MenuFlyout _menu = new();
    string? _attached;

    public ScriptRow()
    {
        ColumnDefinitions = new ColumnDefinitions("*,*");
        Height = RowHeight;
        Margin = new Thickness(0, 4, 0, 0);
        Field = new Button
        {
            Height = RowHeight,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(12, 0, 9, 0),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = EditorBrushes.Field,
            CornerRadius = EditorTheme.Corners,
            Content = Value(_value),
            Flyout = _menu,
        };
        _menu.Opening += (_, _) => FillMenu();
        SetColumn(Field, 1);
        Children.Add(new TextBlock { Text = "Script", Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        Children.Add(Field);
    }

    /// <summary>Raised with the res:// path of the script the user chose.</summary>
    public event Action<string>? Chosen;

    public event Action? Cleared;

    /// <summary>Raised by Load, which asks for a script to attach.</summary>
    public event Action? Loading;

    /// <summary>Raised by Edit with the res:// path of the attached script.</summary>
    public event Action<string>? Editing;

    /// <summary>Lists the scripts the menu offers. It is read each time the menu opens.</summary>
    public Func<IReadOnlyList<string>> Choices { get; set; } = () => [];

    internal Button Field { get; }

    internal MenuFlyout Menu => _menu;

    /// <summary>Shows the attached script's file name, or <c>&lt;empty&gt;</c> for none.</summary>
    public void Show(string? path)
    {
        _attached = path;
        _value.Text = path is null ? Empty : Path.GetFileName(path);
        ToolTip.SetTip(Field, path);
    }

    void FillMenu()
    {
        _menu.Items.Clear();
        foreach (var path in Choices())
        {
            var item = new MenuItem { Header = path };
            item.Click += (_, _) => Chosen?.Invoke(path);
            _menu.Items.Add(item);
        }
        _menu.Items.Add(new Separator());
        _menu.Items.Add(Parts.ActionItem("Load...", () => Loading?.Invoke()));
        var edit = Parts.ActionItem("Edit", () => Editing?.Invoke(_attached!));
        _menu.Items.Add(edit);
        var clear = Parts.ActionItem("Clear", () => Cleared?.Invoke());
        _menu.Items.Add(clear);
        // Edit and Clear need a script, and the menu is filled each time it opens.
        edit.IsEnabled = clear.IsEnabled = _attached is not null;
    }

    // The text, then the load icon and the dropdown arrow 9 pixels apart.
    static Grid Value(TextBlock text)
    {
        var load = new EditorIcon("Load") { Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
        var arrow = new EditorIcon("GuiDropdown") { VerticalAlignment = VerticalAlignment.Center };
        SetColumn(load, 1);
        SetColumn(arrow, 2);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Children = { text, load, arrow } };
    }
}

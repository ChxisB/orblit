using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Orblit.Editor.App.Theme;

/// <summary>
/// The base theme with the editor's colours, font and shape laid over it. Every control in the shell takes
/// its look from here, so none of them sets a colour of its own.
/// </summary>
internal sealed class EditorTheme : Styles
{
    // The old editor ships its font as woff2 files that the renderer cannot load, so this is the system font.
    // A font package or converted files replace it in this one line (decision 218).
    internal static readonly FontFamily Font = FontFamily.Default;

    internal const double FontSize = 14;

    internal const double RowHeight = 23;

    internal const double RowIndent = 12;

    /// <summary>Pixels the theme leaves before the content of a tree row, more than the reference draws.</summary>
    internal const double RowOverhang = 15;

    const double ArrowShift = 7;

    internal static readonly CornerRadius Corners = new(4);

    // Fluent resource key and the colour that replaces it. A list of cases, so it is data.
    static readonly (string Key, Color Color)[] BrushOverrides =
    [
        ("ButtonBackground", Colors.Transparent),
        ("ButtonBackgroundPointerOver", EditorColors.Hover),
        ("ButtonBackgroundPressed", EditorColors.Field),
        ("ButtonBackgroundDisabled", Colors.Transparent),
        ("ButtonBorderBrush", Colors.Transparent),
        ("ButtonBorderBrushPointerOver", Colors.Transparent),
        ("ButtonBorderBrushPressed", Colors.Transparent),
        ("ButtonBorderBrushDisabled", Colors.Transparent),
        ("ButtonForeground", EditorColors.Text),
        ("ButtonForegroundPointerOver", EditorColors.Text),
        ("ButtonForegroundPressed", EditorColors.Text),
        ("ButtonForegroundDisabled", EditorColors.Disabled),
        ("ToggleButtonBackground", Colors.Transparent),
        ("ToggleButtonBackgroundPointerOver", EditorColors.Hover),
        ("ToggleButtonBackgroundPressed", EditorColors.Field),
        ("ToggleButtonBackgroundDisabled", Colors.Transparent),
        ("ToggleButtonBackgroundChecked", EditorColors.ActiveTool),
        ("ToggleButtonBackgroundCheckedPointerOver", EditorColors.ActiveTool),
        ("ToggleButtonBackgroundCheckedPressed", EditorColors.Field),
        ("ToggleButtonBackgroundCheckedDisabled", Colors.Transparent),
        ("ToggleButtonBorderBrush", Colors.Transparent),
        ("ToggleButtonBorderBrushPointerOver", Colors.Transparent),
        ("ToggleButtonBorderBrushPressed", Colors.Transparent),
        ("ToggleButtonBorderBrushDisabled", Colors.Transparent),
        ("ToggleButtonBorderBrushChecked", Colors.Transparent),
        ("ToggleButtonBorderBrushCheckedPointerOver", Colors.Transparent),
        ("ToggleButtonBorderBrushCheckedPressed", Colors.Transparent),
        ("ToggleButtonBorderBrushCheckedDisabled", Colors.Transparent),
        ("ToggleButtonForeground", EditorColors.Text),
        ("ToggleButtonForegroundPointerOver", EditorColors.Text),
        ("ToggleButtonForegroundPressed", EditorColors.Text),
        ("ToggleButtonForegroundDisabled", EditorColors.Disabled),
        ("ToggleButtonForegroundChecked", EditorColors.Accent),
        ("ToggleButtonForegroundCheckedPointerOver", EditorColors.Accent),
        ("ToggleButtonForegroundCheckedPressed", EditorColors.Accent),
        ("ToggleButtonForegroundCheckedDisabled", EditorColors.Disabled),
        ("TextControlBackground", EditorColors.Field),
        ("TextControlBackgroundPointerOver", EditorColors.Field),
        ("TextControlBackgroundFocused", EditorColors.Field),
        ("TextControlBackgroundDisabled", EditorColors.Field),
        ("TextControlBorderBrush", Colors.Transparent),
        ("TextControlBorderBrushPointerOver", Colors.Transparent),
        ("TextControlBorderBrushFocused", EditorColors.Accent),
        ("TextControlBorderBrushDisabled", Colors.Transparent),
        ("TextControlForeground", EditorColors.Text),
        ("TextControlForegroundPointerOver", EditorColors.Text),
        ("TextControlForegroundFocused", EditorColors.Text),
        ("TextControlForegroundDisabled", EditorColors.Disabled),
        ("TextControlPlaceholderForeground", EditorColors.TextDim),
        ("TextControlPlaceholderForegroundPointerOver", EditorColors.TextDim),
        ("TextControlPlaceholderForegroundFocused", EditorColors.TextDim),
        ("TextControlPlaceholderForegroundDisabled", EditorColors.Disabled),
        ("ComboBoxBackground", EditorColors.Field),
        ("ComboBoxBackgroundPointerOver", EditorColors.Field),
        ("ComboBoxBackgroundPressed", EditorColors.Field),
        ("ComboBoxBackgroundDisabled", EditorColors.Field),
        ("ComboBoxBorderBrush", Colors.Transparent),
        ("ComboBoxBorderBrushPointerOver", Colors.Transparent),
        ("ComboBoxBorderBrushPressed", Colors.Transparent),
        ("ComboBoxBorderBrushDisabled", Colors.Transparent),
        ("ComboBoxForeground", EditorColors.Text),
        ("ComboBoxForegroundDisabled", EditorColors.Disabled),
        ("ComboBoxDropDownBackground", EditorColors.Panel),
        ("ComboBoxDropDownBorderBrush", EditorColors.Window),
        ("ComboBoxItemBackgroundPointerOver", EditorColors.Hover),
        ("ComboBoxItemBackgroundSelected", EditorColors.SelectedRow),
        ("ComboBoxItemBackgroundSelectedPointerOver", EditorColors.SelectedRow),
        ("ComboBoxItemForeground", EditorColors.Text),
        ("ComboBoxItemForegroundPointerOver", EditorColors.Text),
        ("ComboBoxItemForegroundSelected", EditorColors.Text),
        ("ComboBoxItemForegroundSelectedPointerOver", EditorColors.Text),
        ("TreeViewItemBackground", Colors.Transparent),
        ("TreeViewItemBackgroundPointerOver", Colors.Transparent),
        ("TreeViewItemBackgroundPressed", Colors.Transparent),
        ("TreeViewItemBackgroundSelected", Colors.Transparent),
        ("TreeViewItemBackgroundSelectedPointerOver", Colors.Transparent),
        ("TreeViewItemBackgroundSelectedPressed", Colors.Transparent),
        ("TreeViewItemBorderBrush", Colors.Transparent),
        ("TreeViewItemBorderBrushPointerOver", Colors.Transparent),
        ("TreeViewItemBorderBrushPressed", Colors.Transparent),
        ("TreeViewItemBorderBrushSelected", Colors.Transparent),
        ("TreeViewItemBorderBrushSelectedPointerOver", Colors.Transparent),
        ("TreeViewItemBorderBrushSelectedPressed", Colors.Transparent),
        ("TreeViewItemForeground", EditorColors.Text),
        ("TreeViewItemForegroundPointerOver", EditorColors.Text),
        ("TreeViewItemForegroundPressed", EditorColors.Text),
        ("TreeViewItemForegroundSelected", EditorColors.Text),
        ("TreeViewItemForegroundSelectedPointerOver", EditorColors.Text),
        ("TreeViewItemForegroundSelectedPressed", EditorColors.Text),
        ("MenuFlyoutPresenterBackground", EditorColors.Panel),
        ("MenuFlyoutPresenterBorderBrush", EditorColors.Window),
        ("MenuFlyoutItemBackground", Colors.Transparent),
        ("MenuFlyoutItemBackgroundPointerOver", EditorColors.Hover),
        ("MenuFlyoutItemBackgroundPressed", EditorColors.Hover),
        ("MenuFlyoutItemForeground", EditorColors.Text),
        ("MenuFlyoutItemForegroundPointerOver", EditorColors.Text),
        ("MenuFlyoutItemForegroundPressed", EditorColors.Text),
        ("MenuFlyoutItemForegroundDisabled", EditorColors.Disabled),
        ("ToolTipBackground", EditorColors.Field),
        ("ToolTipBorderBrush", EditorColors.ActiveTool),
        ("ToolTipForeground", EditorColors.Text),
        ("ScrollBarTrackFill", Colors.Transparent),
        ("ScrollBarTrackFillPointerOver", Colors.Transparent),
        ("ScrollBarTrackStroke", Colors.Transparent),
        ("ScrollBarTrackStrokePointerOver", Colors.Transparent),
        ("ScrollBarThumbFillPointerOver", EditorColors.SelectedRow),
        ("ScrollBarThumbFillPressed", EditorColors.TextDim),
    ];

    public EditorTheme()
    {
        Add(new FluentTheme());
        var overrides = new ResourceDictionary
        {
            ["ContentControlThemeFontFamily"] = Font,
            ["ControlContentThemeFontSize"] = FontSize,
            ["ControlCornerRadius"] = Corners,
            ["TreeViewItemMinHeight"] = RowHeight,
            ["TreeViewItemIndent"] = RowIndent,
            ["ScrollBarThumbBackgroundColor"] = EditorColors.ActiveTool,
        };
        foreach (var (key, color) in BrushOverrides)
        {
            overrides[key] = new SolidColorBrush(color);
        }

        Resources = overrides;
        // The theme gives the arrow a 36 pixel box with margins that no style can change. A negative margin
        // on the arrow brings it back to the reference at 10. A row does the same for its icon.
        Add(new Style(x => x.OfType<TreeViewItem>().Template().Name("PART_ExpandCollapseChevron"))
        {
            Setters =
            {
                new Setter(Layoutable.MarginProperty, new Thickness(-ArrowShift, 0, ArrowShift, 0)),
                new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<ToggleButton>((button, _) => Arrow(button))),
            },
        });
        Add(new Style(x => x.OfType<Window>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, new SolidColorBrush(EditorColors.Window)),
                new Setter(TemplatedControl.ForegroundProperty, new SolidColorBrush(EditorColors.Text)),
            },
        });
    }

    // The theme draws a thin, wide chevron. The reference draws its own 12 pixel arrow icons.
    static Border Arrow(ToggleButton button)
    {
        var right = new EditorIcon("GuiTreeArrowRight");
        var down = new EditorIcon("GuiTreeArrowDown");
        void Point()
        {
            down.IsVisible = button.IsChecked == true;
            right.IsVisible = !down.IsVisible;
        }

        button.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
            {
                Point();
            }
        };
        Point();
        return new Border { Background = Brushes.Transparent, Child = new Grid { Children = { right, down } } };
    }

    /// <summary>Makes the application dark and applies the theme, as the editor and its tests both need.</summary>
    internal static void Apply(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Dark;
        app.Styles.Add(new EditorTheme());
    }
}

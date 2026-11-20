using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Orblit.Editor.App.Theme;

/// <summary>
/// The base theme with the editor's colors, font and shape laid over it. Every control in the shell takes
/// its look from here, so none of them sets a colour of its own.
/// </summary>
internal sealed class EditorTheme : Styles
{
    internal const string FontFolder = "avares://Orblit.Editor.App/Assets/Fonts/";

    /// <summary>The face for words.</summary>
    internal static readonly FontFamily Font = new(FontFolder + "#Geist");

    /// <summary>The face for values a reader compares by column: numbers, code and log lines.</summary>
    internal static readonly FontFamily Mono = new(FontFolder + "#Geist Mono");

    internal const double FontSize = 13;

    internal const double RowHeight = 26;

    internal const double RowIndent = 16;

    /// <summary>Pixels the theme leaves before the content of a tree row, more than the reference draws.</summary>
    internal const double RowOverhang = 15;

    const double ArrowShift = 7;

    internal static readonly CornerRadius Corners = new(6);

    /// <summary>The class that makes a border an island: a panel on the ground with a fine edge and round corners.</summary>
    internal const string Island = "island";

    internal static readonly CornerRadius IslandCorners = new(10);

    /// <summary>The corners of a menu or a flyout, a step rounder than an island.</summary>
    internal static readonly CornerRadius PopoverCorners = new(11);

    /// <summary>The class of a worded button: a raised fill with an edge. The classes after it change its kind or size.</summary>
    internal const string TextButton = "text";

    /// <summary>The one main action of a dialog or a toast, in the accent.</summary>
    internal const string Primary = "primary";

    /// <summary>A quiet action with no fill or edge, such as Cancel.</summary>
    internal const string Ghost = "ghost";

    /// <summary>An action that drops work, such as Close without saving.</summary>
    internal const string Danger = "danger";

    /// <summary>The smaller worded button a toast or a row holds.</summary>
    internal const string Small = "small";

    /// <summary>A tall worded button that holds an icon and two lines, such as a root in New Scene. A checked one is the choice.</summary>
    internal const string Card = "card";

    /// <summary>A small ghost button inside a segmented choice, 24 high. A checked one is lifted.</summary>
    internal const string Segment = "segment";

    /// <summary>A text box or a list box drawn as the design's field, 30 high with a ring while it has the keys.</summary>
    internal const string Field = "field";

    /// <summary>A field whose value cannot be used. It rings red until the value changes.</summary>
    internal const string Bad = "bad";

    /// <summary>A text box with no fill or border in any state, for a search line that its card frames.</summary>
    internal const string Bare = "bare";

    /// <summary>A flyout whose card sets its own room, so the presenter adds none.</summary>
    internal const string Flush = "flush";

    /// <summary>
    /// Marks a menu item, or the native item behind one, whose feature is not built. The theme dims its label and
    /// draws Soon where its key would be. The screen bar of macOS draws its own items, so it shows neither.
    /// </summary>
    internal static readonly AttachedProperty<bool> SoonProperty = AvaloniaProperty.RegisterAttached<AvaloniaObject, bool>("Soon", typeof(EditorTheme));

    internal static bool IsSoon(AvaloniaObject item) => item.GetValue(SoonProperty);

    internal static void MarkSoon(AvaloniaObject item) => item.SetValue(SoonProperty, true);

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
        ("ToggleButtonBackgroundChecked", EditorColors.AccentSoft),
        ("ToggleButtonBackgroundCheckedPointerOver", EditorColors.AccentSoft),
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
        ("TextControlBorderBrush", EditorColors.Line),
        ("TextControlBorderBrushPointerOver", EditorColors.LineStrong),
        ("TextControlBorderBrushFocused", EditorColors.Accent),
        ("TextControlBorderBrushDisabled", EditorColors.Line),
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
        ("ComboBoxBorderBrush", EditorColors.Line),
        ("ComboBoxBorderBrushPointerOver", EditorColors.LineStrong),
        ("ComboBoxBorderBrushPressed", EditorColors.LineStrong),
        ("ComboBoxBorderBrushDisabled", EditorColors.Line),
        ("ComboBoxForeground", EditorColors.Text),
        ("ComboBoxForegroundDisabled", EditorColors.Disabled),
        ("ComboBoxDropDownBackground", EditorColors.Popover),
        ("ComboBoxDropDownBorderBrush", EditorColors.LineStrong),
        ("ComboBoxItemBackgroundPointerOver", EditorColors.Hover),
        ("ComboBoxItemBackgroundSelected", EditorColors.AccentSoft),
        ("ComboBoxItemBackgroundSelectedPointerOver", EditorColors.AccentSoft),
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
        ("TreeViewItemForegroundSelected", Colors.White),
        ("TreeViewItemForegroundSelectedPointerOver", Colors.White),
        ("TreeViewItemForegroundSelectedPressed", Colors.White),
        ("MenuFlyoutPresenterBackground", EditorColors.Popover),
        ("MenuFlyoutPresenterBorderBrush", EditorColors.LineStrong),
        ("MenuFlyoutItemBackground", Colors.Transparent),
        ("MenuFlyoutItemBackgroundPointerOver", EditorColors.Hover),
        ("MenuFlyoutItemBackgroundPressed", EditorColors.Hover),
        ("MenuFlyoutItemForeground", EditorColors.Text),
        ("MenuFlyoutItemForegroundPointerOver", EditorColors.Text),
        ("MenuFlyoutItemForegroundPressed", EditorColors.Text),
        ("MenuFlyoutItemForegroundDisabled", EditorColors.Disabled),
        ("FlyoutPresenterBackground", EditorColors.Popover),
        ("FlyoutBorderThemeBrush", EditorColors.LineStrong),
        ("ToolTipBackground", EditorColors.Raised),
        ("ToolTipBorderBrush", EditorColors.LineStrong),
        ("ToolTipForeground", EditorColors.Text),
        ("ScrollBarTrackFill", Colors.Transparent),
        ("ScrollBarTrackFillPointerOver", Colors.Transparent),
        ("ScrollBarTrackStroke", Colors.Transparent),
        ("ScrollBarTrackStrokePointerOver", Colors.Transparent),
        ("ScrollBarThumbFillPointerOver", EditorColors.Disabled),
        ("ScrollBarThumbFillPressed", EditorColors.TextDim),
        ("ToggleSwitchFillOff", EditorColors.LineStrong),
        ("ToggleSwitchFillOffPointerOver", EditorColors.LineStrong),
        ("ToggleSwitchFillOffPressed", EditorColors.LineStrong),
        ("ToggleSwitchFillOn", EditorColors.Accent),
        ("ToggleSwitchFillOnPointerOver", EditorColors.Accent),
        ("ToggleSwitchFillOnPressed", EditorColors.Accent),
        ("ToggleSwitchKnobFillOff", EditorColors.TextSoft),
        ("ToggleSwitchKnobFillOffPointerOver", EditorColors.TextSoft),
        ("ToggleSwitchKnobFillOffPressed", EditorColors.TextSoft),
        ("ToggleSwitchKnobFillOn", EditorColors.OnAccent),
        ("ToggleSwitchKnobFillOnPointerOver", EditorColors.OnAccent),
        ("ToggleSwitchKnobFillOnPressed", EditorColors.OnAccent),
        ("ToggleSwitchStrokeOff", Colors.Transparent),
        ("ToggleSwitchStrokeOffPointerOver", Colors.Transparent),
        ("ToggleSwitchStrokeOffPressed", Colors.Transparent),
        ("ToggleSwitchStrokeOn", Colors.Transparent),
        ("ToggleSwitchStrokeOnPointerOver", Colors.Transparent),
        ("ToggleSwitchStrokeOnPressed", Colors.Transparent),
    ];

    public EditorTheme()
    {
        // The theme resolves its own accent before any parent resource, so the palette is the one place to set it.
        Add(new FluentTheme { Palettes = { [ThemeVariant.Dark] = new ColorPaletteResources { Accent = EditorColors.Accent } } });
        Resources = Overrides();
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
        // Clipped, so a viewport or a row highlight inside follows the round corners.
        Add(new Style(x => x.Is<Border>().Class(Island))
        {
            Setters =
            {
                new Setter(Border.BackgroundProperty, EditorBrushes.Panel),
                new Setter(Border.BorderBrushProperty, EditorBrushes.Line),
                new Setter(Border.BorderThicknessProperty, new Thickness(1)),
                new Setter(Border.CornerRadiusProperty, IslandCorners),
                new Setter(Visual.ClipToBoundsProperty, true),
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
        AddRange(PopoverStyles());
        AddRange(ButtonStyles());
        AddRange(FieldStyles());
    }

    static ResourceDictionary Overrides()
    {
        var overrides = new ResourceDictionary
        {
            ["ContentControlThemeFontFamily"] = Font,
            ["ControlContentThemeFontSize"] = FontSize,
            ["ControlCornerRadius"] = Corners,
            ["TreeViewItemMinHeight"] = RowHeight,
            ["TreeViewItemIndent"] = RowIndent,
            ["ScrollBarThumbBackgroundColor"] = EditorColors.Lifted,
            // The base theme puts 4 pixels over and under the rows of a menu. The menu's own inset is the only gap.
            ["MenuFlyoutScrollerMargin"] = new Thickness(0),
        };
        foreach (var (key, color) in BrushOverrides)
        {
            overrides[key] = new SolidColorBrush(color);
        }

        return overrides;
    }

    // A menu is a popover: a 5 pixel inset round rows that have round corners of their own. A flyout holds a
    // form, so it keeps the base theme's room inside and takes only the popover's corners.
    static Style[] PopoverStyles() =>
    [
        new(x => Selectors.Or(x.OfType<MenuFlyoutPresenter>(), x.OfType<ContextMenu>()))
        {
            Setters =
            {
                new Setter(TemplatedControl.CornerRadiusProperty, PopoverCorners),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(5)),
            },
        },
        new(x => x.OfType<MenuItem>())
        {
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 28.0),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(8, 0)),
                new Setter(TemplatedControl.CornerRadiusProperty, Corners),
            },
        },
        new(x => x.OfType<FlyoutPresenter>()) { Setters = { new Setter(TemplatedControl.CornerRadiusProperty, PopoverCorners) } },
        new(x => x.OfType<FlyoutPresenter>().Class(Flush)) { Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(0)) } },
        // The window bar draws one item for each native item, so it carries the mark of the item it draws.
        new(x => x.OfType<NativeMenuBar>().Descendant().OfType<MenuItem>())
        {
            Setters = { new Setter(SoonProperty, new Binding { Converter = new FuncValueConverter<object?, bool>(item => item is NativeMenuItem native && IsSoon(native)) }) },
        },
        // The label stays readable so people learn the menu. The tag takes the place of the key.
        new(x => x.OfType<MenuItem>().PropertyEquals(SoonProperty, true)) { Setters = { new Setter(TemplatedControl.ForegroundProperty, EditorBrushes.Unbuilt) } },
        new(x => x.OfType<MenuItem>().PropertyEquals(SoonProperty, true).Template().OfType<TextBlock>().Name("PART_InputGestureText"))
        {
            Setters =
            {
                new Setter(TextBlock.TextProperty, "Soon"),
                new Setter(TextBlock.ForegroundProperty, EditorBrushes.Warn),
                new Setter(TextBlock.FontWeightProperty, FontWeight.SemiBold),
            },
        },
        // The menus of a bar are header tabs, so they take the tab height and a softer name.
        new(x => x.Is<Menu>().Child().Is<MenuItem>())
        {
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 26.0),
                new Setter(TemplatedControl.ForegroundProperty, EditorBrushes.TextSoft),
                new Setter(TemplatedControl.FontWeightProperty, FontWeight.Medium),
            },
        },
    ];

    // The fill, hovered fill, edge and text of each kind of worded button. A list of cases, so it is data. Later
    // rows win over the first, which every kind also carries.
    static readonly (string Kind, IBrush Fill, IBrush Lit, IBrush Edge, IBrush Text)[] ButtonKinds =
    [
        (TextButton, EditorBrushes.Raised, EditorBrushes.Hover, EditorBrushes.LineStrong, EditorBrushes.Text),
        (Primary, EditorBrushes.Accent, EditorBrushes.Accent, Brushes.Transparent, EditorBrushes.OnAccent),
        (Ghost, Brushes.Transparent, EditorBrushes.Hover, Brushes.Transparent, EditorBrushes.TextSoft),
        (Danger, Brushes.Transparent, EditorBrushes.Hover, EditorBrushes.FaultEdge, EditorBrushes.FaultText),
    ];

    // The base theme paints a hovered or pressed button on its template part from its own resources, which would
    // turn the accent grey. So each kind lights its own fill and keeps its edge and text.
    static IEnumerable<Style> ButtonStyles()
    {
        yield return new Style(x => x.OfType<Button>().Class(TextButton))
        {
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 30.0),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 0)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(7)),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                new Setter(TemplatedControl.FontWeightProperty, FontWeight.Medium),
            },
        };
        foreach (var (kind, fill, lit, edge, text) in ButtonKinds)
        {
            yield return new Style(x => x.OfType<Button>().Class(TextButton).Class(kind))
            {
                Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, fill),
                    new Setter(TemplatedControl.BorderBrushProperty, edge),
                    new Setter(TemplatedControl.ForegroundProperty, text),
                },
            };
            yield return new Style(x => Selectors.Or(
                x.OfType<Button>().Class(TextButton).Class(kind).Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"),
                x.OfType<Button>().Class(TextButton).Class(kind).Class(":pressed").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter")))
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, lit),
                    new Setter(ContentPresenter.BorderBrushProperty, edge),
                    new Setter(ContentPresenter.ForegroundProperty, text),
                },
            };
        }

        yield return new Style(x => x.OfType<Button>().Class(Primary)) { Setters = { new Setter(TemplatedControl.FontWeightProperty, FontWeight.SemiBold) } };
        yield return new Style(x => x.OfType<Button>().Class(TextButton).Class(Small))
        {
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 26.0),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 0)),
                new Setter(TemplatedControl.FontSizeProperty, 12.0),
                new Setter(TemplatedControl.CornerRadiusProperty, Corners),
            },
        };
        yield return new Style(x => x.OfType<Button>().Class(TextButton).Class(Card))
        {
            Setters =
            {
                new Setter(Layoutable.HeightProperty, double.NaN),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(12)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(10)),
                new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Left),
                new Setter(TemplatedControl.FontWeightProperty, FontWeight.Normal),
            },
        };
        yield return new Style(x => x.OfType<Button>().Class(Card).Class(":checked"))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, EditorBrushes.AccentSoft), new Setter(TemplatedControl.BorderBrushProperty, EditorBrushes.Accent) },
        };
        // After the kinds, so a hovered choice keeps its accent.
        yield return new Style(x => x.OfType<Button>().Class(Card).Class(":checked").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, EditorBrushes.AccentSoft), new Setter(ContentPresenter.BorderBrushProperty, EditorBrushes.Accent) },
        };
        // After Small, so the segment's height wins.
        yield return new Style(x => x.OfType<Button>().Class(TextButton).Class(Segment)) { Setters = { new Setter(Layoutable.HeightProperty, 24.0) } };
        yield return new Style(x => x.OfType<Button>().Class(Segment).Class(":checked"))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, EditorBrushes.Lifted), new Setter(TemplatedControl.ForegroundProperty, EditorBrushes.Text) },
        };
        yield return new Style(x => x.OfType<Button>().Class(Segment).Class(":checked").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, EditorBrushes.Lifted), new Setter(ContentPresenter.ForegroundProperty, EditorBrushes.Text) },
        };
        // The base theme clears the fill and the edge of a button that is off. The design keeps its shape in a sunk fill.
        yield return new Style(x => x.OfType<Button>().Class(TextButton).Class(":disabled").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, EditorBrushes.DisabledFill),
                new Setter(ContentPresenter.BorderBrushProperty, EditorBrushes.Line),
                new Setter(ContentPresenter.ForegroundProperty, EditorBrushes.Disabled),
            },
        };
    }

    // A 3 pixel ring with no blur, as the design draws round a field.
    static BoxShadows Ring(Color color) => new(new BoxShadow { Spread = 3, Color = color });

    // The design's field. The ring sits on the template's border, which the base theme colours by state, so the
    // fault comes after focus and wins while both hold.
    static IEnumerable<Style> FieldStyles()
    {
        yield return new Style(x => x.OfType<TextBox>().Class(Field))
        {
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 30.0),
                new Setter(Layoutable.MinHeightProperty, 0.0),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 0)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(7)),
                new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            },
        };
        yield return new Style(x => x.OfType<ComboBox>().Class(Field))
        {
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 30.0),
                new Setter(Layoutable.MinHeightProperty, 0.0),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 0, 0, 0)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(7)),
                new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
            },
        };
        yield return new Style(x => x.OfType<TextBox>().Class(Field).Class(":focus").Template().OfType<Border>().Name("PART_BorderElement"))
        {
            Setters = { new Setter(Border.BoxShadowProperty, Ring(EditorColors.AccentSoft)) },
        };
        yield return new Style(x => x.OfType<TextBox>().Class(Bare).Template().OfType<Border>().Name("PART_BorderElement"))
        {
            Setters = { new Setter(Border.BackgroundProperty, Brushes.Transparent), new Setter(Border.BorderThicknessProperty, new Thickness(0)) },
        };
        yield return new Style(x => x.OfType<TextBox>().Class(Field).Class(Bad).Template().OfType<Border>().Name("PART_BorderElement"))
        {
            Setters = { new Setter(Border.BorderBrushProperty, EditorBrushes.Fault), new Setter(Border.BoxShadowProperty, Ring(EditorColors.FaultSoft)) },
        };
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

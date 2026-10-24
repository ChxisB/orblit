using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// An interface described somewhere else, drawn here. It measures itself, which is what makes an
/// interface responsive without anybody wiring it up: the width it is given decides which prefixed
/// classes apply and, for a canvas laid out responsively, how much bigger everything gets.
/// </summary>
public sealed class UiSurface : Decorator
{
    private UiNode? _description;
    private UiTheme _vocabulary = new();
    private UiCanvas? _canvas;
    private double? _knownWidth;
    private UiEvent? _onEvent;
    private string? _fontFamily;
    private IReadOnlyList<string> _fontFallback = [];
    private UiStrings? _strings;
    private UiDecorator? _decorate;
    private ImageLoader? _images;

    private UiStrings? _listening;
    private bool _dirty = true;
    private double _builtAt = double.NaN;

    /// <summary>What to draw. Null draws nothing.</summary>
    public UiNode? Description
    {
        get => _description;
        set => Change(ref _description, value);
    }

    /// <summary>The sizes, colours and breakpoints the class list is read against. Not called Theme, which a control already has.</summary>
    public UiTheme Vocabulary
    {
        get => _vocabulary;
        set => Change(ref _vocabulary, value);
    }

    /// <summary>The canvas this was laid out on. Null for a description a script built from nothing, which has no size to be fluid against.</summary>
    public UiCanvas? Canvas
    {
        get => _canvas;
        set => Change(ref _canvas, value);
    }

    /// <summary>
    /// The width to lay out at, when the caller already knows it. The editor does: it shows a phone
    /// inside a panel, and the width that matters is the phone's.
    /// </summary>
    public double? KnownWidth
    {
        get => _knownWidth;
        set => Change(ref _knownWidth, value);
    }

    public UiEvent? OnEvent
    {
        get => _onEvent;
        set => Change(ref _onEvent, value);
    }

    public string? FontFamily
    {
        get => _fontFamily;
        set => Change(ref _fontFamily, value);
    }

    public IReadOnlyList<string> FontFallback
    {
        get => _fontFallback;
        set => Change(ref _fontFallback, value);
    }

    /// <summary>Where keyed text finds its words. The surface builds again when a language or a table changes.</summary>
    public UiStrings? Strings
    {
        get => _strings;
        set
        {
            Change(ref _strings, value);
            Listen();
        }
    }

    public UiDecorator? Decorate
    {
        get => _decorate;
        set => Change(ref _decorate, value);
    }

    public ImageLoader? Images
    {
        get => _images;
        set => Change(ref _images, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Listen();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Listen();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = KnownWidth ?? Offered(availableSize.Width);
        if (_dirty || width != _builtAt)
        {
            Rebuild(width);
        }

        return base.MeasureOverride(availableSize);
    }

    // An unbounded width is a real case, a surface inside a row that scrolls, and it is not a width
    // to resolve breakpoints against, since every one of them would be in play at once. The window
    // is the honest fallback, and zero, the narrowest layout, is the honest fallback to that.
    private double Offered(double offered) =>
        double.IsFinite(offered) ? offered : TopLevel.GetTopLevel(this) is { } top ? top.ClientSize.Width : 0;

    private void Change<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Refresh();
    }

    private void Refresh()
    {
        _dirty = true;
        InvalidateMeasure();
    }

    // Only a surface on screen listens, so a table does not keep a surface nobody sees alive.
    private void Listen()
    {
        var wanted = TopLevel.GetTopLevel(this) is null ? null : _strings;
        if (ReferenceEquals(_listening, wanted))
        {
            return;
        }

        if (_listening is not null)
        {
            _listening.Changed -= Refresh;
        }

        _listening = wanted;
        if (wanted is not null)
        {
            wanted.Changed += Refresh;
        }
    }

    private void Rebuild(double width)
    {
        _dirty = false;
        _builtAt = width;
        var theme = Vocabulary.Scaled(Canvas?.FluidScale(width) ?? 1);
        Defaults(theme);
        Child = Description is null ? null : new UiBuilder(new UiBuilderOptions
        {
            Theme = theme,
            OnEvent = OnEvent,
            FontFamily = FontFamily,
            FontFallback = FontFallback,
            Strings = Strings,
            Decorate = Decorate,
            Width = width,
            Images = Images,
        }).Build(Description);
    }

    // Defaults for anything the description does not set, so text is legible before anybody has
    // styled it. A description that says nothing about colour should not be dark on dark.
    private void Defaults(UiTheme theme)
    {
        if (theme.Text.TryGetValue("base", out var size))
        {
            SetValue(TextElement.FontSizeProperty, size);
        }

        if (theme.Colour("slate-100") is { } colour)
        {
            SetValue(TextElement.ForegroundProperty, UiBox.Brush(colour));
        }

        if (UiBuilder.FamilyOf(FontFamily, FontFallback) is { } family)
        {
            SetValue(TextElement.FontFamilyProperty, family);
        }
    }
}

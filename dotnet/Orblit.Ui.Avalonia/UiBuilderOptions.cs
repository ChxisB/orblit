using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// What happens when somebody presses something. The handler is the name script gave its callback.
/// The payload is what the element has to say about the event, such as the text in a field.
/// </summary>
public delegate void UiEvent(string handler, object? payload);

/// <summary>
/// A chance to wrap every element as it is built. The editor uses it to draw over an interface
/// without the interface knowing. The path is where the node sits in the tree. Growing and placing
/// are read from the control this returns, so a wrapper must be the outermost one.
/// </summary>
public delegate Control UiDecorator(UiNode node, IReadOnlyList<int> path, Control built);

/// <summary>Loads the picture an image element names. A null result shows nothing.</summary>
public delegate Task<IImage?> ImageLoader(string source);

/// <summary>What a <see cref="UiBuilder"/> needs to know besides the description.</summary>
public sealed record UiBuilderOptions
{
    public UiTheme Theme { get; init; } = new();

    /// <summary>Called when an element with a handler is used.</summary>
    public UiEvent? OnEvent { get; init; }

    /// <summary>The font everything is set in, when the host has one it wants used.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Fonts tried, in order, for a character <see cref="FontFamily"/> has no glyph for.</summary>
    public IReadOnlyList<string> FontFallback { get; init; } = [];

    /// <summary>Where a text with a key finds its words. Null shows the key.</summary>
    public UiStrings? Strings { get; init; }

    /// <summary>Called for every element. Null in a game, where the design chrome is never built.</summary>
    public UiDecorator? Decorate { get; init; }

    /// <summary>
    /// The width the whole interface is laid out at, which the prefixed classes are resolved against.
    /// It is one width for the tree, so <c>md:</c> reads the same everywhere on one screen.
    /// </summary>
    public double? Width { get; init; }

    /// <summary>Where an image element gets its picture. Null shows no pictures.</summary>
    public ImageLoader? Images { get; init; }
}

using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>One pin of one step. Port counts from 0 on its own side.</summary>
internal readonly record struct PinRef(int Step, int Port, bool IsInput)
{
    /// <summary>The wire that joins this pin to another. Both pins must be on opposite sides.</summary>
    public GraphWire WireTo(PinRef other) => IsInput ? new GraphWire(other.Step, other.Port, Step, Port) : new GraphWire(Step, Port, other.Step, other.Port);
}

/// <summary>The colors a pin and its wire are drawn in.</summary>
internal static class PinColors
{
    // The type numbers are the engine's Variant types: 0 is any, 1 bool, 2 int, 3 float, 4 String, 5 to 19 the
    // vectors and transforms, 20 Color, 21 and 22 names and paths, and from 24 on the objects and containers.
    const int Bool = 1;
    const int Int = 2;
    const int Float = 3;
    const int Text = 4;
    const int FirstMath = 5;
    const int ColorType = 20;
    const int FirstName = 21;
    const int FirstObject = 24;

    static readonly IBrush s_flow = Brush("#f0f0f0");
    static readonly IBrush s_any = Brush("#9a9a9a");
    static readonly IBrush s_bool = Brush("#d9534f");
    static readonly IBrush s_int = Brush("#4fb3a5");
    static readonly IBrush s_float = Brush("#8cc152");
    static readonly IBrush s_text = Brush("#5b9bd5");
    static readonly IBrush s_math = Brush("#b28ae6");
    static readonly IBrush s_color = Brush("#e87fb0");
    static readonly IBrush s_name = Brush("#6fa8dc");
    static readonly IBrush s_object = Brush("#e0a050");

    public static IBrush Flow => s_flow;

    public static IBrush Any => s_any;

    public static IBrush Of(GraphPort port) => port.Sort == GraphPortSort.Flow ? s_flow : OfType(port.Type);

    static IBrush OfType(int type) => type switch
    {
        Bool => s_bool,
        Int => s_int,
        Float => s_float,
        Text => s_text,
        >= FirstMath and < ColorType => s_math,
        ColorType => s_color,
        >= FirstName and < FirstObject => s_name,
        >= FirstObject => s_object,
        _ => s_any,
    };

    static ImmutableSolidColorBrush Brush(string color) => new(Avalonia.Media.Color.Parse(color));
}

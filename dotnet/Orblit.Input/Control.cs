using System.Diagnostics;

namespace Orblit.Input;

/// <summary>
/// One thing a player can press, push or touch.
/// </summary>
/// <remarks>
/// A key is named by a string the game supplies. Any string works as long as the same key always
/// gives the same one. The physical key, such as <c>KeyW</c>, keeps a binding in place when the
/// keyboard layout changes. A touch region is a name too, for a rectangle the game draws.
/// </remarks>
public abstract class Control : IEquatable<Control>
{
    private protected Control()
    {
    }

    /// <summary>What makes two controls the same one, within a kind.</summary>
    private protected abstract object Identity { get; }

    /// <summary>Reads what <see cref="ToJson"/> wrote. Throws a <see cref="FormatException"/> for anything else.</summary>
    public static Control FromJson(object? json) =>
        Read(json) ?? throw new FormatException($"This is not a control: {json}");

    /// <summary>The control as JSON, for a saved profile.</summary>
    public IReadOnlyDictionary<string, object?> ToJson()
    {
        var (key, name) = this switch
        {
            KeyControl control => ("key", control.Key),
            PadButtonControl control => ("padButton", EnumNames.Of(control.Button)),
            PadAxisControl control => (control.Side.JsonKey(), EnumNames.Of(control.Axis)),
            PadStickControl control => ("padStick", EnumNames.Of(control.Stick)),
            TouchControl control => ("touch", control.Region),
            _ => throw new UnreachableException(),
        };
        return new OrderedDictionary<string, object?> { [key] = name };
    }

    public bool Equals(Control? other) =>
        other is not null && other.GetType() == GetType() && other.Identity.Equals(Identity);

    public override bool Equals(object? obj) => Equals(obj as Control);

    public override int GetHashCode() => HashCode.Combine(GetType(), Identity);

    public override string ToString() => $"{GetType().Name}({Identity})";

    /// <summary>The control a one-entry object names, or null for anything that is not one.</summary>
    internal static Control? Read(object? json) =>
        json is IReadOnlyDictionary<string, object?> { Count: 1 } map
        && map.Single() is { Value: string name } entry
            ? Named(entry.Key, name)
            : null;

    private static Control? Named(string kind, string name) => kind switch
    {
        "key" => new KeyControl(name),
        "padButton" => EnumNames.Find<PadButton>(name) is { } button ? new PadButtonControl(button) : null,
        "padAxis" => Axis(name, AxisSide.Both),
        "padAxisPositive" => Axis(name, AxisSide.Positive),
        "padAxisNegative" => Axis(name, AxisSide.Negative),
        "padStick" => EnumNames.Find<PadStick>(name) is { } stick ? new PadStickControl(stick) : null,
        "touch" => new TouchControl(name),
        _ => null,
    };

    private static PadAxisControl? Axis(string name, AxisSide side) =>
        EnumNames.Find<PadAxis>(name) is { } axis ? new PadAxisControl(axis, side) : null;
}

/// <summary>A key, by the name the game gives it.</summary>
public sealed class KeyControl(string key) : Control
{
    public string Key { get; } = key;

    private protected override object Identity => Key;
}

/// <summary>A button on a pad.</summary>
public sealed class PadButtonControl(PadButton button) : Control
{
    public PadButton Button { get; } = button;

    private protected override object Identity => Button;
}

/// <summary>Which way along an axis a <see cref="PadAxisControl"/> reads.</summary>
public enum AxisSide
{
    /// <summary>Both ways, from -1 to 1.</summary>
    Both,

    /// <summary>Only the push above the middle, from 0 to 1.</summary>
    Positive,

    /// <summary>Only the push below the middle, from 0 to 1.</summary>
    Negative,
}

public static class AxisSideExtensions
{
    /// <summary>The key a saved control is written under.</summary>
    public static string JsonKey(this AxisSide side) => side switch
    {
        AxisSide.Both => "padAxis",
        AxisSide.Positive => "padAxisPositive",
        AxisSide.Negative => "padAxisNegative",
        _ => throw new UnreachableException(),
    };

    /// <summary><paramref name="value"/> as this side reads it.</summary>
    public static double Read(this AxisSide side, double value) => side switch
    {
        AxisSide.Both => value,
        AxisSide.Positive => Math.Max(0, value),
        AxisSide.Negative => Math.Max(0, -value),
        _ => throw new UnreachableException(),
    };
}

/// <summary>
/// One axis of a pad, such as a trigger or half of a stick.
/// </summary>
/// <remarks>
/// A half is a control like a key: it reads 0 to 1, and the binding's scale says which way the
/// action goes. That keeps a rebound direction right when a player pushes a stick left for "steer
/// left".
/// </remarks>
public sealed class PadAxisControl(PadAxis axis, AxisSide side = AxisSide.Both) : Control
{
    public PadAxis Axis { get; } = axis;

    public AxisSide Side { get; } = side;

    private protected override object Identity => (Axis, Side);
}

/// <summary>A whole stick, as the pair of axes it is. Only a two-axis action takes one.</summary>
public sealed class PadStickControl(PadStick stick) : Control
{
    public PadStick Stick { get; } = stick;

    private protected override object Identity => Stick;
}

/// <summary>A named region of a touch screen.</summary>
public sealed class TouchControl(string region) : Control
{
    public string Region { get; } = region;

    private protected override object Identity => Region;
}

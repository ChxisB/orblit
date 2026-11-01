using Orblit.Json;

namespace Orblit.Input.Tests;

internal static class ActionFixture
{
    internal static readonly IReadOnlyList<InputAction> Actions =
    [
        InputAction.Button("jump"),
        InputAction.Button("fire"),
        InputAction.Button("confirm"),
        InputAction.Axis("steer"),
        InputAction.Axis2("move"),
    ];

    internal static TimeSpan Ms(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    internal static KeyControl Key(string name) => new(name);

    internal static Dictionary<PadAxis, double> Axes(params (PadAxis Axis, double Value)[] values) =>
        values.ToDictionary(pair => pair.Axis, pair => pair.Value);

    /// <summary>One frame of the devices, with every part optional.</summary>
    internal static FrameControls Frame(
        IReadOnlyCollection<string>? keys = null,
        IReadOnlyCollection<string>? touch = null,
        IReadOnlyCollection<PadButton>? down = null,
        IReadOnlyDictionary<PadAxis, double>? axes = null)
    {
        var held = axes ?? new Dictionary<PadAxis, double>();
        return new FrameControls(
            keys,
            touch,
            pad: new PadState(0, new PadIdentity("test"), down ?? [], [], [], held, held));
    }

    /// <summary>
    /// Gameplay on the keyboard and a pad, a vehicle that rebinds the jump key and leaves the rest,
    /// and a menu that blocks.
    /// </summary>
    internal static List<InputContext> Contexts(string jumpKey = "Space") =>
    [
        new InputContext(
            "play",
            new Dictionary<string, IReadOnlyList<Binding>>
            {
                ["jump"] = [new Binding(Key(jumpKey)), new Binding(new PadButtonControl(PadButton.South))],
                ["fire"] = [new Binding(Key("KeyF"))],
                ["steer"] = [new Binding(Key("KeyA"), scale: -1), new Binding(Key("KeyD"))],
                ["move"] =
                [
                    new Binding(Key("KeyA"), scale: -1),
                    new Binding(Key("KeyD")),
                    new Binding(Key("KeyS"), scale: -1, part: AxisPart.Y),
                    new Binding(Key("KeyW"), part: AxisPart.Y),
                    new Binding(new PadStickControl(PadStick.Left), shaping: Shaping.None),
                ],
            }),
        new InputContext(
            "vehicle",
            new Dictionary<string, IReadOnlyList<Binding>> { ["jump"] = [new Binding(Key("KeyE"))] }),
        new InputContext(
            "menu",
            new Dictionary<string, IReadOnlyList<Binding>> { ["confirm"] = [new Binding(Key("Enter")), new Binding(Key("Space"))] },
            blocks: true),
    ];

    /// <summary>What a player's file holds, as it would be written and read back.</summary>
    internal static object? Saved(BindingTable table) => JsonText.Decode(JsonText.Encode(table.ToJson()));

    /// <summary>Text for <paramref name="json"/> with every map's keys in order, because a map is equal to another with the same entries in any order.</summary>
    internal static string Canonical(object? json) => JsonText.Encode(Sorted(json));

    internal static string SavedText(BindingTable table) => Canonical(Saved(table));

    internal static string Written(string json) => Canonical(JsonText.Decode(json));

    /// <summary>A map with one action and one context, on from the start.</summary>
    internal static ActionMap Play(InputAction action, params Binding[] bindings) =>
        new(
            [action],
            [new InputContext("play", new Dictionary<string, IReadOnlyList<Binding>> { [action.Name] = bindings })],
            ["play"]);

    private static object? Sorted(object? json) => json switch
    {
        IReadOnlyDictionary<string, object?> map => new OrderedDictionary<string, object?>(
            map.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => KeyValuePair.Create(pair.Key, Sorted(pair.Value)))),
        IReadOnlyList<object?> list => list.Select(Sorted).ToList(),
        _ => json,
    };

    internal static ActionMap Map(IEnumerable<string>? active = null, string jumpKey = "Space") =>
        new(Actions, Contexts(jumpKey), active ?? ["play"]);
}

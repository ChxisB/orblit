using Orblit.VectorMath;

namespace Orblit.Input;

/// <summary>
/// The things a game's player can do, and which controls do them. The game names its actions and
/// groups their default bindings into contexts. Once a frame it calls <see cref="Update"/> with
/// what the devices hold, then asks about actions by name. It never asks about a key.
/// </summary>
/// <remarks>
/// Contexts stack. The one switched on last reads first. An action that a higher context binds
/// uses that context's bindings. A higher context that blocks hides every action it does not bind,
/// and everything under it.
/// </remarks>
public sealed class ActionMap
{
    private readonly Dictionary<string, InputAction> _actions;
    private readonly List<string> _stack = [];
    private readonly Dictionary<string, ButtonTrack> _buttons = new();
    private readonly Dictionary<string, double> _values = new();
    private readonly Dictionary<string, Vector2> _vectors = new();
    private readonly Dictionary<string, IReadOnlyList<Binding>?> _seen = new();

    public ActionMap(IReadOnlyList<InputAction> actions, IReadOnlyList<InputContext> contexts, IEnumerable<string>? active = null)
    {
        _actions = actions.ToDictionary(action => action.Name);
        if (_actions.Count != actions.Count)
        {
            throw new ArgumentException("Two actions have the same name.", nameof(actions));
        }

        Bindings = new BindingTable(actions.ToDictionary(action => action.Name, action => action.Kind), contexts);
        foreach (var action in actions)
        {
            Register(action);
        }

        foreach (var context in active ?? [])
        {
            Activate(context);
        }
    }

    /// <summary>The bindings, for rebinding and for saving.</summary>
    public BindingTable Bindings { get; }

    /// <summary>Whether <paramref name="context"/> is switched on.</summary>
    public bool IsActive(string context) => _stack.Contains(context);

    /// <summary>Switches <paramref name="context"/> on, above every other.</summary>
    public void Activate(string context)
    {
        if (!Bindings.HasContext(context))
        {
            throw new ArgumentException($"There is no context called \"{context}\".", nameof(context));
        }

        _stack.Remove(context);
        _stack.Add(context);
    }

    /// <summary>Switches <paramref name="context"/> off. Does nothing when it is off already.</summary>
    public void Deactivate(string context) => _stack.Remove(context);

    /// <summary>
    /// Reads one frame of the devices, at <paramref name="now"/>. The clock is passed in so that a
    /// replay and a test decide what time it is. Call it once a frame, before asking about any
    /// action.
    /// </summary>
    /// <remarks>
    /// A control that is down on the first call counts as a press. After that, one that is down
    /// when its binding appears has to come up first.
    /// </remarks>
    public void Update(IControlSource source, TimeSpan now)
    {
        var shown = Shown();
        foreach (var action in _actions.Values)
        {
            Advance(action, shown.GetValueOrDefault(action.Name), source, now);
        }
    }

    /// <summary>Whether the button is held.</summary>
    public bool IsDown(string action) => Button(action).IsDown;

    /// <summary>Whether the button went down in the last <see cref="Update"/>.</summary>
    public bool WasPressed(string action) => Button(action).WasPressed;

    /// <summary>Whether the button came up in the last <see cref="Update"/>, or was cut off while down by a context hiding it or a rebinding.</summary>
    public bool WasReleased(string action) => Button(action).WasReleased;

    /// <summary>Whether the button reached its hold time in the last <see cref="Update"/>. True once per press.</summary>
    public bool WasHeld(string action) => Button(action).WasHeld;

    /// <summary>Whether the button came up in the last <see cref="Update"/> after a short press.</summary>
    public bool WasTapped(string action) => Button(action).WasTapped;

    /// <summary>Whether the button went down in the last <see cref="Update"/> soon after a tap came up.</summary>
    public bool WasDoubleTapped(string action) => Button(action).WasDoubleTapped;

    /// <summary>An axis, from -1 to 1.</summary>
    public double Value(string action) =>
        _values.TryGetValue(action, out var value) ? value : throw Refuse(action, ActionKind.Axis);

    /// <summary>A two-axis action, no longer than 1. A copy, so it can be changed.</summary>
    public Vector2 Vector(string action) =>
        _vectors.TryGetValue(action, out var vector) ? vector.Clone() : throw Refuse(action, ActionKind.Axis2);

    private static double Scalar(Binding binding, IControlSource source) =>
        binding.Shaping.Scalar(source.Read(binding.Control)) * binding.Scale;

    private static double Sum(IReadOnlyList<Binding> list, IControlSource source) =>
        Doubles.Clamp(list.Aggregate(0.0, (sum, binding) => sum + Scalar(binding, source)), -1.0, 1.0);

    // A whole stick feeds both halves. Any other control feeds the half its binding names. Keys on
    // two sides of a diagonal would otherwise be faster than one, so the sum is brought back to a
    // length of 1.
    private static Vector2 Combine(IReadOnlyList<Binding> list, IControlSource source)
    {
        var sum = Vector2.Zero();
        foreach (var binding in list)
        {
            if (binding.Control is PadStickControl stick)
            {
                var shaped = binding.Shaping.Stick(source.Stick(stick.Stick)) * binding.Scale;
                sum.X += shaped.X;
                sum.Y += shaped.Y;
            }
            else if (binding.Part == AxisPart.X)
            {
                sum.X += Scalar(binding, source);
            }
            else
            {
                sum.Y += Scalar(binding, source);
            }
        }

        if (sum.Length > 1)
        {
            sum.Normalize();
        }

        return sum;
    }

    private void Register(InputAction action)
    {
        switch (action.Kind)
        {
            case ActionKind.Button:
                _buttons[action.Name] = new ButtonTrack();
                break;
            case ActionKind.Axis:
                _values[action.Name] = 0;
                break;
            case ActionKind.Axis2:
                _vectors[action.Name] = Vector2.Zero();
                break;
        }
    }

    private void Advance(InputAction action, IReadOnlyList<Binding>? list, IControlSource source, TimeSpan now)
    {
        var name = action.Name;
        switch (action.Kind)
        {
            case ActionKind.Button:
                _buttons[name].Advance(
                    down: list is not null && list.Any(binding => Scalar(binding, source) >= IControlSource.PressPoint),
                    // Bindings are replaced, never edited, so a new list is a new meaning. The first
                    // frame has nothing to differ from, so a test can press on it.
                    changed: _seen.TryGetValue(name, out var seen) && !ReferenceEquals(seen, list),
                    now: now,
                    gestures: action.Gestures);
                break;
            case ActionKind.Axis:
                _values[name] = list is null ? 0 : Sum(list, source);
                break;
            case ActionKind.Axis2:
                _vectors[name] = list is null ? Vector2.Zero() : Combine(list, source);
                break;
        }

        _seen[name] = list;
    }

    private ButtonTrack Button(string action) =>
        _buttons.TryGetValue(action, out var track) ? track : throw Refuse(action, ActionKind.Button);

    private Exception Refuse(string name, ActionKind wanted) =>
        _actions.TryGetValue(name, out var action)
            ? new InvalidOperationException($"\"{name}\" is {EnumNames.Of(action.Kind)}, not {EnumNames.Of(wanted)}.")
            : new ArgumentException($"There is no action called \"{name}\".", nameof(name));

    // What each action reads from now: the first context from the top that binds it, stopping after
    // one that blocks.
    // shortcut: a new map every update, which a game with hundreds of actions would feel. Cache it
    // between changes of the stack and the bindings then.
    private Dictionary<string, IReadOnlyList<Binding>> Shown()
    {
        var shown = new Dictionary<string, IReadOnlyList<Binding>>();
        foreach (var context in Enumerable.Reverse(_stack))
        {
            foreach (var (action, list) in Bindings.BindingsIn(context))
            {
                shown.TryAdd(action, list);
            }

            if (Bindings.Blocks(context))
            {
                break;
            }
        }

        return shown;
    }
}

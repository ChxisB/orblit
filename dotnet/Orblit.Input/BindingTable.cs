using System.Collections.ObjectModel;

namespace Orblit.Input;

/// <summary>
/// Which controls are wired to which actions, in each context. Starts as the defaults the contexts
/// were made with. A player changes it with <see cref="Rebind"/>, and <see cref="ToJson"/> writes
/// down only the slots that differ from the defaults, so a default the game changes later still
/// reaches a player who never touched that slot.
/// </summary>
/// <remarks>
/// Only a <see cref="Rebind"/> is checked for clashes, and only inside its own context. A game's
/// defaults may give one control to two actions on purpose.
/// </remarks>
public sealed class BindingTable
{
    private readonly Dictionary<string, ActionKind> _kinds;
    private readonly Dictionary<string, InputContext> _contexts = new();
    private readonly Dictionary<string, OrderedDictionary<string, IReadOnlyList<Binding>>> _bound = new();

    public BindingTable(IReadOnlyDictionary<string, ActionKind> kinds, IReadOnlyList<InputContext> contexts)
    {
        _kinds = new Dictionary<string, ActionKind>(kinds);
        foreach (var context in contexts)
        {
            _contexts[context.Name] = context;
            _bound[context.Name] = new();
        }

        if (_contexts.Count != contexts.Count)
        {
            throw new ArgumentException("Two contexts have the same name.", nameof(contexts));
        }

        foreach (var context in contexts)
        {
            Check(context);
        }

        Replace(Defaults());
    }

    /// <summary>Whether a context of this name exists.</summary>
    public bool HasContext(string context) => _contexts.ContainsKey(context);

    /// <summary>Whether the contexts under <paramref name="context"/> go quiet while it is on.</summary>
    public bool Blocks(string context) => Context(context).Blocks;

    /// <summary>
    /// What <paramref name="context"/> binds now, by action name. A view that follows every
    /// <see cref="Rebind"/> and <see cref="Load"/>. The list for an action is never edited, only
    /// replaced.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Binding>> BindingsIn(string context) =>
        new ReadOnlyDictionary<string, IReadOnlyList<Binding>>(_bound[Context(context).Name]);

    /// <summary>
    /// The action that already has <paramref name="control"/> in <paramref name="context"/>, or
    /// null. <paramref name="slot"/> is the binding of <paramref name="action"/> that is about to
    /// take <paramref name="control"/>. It is not a clash with itself. A slot that has the control
    /// already is not a clash either, even when another action shares it, because nothing new is
    /// taken.
    /// </summary>
    /// <remarks>
    /// Only the same control counts. A whole stick and the axes it is made of are different
    /// controls, and so are a trigger read as a button and as an axis.
    /// </remarks>
    public string? Clash(string action, Control control, string context, int slot = 0)
    {
        Slots(context, action);
        return Holders(context, control, action, slot).FirstOrDefault()?.Action;
    }

    /// <summary>
    /// Points binding number <paramref name="slot"/> of <paramref name="action"/> at
    /// <paramref name="control"/>. The slot may be one past the last binding, which adds one. The
    /// binding keeps the scale and half the slot had, unless <paramref name="scale"/> or
    /// <paramref name="part"/> say otherwise. A new binding starts at a scale of 1 on x.
    /// </summary>
    /// <remarks>
    /// When another binding in <paramref name="context"/> has the control, this throws a
    /// <see cref="BindingClashException"/> unless <paramref name="swap"/> is set, which gives that
    /// binding the control the slot had. A swap needs exactly one other binding to trade with.
    /// Throws an <see cref="ArgumentException"/> for a scale that is not a finite number.
    /// </remarks>
    // F2: the Dart named parameters count as the options object.
    public void Rebind(
        string action,
        Control control,
        string context,
        int slot = 0,
        bool swap = false,
        double? scale = null,
        AxisPart? part = null)
    {
        var slots = Slots(context, action);
        if (slot < 0 || slot > slots.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Invalid value: Not in inclusive range 0..{slots.Count}");
        }

        if (scale is { } given && !double.IsFinite(given))
        {
            throw new ArgumentException("A scale must be finite.", nameof(scale));
        }

        RequireAccepts(action, control);
        var holders = Holders(context, control, action, slot);
        if (holders.Count == 0)
        {
            Put(context, action, slot, control, scale, part);
            return;
        }

        if (!swap || holders.Count > 1)
        {
            throw new BindingClashException(holders[0].Action, control);
        }

        if (slot == slots.Count)
        {
            throw new ArgumentException("A new binding has nothing to swap.", nameof(slot));
        }

        var holder = holders[0];
        var taken = slots[slot].Control;
        RequireAccepts(holder.Action, taken);
        Put(context, holder.Action, holder.Slot, taken);
        Put(context, action, slot, control, scale, part);
    }

    /// <summary>
    /// The controls the player changed, as JSON. A changed action is written as one entry per
    /// slot. A slot still at its default is null. A changed slot is its control, with a
    /// <c>scale</c> or a <c>part</c> beside it when the binding does not have the one the default
    /// slot would have kept.
    /// </summary>
    public OrderedDictionary<string, object?> ToJson()
    {
        var contexts = new OrderedDictionary<string, object?>();
        foreach (var (context, actions) in _bound)
        {
            var changed = Changed(context, actions);
            if (changed.Count > 0)
            {
                contexts[context] = changed;
            }
        }

        return new OrderedDictionary<string, object?> { ["contexts"] = contexts };
    }

    /// <summary>
    /// Replaces the changes with those in <paramref name="json"/>, as <see cref="ToJson"/> wrote
    /// them. Names the game no longer has are skipped, and so are controls an action can no longer
    /// take or this build cannot read, so an old profile loads into a newer game. A profile that is
    /// not made of objects and lists throws a <see cref="FormatException"/> and changes nothing.
    /// </summary>
    public void Load(object? json)
    {
        var overrides = Parse(json);
        var next = Defaults();
        foreach (var (context, actions) in overrides)
        {
            foreach (var (action, slots) in actions)
            {
                Override(next, context, action, slots);
            }
        }

        Replace(next);
    }

    private InputContext Context(string context) =>
        _contexts.GetValueOrDefault(context)
        ?? throw new ArgumentException($"There is no context called \"{context}\".", nameof(context));

    // An action the context does not mention has no bindings there yet, which is not the same as
    // an action that does not exist.
    private IReadOnlyList<Binding> Slots(string context, string action) =>
        _bound[Context(context).Name].GetValueOrDefault(action)
        ?? (_kinds.ContainsKey(action)
            ? []
            : throw new ArgumentException($"There is no action called \"{action}\".", nameof(action)));

    private void Check(InputContext context)
    {
        foreach (var (action, slots) in context.Bindings)
        {
            if (!_kinds.ContainsKey(action))
            {
                throw new ArgumentException($"\"{context.Name}\" binds \"{action}\", which is not an action.", nameof(context));
            }

            foreach (var binding in slots)
            {
                RequireAccepts(action, binding.Control);
                if (!double.IsFinite(binding.Scale))
                {
                    throw new ArgumentException($"A binding of \"{action}\" has no usable scale.", nameof(context));
                }
            }
        }
    }

    private void RequireAccepts(string action, Control control)
    {
        if (!_kinds[action].Accepts(control))
        {
            throw new ArgumentException($"\"{action}\" cannot take {control}.", nameof(control));
        }
    }

    private Dictionary<string, OrderedDictionary<string, IReadOnlyList<Binding>>> Defaults() =>
        _contexts.Values.ToDictionary(
            context => context.Name,
            context => new OrderedDictionary<string, IReadOnlyList<Binding>>(context.Bindings));

    // In place, so the views BindingsIn handed out follow a load. A list that comes out the same
    // stays the list it was, because a new list reads as a new meaning and cuts a held button.
    private void Replace(Dictionary<string, OrderedDictionary<string, IReadOnlyList<Binding>>> next)
    {
        foreach (var (context, actions) in next)
        {
            var held = _bound[context];
            foreach (var action in held.Keys.Where(name => !actions.ContainsKey(name)).ToList())
            {
                held.Remove(action);
            }

            foreach (var (action, slots) in actions)
            {
                if (!SameBindings(held.GetValueOrDefault(action), slots))
                {
                    held[action] = slots;
                }
            }
        }
    }

    private List<Holder> Holders(string context, Control control, string action, int slot)
    {
        var own = _bound[context].GetValueOrDefault(action) ?? [];
        if (slot < own.Count && own[slot].Control.Equals(control))
        {
            return [];
        }

        return
        [
            .. from entry in _bound[context]
               from index in Enumerable.Range(0, entry.Value.Count)
               where entry.Value[index].Control.Equals(control) && !(entry.Key == action && index == slot)
               select new Holder(entry.Key, index),
        ];
    }

    // Lists are replaced rather than edited, so a list handed out by BindingsIn never changes
    // under its reader.
    private void Put(string context, string action, int slot, Control control, double? scale = null, AxisPart? part = null)
    {
        var slots = Slots(context, action).ToList();
        if (slot == slots.Count)
        {
            slots.Add(new Binding(control, scale ?? 1, part ?? AxisPart.X));
        }
        else
        {
            slots[slot] = slots[slot].WithControl(control, scale, part);
        }

        _bound[context][action] = slots.AsReadOnly();
    }

    private OrderedDictionary<string, object?> Changed(string context, OrderedDictionary<string, IReadOnlyList<Binding>> actions)
    {
        var changed = new OrderedDictionary<string, object?>();
        foreach (var (action, slots) in actions)
        {
            var was = _contexts[context].Bindings.GetValueOrDefault(action) ?? [];
            var changes = Changes(slots, was);
            if (changes.Any(change => change is not null))
            {
                changed[action] = changes;
            }
        }

        return changed;
    }

    private void Override(
        Dictionary<string, OrderedDictionary<string, IReadOnlyList<Binding>>> next,
        string context,
        string action,
        List<Slot?> slots)
    {
        if (!next.TryGetValue(context, out var actions) || !_kinds.TryGetValue(action, out var kind))
        {
            return;
        }

        // A control the action cannot take leaves its slot at the default, like one that could not
        // be read.
        var fitting = slots.Select(slot => slot is { } found && kind.Accepts(found.Control) ? slot : null).ToList();
        actions[action] = Merge(actions.GetValueOrDefault(action) ?? [], fitting);
    }

    private static bool SameBindings(IReadOnlyList<Binding>? a, IReadOnlyList<Binding> b) =>
        a is not null && a.Count == b.Count && Enumerable.Range(0, b.Count).All(i => Same(a[i], b[i]));

    // Shaping is left out. A rebind and a load carry it over as it was.
    private static bool Same(Binding a, Binding b) =>
        a.Control.Equals(b.Control) && a.Scale == b.Scale && a.Part == b.Part;

    private static List<object?> Changes(IReadOnlyList<Binding> now, IReadOnlyList<Binding> was) =>
        [.. Enumerable.Range(0, now.Count).Select(i => Change(now[i], i < was.Count ? was[i] : null))];

    // What the slot would keep through a rebind to the same control is left out, so a profile
    // says only what the player chose.
    private static OrderedDictionary<string, object?>? Change(Binding now, Binding? was)
    {
        if (was is not null && Same(now, was))
        {
            return null;
        }

        var kept = (was ?? new Binding(now.Control)).WithControl(now.Control);
        var json = new OrderedDictionary<string, object?>(now.Control.ToJson());
        if (now.Scale != kept.Scale)
        {
            json["scale"] = now.Scale;
        }

        if (now.Part != kept.Part)
        {
            json["part"] = EnumNames.Of(now.Part);
        }

        return json;
    }

    // A null slot leaves the binding as it is. Slots past the last binding are added, and a null
    // past the last binding adds nothing, so the slots after it move up.
    private static ReadOnlyCollection<Binding> Merge(IReadOnlyList<Binding> bindings, List<Slot?> slots)
    {
        var merged = new List<Binding>();
        for (var i = 0; i < Math.Max(bindings.Count, slots.Count); i++)
        {
            var slot = i < slots.Count ? slots[i] : null;
            var binding = i < bindings.Count ? bindings[i] : null;
            if (slot is { } found)
            {
                merged.Add((binding ?? new Binding(found.Control)).WithControl(found.Control, found.Scale, found.Part));
            }
            else if (binding is not null)
            {
                merged.Add(binding);
            }
        }

        return merged.AsReadOnly();
    }

    private static OrderedDictionary<string, OrderedDictionary<string, List<Slot?>>> Parse(object? json)
    {
        var contexts = AsMap(AsMap(json, "The profile").GetValueOrDefault("contexts") ?? new Dictionary<string, object?>(), "\"contexts\"");
        var parsed = new OrderedDictionary<string, OrderedDictionary<string, List<Slot?>>>();
        foreach (var (context, actions) in contexts)
        {
            var byAction = new OrderedDictionary<string, List<Slot?>>();
            foreach (var (action, slots) in AsMap(actions, $"\"{context}\""))
            {
                byAction[action] = SlotList(slots, action);
            }

            parsed[context] = byAction;
        }

        return parsed;
    }

    private static IReadOnlyDictionary<string, object?> AsMap(object? value, string what) =>
        value as IReadOnlyDictionary<string, object?> ?? throw new FormatException($"{what} should be an object.");

    private static List<Slot?> SlotList(object? value, string action) =>
        value is IReadOnlyList<object?> list
            ? [.. list.Select(Readable)]
            : throw new FormatException($"\"{action}\" should be a list of controls.");

    // A slot this build cannot read, such as a button a later engine dropped, leaves the binding at
    // the default. Refusing it would cost the player the whole profile.
    private static Slot? Readable(object? json)
    {
        if (json is not IReadOnlyDictionary<string, object?> map
            || !TryScale(map, out var scale)
            || !TryPart(map, out var part))
        {
            return null;
        }

        var rest = new OrderedDictionary<string, object?>(map);
        rest.Remove("scale");
        rest.Remove("part");
        return Control.Read(rest) is { } control ? new Slot(control, scale, part) : null;
    }

    private static bool TryScale(IReadOnlyDictionary<string, object?> map, out double? scale)
    {
        scale = JsonNumber.Of(map.GetValueOrDefault("scale"));
        return !map.ContainsKey("scale") || scale is { } number && double.IsFinite(number);
    }

    private static bool TryPart(IReadOnlyDictionary<string, object?> map, out AxisPart? part)
    {
        part = map.GetValueOrDefault("part") is string name ? EnumNames.Find<AxisPart>(name) : null;
        return !map.ContainsKey("part") || part is not null;
    }

    private readonly record struct Slot(Control Control, double? Scale, AxisPart? Part);

    private sealed record Holder(string Action, int Slot);
}

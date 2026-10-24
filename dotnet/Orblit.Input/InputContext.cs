using System.Collections.ObjectModel;

namespace Orblit.Input;

/// <summary>A set of bindings that is switched on and off as a whole: gameplay, a menu, a vehicle.</summary>
public sealed class InputContext
{
    public InputContext(string name, IReadOnlyDictionary<string, IReadOnlyList<Binding>> bindings, bool blocks = false)
    {
        Name = name;
        Blocks = blocks;
        var copy = new OrderedDictionary<string, IReadOnlyList<Binding>>();
        foreach (var (action, list) in bindings)
        {
            copy[action] = list.ToList().AsReadOnly();
        }

        Bindings = new ReadOnlyDictionary<string, IReadOnlyList<Binding>>(copy);
    }

    public string Name { get; }

    /// <summary>The default bindings, by action name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<Binding>> Bindings { get; }

    /// <summary>
    /// Whether the contexts under this one go quiet while it is on. A menu blocks, so the jump key
    /// does not jump behind it. A vehicle does not, so the pause key still works.
    /// </summary>
    public bool Blocks { get; }
}

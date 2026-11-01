namespace Orblit.Motion;

/// <summary>A requested run of leaf states, in the order they are to be entered.</summary>
public sealed class BlendRoute(IEnumerable<string> states)
{
    public IReadOnlyList<string> States { get; } = states.ToList().AsReadOnly();
}

namespace Orblit.Editor.App.Shell;

/// <summary>The places whose groups are collapsed to the rails, and what was collapsed before they all were.</summary>
internal sealed class CollapsedSlots(params DockSlot[] collapsed)
{
    readonly HashSet<DockSlot> _now = [.. collapsed];
    HashSet<DockSlot> _before = [.. Enum.GetValues<DockSlot>()];

    public bool Has(DockSlot slot) => _now.Contains(slot);

    public void Collapse(DockSlot slot) => _now.Add(slot);

    public void Open(DockSlot slot) => _now.Remove(slot);

    public void OpenAll() => _now.Clear();

    /// <summary>Collapses every place, and keeps what was collapsed for <see cref="BringBack"/>.</summary>
    public void CollapseAll()
    {
        _before = [.. _now];
        _now.UnionWith(Enum.GetValues<DockSlot>());
    }

    /// <summary>Opens what was open before <see cref="CollapseAll"/>, or everything when that would open nothing.</summary>
    /// <param name="taken">The places that have a group to show.</param>
    public void BringBack(IEnumerable<DockSlot> taken)
    {
        var back = taken.Any(slot => !_before.Contains(slot));
        _now.Clear();
        if (back)
        {
            _now.UnionWith(_before);
        }
    }
}

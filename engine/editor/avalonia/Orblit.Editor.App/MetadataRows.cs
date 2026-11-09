using Avalonia.Controls;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>The metadata rows of the selected entity, one under another. With none there is no height.</summary>
internal sealed class MetadataRows : StackPanel
{
    List<MetaEntry> _shown = [];

    /// <summary>Raised with the name and the new value.</summary>
    public event Action<string, object>? Entered;

    public event Action<string>? Removed;

    internal IEnumerable<MetadataRow> Rows => Children.OfType<MetadataRow>();

    /// <summary>
    /// Replaces the rows with one for each entry. Entries the rows already show leave them alone, so a box that has
    /// the typing focus keeps it after its own edit.
    /// </summary>
    public void Show(IReadOnlyList<MetaEntry> entries)
    {
        if (entries.SequenceEqual(_shown))
        {
            return;
        }
        _shown = [.. entries];
        Children.Clear();
        for (var at = 0; at < entries.Count; at++)
        {
            Children.Add(RowAt(at));
        }
    }

    MetadataRow RowAt(int at)
    {
        var name = _shown[at].Name;
        var row = new MetadataRow(_shown[at]);
        row.Entered += value =>
        {
            _shown[at] = new MetaEntry(name, value);
            Entered?.Invoke(name, value);
        };
        row.Removed += () => Removed?.Invoke(name);
        return row;
    }
}

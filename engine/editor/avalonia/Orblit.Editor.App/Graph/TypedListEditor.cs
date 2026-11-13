using Avalonia;
using Avalonia.Controls;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// A list of names that each have a type, such as the arguments of a function. Every edit hands the whole new list
/// to the callback. A row drags onto the graph when the list has a drop kind.
/// </summary>
internal sealed class TypedListEditor : StackPanel
{
    readonly string _id;
    readonly IReadOnlyList<GraphTyped> _items;
    readonly Action<IReadOnlyList<GraphTyped>> _commit;

    public TypedListEditor(string id, IReadOnlyList<GraphTyped> items, GraphDropKind? drag, Action<IReadOnlyList<GraphTyped>> commit)
    {
        _id = id;
        _items = items;
        _commit = commit;
        Spacing = 2;
        for (var at = 0; at < items.Count; at++)
        {
            Children.Add(Row(at, drag));
        }

        Children.Add(PanelFields.Small($"{id}:add", "+ Add", () => commit([.. items, new GraphTyped(PanelFields.Unique(items.Select(item => item.Name), "value"), 0)])));
    }

    Grid Row(int at, GraphDropKind? drag)
    {
        var item = _items[at];
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 4 };
        var cells = new List<Control>();
        if (drag is { } kind)
        {
            cells.Add(PanelFields.Handle($"{_id}:{at}:drag", new GraphDrop(kind, item.Name)));
        }

        cells.Add(PanelFields.Box($"{_id}:{at}:name", item.Name, text => Put(at, item with { Name = text })));
        cells.Add(PanelFields.TypeBox($"{_id}:{at}:type", item.Type, type => Put(at, item with { Type = type })));
        cells.Add(PanelFields.Small($"{_id}:{at}:remove", "x", () => _commit([.. _items.Where((_, other) => other != at)])));
        // A list without handles starts at the name, so the columns shift left by one.
        var first = drag is null ? 1 : 0;
        for (var cell = 0; cell < cells.Count; cell++)
        {
            Grid.SetColumn(cells[cell], first + cell);
            row.Children.Add(cells[cell]);
        }

        return row;
    }

    void Put(int at, GraphTyped item) => _commit([.. _items.Select((existing, other) => other == at ? item : existing)]);
}

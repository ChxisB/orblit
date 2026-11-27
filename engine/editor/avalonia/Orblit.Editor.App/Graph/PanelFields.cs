using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The small controls the script panels and the step inspector are made of. Each takes an id, which it keeps in
/// <see cref="Control.Tag"/> so a test can find it, and a callback for the one change a person makes to it.
/// </summary>
internal static class PanelFields
{
    /// <summary>A text box that passes its text on when Enter is pressed or focus leaves it. Escape puts back what it had.</summary>
    public static TextBox Box(string id, string text, Action<string> commit, string? hint = null)
    {
        var box = new TextBox { Text = text, Tag = id, MinWidth = 60, PlaceholderText = hint, FontSize = 12 };
        var done = false;
        void Finish()
        {
            // A commit can rebuild the panel, which takes focus from the old box and would commit it again.
            var changed = !done && (box.Text ?? "") != text;
            done = true;
            if (changed)
            {
                commit(box.Text ?? "");
            }
        }

        box.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                box.Text = text;
            }

            if (e.Key is Key.Enter or Key.Escape)
            {
                Finish();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        box.LostFocus += (_, _) => Finish();
        return box;
    }

    /// <summary>The plain words of a type. A type with no plain words goes by its engine name.</summary>
    public static string TypeWords(int type) => type switch
    {
        1 => "Yes or no",
        2 => "Whole number",
        3 => "Number",
        4 => "Text",
        _ => VariantTypes.NameOf(type),
    };

    /// <summary>A list of the Variant types by their plain words, each with its engine name beside it in dim mono.</summary>
    public static ComboBox TypeList() => new()
    {
        ItemsSource = Enumerable.Range(0, VariantTypes.Names.Count).ToList(),
        ItemTemplate = new FuncDataTemplate<int>((type, _) =>
        {
            var words = TypeWords(type);
            var engine = VariantTypes.NameOf(type);
            return Parts.Noted(words, null, words == engine ? null : engine);
        }),
        SelectedIndex = 0,
    };

    /// <summary>A list of the Variant types, with the type of the id shown. The callback gets the type number.</summary>
    public static ComboBox TypeBox(string id, int type, Action<int> commit)
    {
        var box = TypeList();
        box.SelectedIndex = type;
        box.Tag = id;
        box.MinWidth = 90;
        box.FontSize = 12;
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex >= 0 && box.SelectedIndex != type)
            {
                commit(box.SelectedIndex);
            }
        };
        return box;
    }

    public static CheckBox Check(string id, string label, bool isChecked, Action<bool> commit)
    {
        var box = new CheckBox { Content = label, IsChecked = isChecked, Tag = id, FontSize = 12 };
        box.IsCheckedChanged += (_, _) => commit(box.IsChecked == true);
        return box;
    }

    public static Button Small(string id, string label, Action click)
    {
        var button = new Button { Content = label, Tag = id, Padding = new Thickness(Gaps.Item, Gaps.Joined), MinHeight = 0, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>A grip a person drags onto the canvas. The drop carries the kind and the name.</summary>
    public static TextBlock Handle(string id, GraphDrop drop)
    {
        var handle = new TextBlock { Text = "::", Tag = id, Cursor = new Cursor(StandardCursorType.SizeAll), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Gaps.Tight, 0) };
        ToolTip.SetTip(handle, "Drag onto the graph.");
        // The drag runs until the drop, so the call is not awaited here. A drag that fails has nothing to report.
        handle.PointerPressed += (_, e) => _ = DragDrop.DoDragDropAsync(e, drop.ToTransfer(), DragDropEffects.Copy);
        return handle;
    }

    /// <summary>The stem alone when nothing has it, else the stem with the first number that is free.</summary>
    public static string Unique(IEnumerable<string> taken, string stem)
    {
        var used = taken.ToHashSet();
        var name = stem;
        for (var number = 2; used.Contains(name); number++)
        {
            name = stem + number;
        }

        return name;
    }
}

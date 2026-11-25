using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>A sheet of every key, a row a key under a heading for its group. Done closes it.</summary>
internal sealed class KeysSheet
{
    readonly StackPanel _rows = new() { Spacing = 2 };

    public KeysSheet()
    {
        Dialog = new Dialog("All keys", "Every shortcut in the editor.", new ScrollViewer { MaxHeight = 440, Content = _rows }) { MainText = "Done" };
        Dialog.CancelButton.IsVisible = false;
        Dialog.MainButton.Click += (_, _) => Dialog.Close();
    }

    internal Dialog Dialog { get; }

    /// <summary>Each row as it shows: the group it sits under, its name and its key.</summary>
    internal IEnumerable<(string Group, string Name, string Key)> Rows => _rows.Children.OfType<KeyRow>().Select(row => (row.Group, row.Label, row.Key));

    /// <summary>Shows these keys, grouped in the order the groups first come.</summary>
    public void Open(IEnumerable<(string Group, string Name, string Key)> keys)
    {
        _rows.Children.Clear();
        foreach (var group in keys.GroupBy(key => key.Group))
        {
            _rows.Children.Add(new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                FontSize = 10.5,
                FontWeight = FontWeight.SemiBold,
                LetterSpacing = 0.6,
                Foreground = EditorBrushes.Unit,
                Margin = new Thickness(0, _rows.Children.Count == 0 ? 0 : 10, 0, 4),
            });
            foreach (var (_, name, key) in group)
            {
                _rows.Children.Add(new KeyRow(group.Key, name, key));
            }
        }

        Dialog.Open();
    }

    sealed class KeyRow : DockPanel
    {
        public KeyRow(string group, string name, string key)
        {
            Group = group;
            Label = name;
            Key = key;
            var chip = Parts.Kbd(key);
            SetDock(chip, Dock.Right);
            Children.Add(chip);
            Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        }

        internal string Group { get; }

        internal string Label { get; }

        internal string Key { get; }
    }
}

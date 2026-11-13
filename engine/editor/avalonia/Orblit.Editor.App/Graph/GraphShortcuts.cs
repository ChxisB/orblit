using Avalonia;
using Avalonia.Input;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The commands the keyboard runs on the canvas. Each needs Control or Command held, and Shift tells a command from
/// its twin, as Z and Shift and Z do. The table is data, so the keys of the editor are listed in one place.
/// </summary>
internal sealed class GraphShortcuts(IReadOnlyDictionary<(Key Key, bool Shift), Action<Point>> commands)
{
    const KeyModifiers Primary = KeyModifiers.Control | KeyModifiers.Meta;

    /// <summary>Runs the command for the key and returns true. A key with no command returns false and changes nothing.</summary>
    public bool TryRun(Key key, KeyModifiers modifiers, Point at)
    {
        if ((modifiers & Primary) == 0 || !commands.TryGetValue((key, (modifiers & KeyModifiers.Shift) != 0), out var run))
        {
            return false;
        }

        run(at);
        return true;
    }
}

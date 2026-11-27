using Avalonia.Controls;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// Rename project: the project's name in a field. Rename writes it to the project file and raises Renamed. The
/// folder keeps its name. An empty name turns Rename off and says why.
/// </summary>
internal sealed class RenameSheet
{
    /// <summary>What Rename does, as the card menu and the dialog say.</summary>
    public const string Does = "Changes the project's name. The folder keeps its name.";

    readonly TextBox _name = new() { Classes = { EditorTheme.Field } };
    readonly TextBlock _nameFault = Parts.FaultLine();
    readonly TextBlock _fault = Parts.FaultLine();
    string _folder = "";

    public RenameSheet()
    {
        _nameFault.Text = "Give the project a name.";
        var body = Parts.Labeled("Name", _name, _nameFault, _fault, Parts.Hint("Shown in this list, in the editor and on the games you export."));
        Dialog = new Dialog("Rename project", Does, body);
        Dialog.MainButton.Content = Parts.Keyed("Rename", "↵");
        var rename = new HoverCard("Rename", "Writes the new name into the project file.", "↵");
        rename.On(Dialog.MainButton);
        rename.SaysWhyOff(Dialog.MainButton, "Give the project a name first.");
        Dialog.MainButton.Click += (_, _) => Rename();
        // TextChanged waits for a render, so the property is the one that fires at once.
        _name.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Named();
            }
        };
    }

    /// <summary>Raised once the new name is written, for the owner to show it.</summary>
    public event Action? Renamed;

    internal Dialog Dialog { get; }

    internal TextBox NameBox => _name;

    internal TextBlock NameFault => _nameFault;

    internal TextBlock Fault => _fault;

    /// <summary>Opens on the project's name, selected so typing replaces it.</summary>
    public void Open(ProjectEntry project)
    {
        _folder = project.Folder;
        _name.Text = project.Name;
        _fault.IsVisible = false;
        Dialog.Open();
        _name.Focus();
        _name.SelectAll();
    }

    void Named()
    {
        var named = (_name.Text ?? "").Trim().Length > 0;
        _name.Classes.Set(EditorTheme.Bad, !named);
        _nameFault.IsVisible = !named;
        Dialog.MainButton.IsEnabled = named;
    }

    void Rename()
    {
        // The launcher reads the file's time as when the project was last opened, and a rename is not an open.
        var file = Path.Combine(_folder, ProjectEntry.SettingsFile);
        try
        {
            var opened = File.GetLastWriteTimeUtc(file);
            ProjectSettings.SetName(_folder, (_name.Text ?? "").Trim());
            File.SetLastWriteTimeUtc(file, opened);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _fault.Text = error.Message;
            _fault.IsVisible = true;
            return;
        }

        Dialog.Close();
        Renamed?.Invoke();
    }
}

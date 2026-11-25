using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Launcher;

/// <summary>
/// Settings: the folder New project puts each new project in, and the way to the sheet of every key. Done saves
/// the folder and closes it. Escape and Cancel leave the folder as it was.
/// </summary>
internal sealed class SettingsSheet
{
    /// <summary>What Settings holds, as the cards and menu notes that open it say.</summary>
    public const string Holds = "Where new projects go, and every key.";

    readonly EditorSettings _settings;
    readonly TextBox _folder = new() { PlaceholderText = EditorSettings.DefaultProjectsFolder, FontFamily = EditorTheme.Mono, Classes = { EditorTheme.Field } };
    readonly Button _choose = new() { Content = "Choose…", Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center, Classes = { EditorTheme.TextButton, EditorTheme.Small } };
    readonly TextBlock _fault = Parts.FaultLine();

    public SettingsSheet(EditorSettings settings)
    {
        _settings = settings;
        _folder.InnerRightContent = new HoverCard("Choose…", "Picks the folder from disk.").On(_choose);
        AllKeysButton = new HoverCard("All keys", "Every shortcut on one sheet.").On(new Button { Content = "All keys", Classes = { EditorTheme.TextButton } });
        var body = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                Parts.Labeled("New projects go in", _folder, _fault, Parts.Hint("New project puts each new folder here. Orblit makes the folder if it is not there yet.")),
                Parts.Labeled("Keys", new StackPanel { Spacing = 6, Children = { new Border { HorizontalAlignment = HorizontalAlignment.Left, Child = AllKeysButton }, Parts.Hint("Every shortcut in the editor, on one sheet.") } }),
            },
        };
        Dialog = new Dialog("Settings", Holds, body) { MainText = "Done" };
        Dialog.MainButton.Click += (_, _) => Save(() => { });
        AllKeysButton.Click += (_, _) => Save(() => KeysAsked?.Invoke());
        _choose.Click += async (_, _) =>
        {
            if (await NewProjectPanel.PickFolderAsync(_folder, "Choose where new projects go") is { } folder)
            {
                _folder.Text = HomeFolder.Shorten(folder);
            }
        };
    }

    /// <summary>Raised once Settings has saved and closed, for the owner to show the sheet of every key.</summary>
    public event Action? KeysAsked;

    internal Dialog Dialog { get; }

    internal TextBox FolderBox => _folder;

    internal Button ChooseButton => _choose;

    internal Button AllKeysButton { get; }

    internal TextBlock Fault => _fault;

    /// <summary>Opens on the saved folder, with no fault.</summary>
    public void Open()
    {
        _folder.Text = _settings.ProjectsFolder();
        _fault.IsVisible = false;
        Dialog.Open();
    }

    /// <summary>The rows Search shows for Settings. Each opens Settings on its part, and shows the value it has.</summary>
    internal IEnumerable<PickerRow> Rows() =>
    [
        new(Search.Settings, "Sliders", "New projects go in", () =>
        {
            Open();
            _folder.Focus();
        })
        { Note = "Where New project makes each folder", Key = _settings.ProjectsFolder() },
    ];

    // A blank folder saves as blank, which reads back as the default the placeholder shows.
    void Save(Action then)
    {
        try
        {
            _settings.SetProjectsFolder((_folder.Text ?? "").Trim());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _fault.Text = error.Message;
            _fault.IsVisible = true;
            return;
        }

        Dialog.Close();
        then();
    }
}

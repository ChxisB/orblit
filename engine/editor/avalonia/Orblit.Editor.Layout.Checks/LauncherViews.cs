using Avalonia.Controls;
using Avalonia.Threading;
using Orblit.Editor.App.Launcher;
using Orblit.Editor.App.Shell;
using Orblit.Editor.App.Tests;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.Layout.Checks;

/// <summary>The launcher and its dialogs, on a list of projects kept in a folder of its own, at its smallest and a large size.</summary>
internal static class LauncherViews
{
    // The smallest launcher window, and a common large screen.
    static readonly (int Width, int Height)[] s_sizes = [(820, 560), (1920, 1080)];

    static readonly (string Name, bool Listed, Action<LauncherWindow> Show)[] s_views =
    [
        ("Projects", true, _ => { }),
        ("No projects", false, _ => { }),
        ("New project", true, launcher => Click(launcher.NewButton)),
        ("Settings", true, launcher => launcher.OpenSettings()),
        ("All keys", true, launcher => Then(launcher.OpenSettings, () => Click(launcher.Settings.AllKeysButton))),
        ("Rename", true, launcher => launcher.Rename.Open(launcher.Shown.First())),
    ];

    public static IEnumerable<(string Name, IReadOnlyList<string> Faults)> All(Look look) =>
        from size in s_sizes from view in s_views select Run(look, size, view);

    static (string, IReadOnlyList<string>) Run(Look look, (int Width, int Height) size, (string Name, bool Listed, Action<LauncherWindow> Show) view)
    {
        var root = Path.Combine(Path.GetTempPath(), $"orblit_layout_{Guid.NewGuid():N}");
        try
        {
            var list = new ProjectList(Path.Combine(root, "data", "projects.cfg"));
            if (view.Listed)
            {
                List(list, root);
            }

            var launcher = new LauncherWindow(list, _ => { }, time: new ManualTime()) { Width = size.Width, Height = size.Height };
            launcher.Show();
            Dispatcher.UIThread.RunJobs();
            var label = $"{size.Width}x{size.Height} launcher {view.Name}";
            var faults = look.Try(launcher, label, () => view.Show(launcher));
            Shut(launcher);
            return (label, faults);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    // Three projects, one with a name and a folder longer than a card.
    static void List(ProjectList list, string root)
    {
        (string Folder, string Name)[] projects =
        [
            ("crate", "Crate"),
            ("runner", "Runner"),
            ("a_project_folder_with_a_very_long_name_that_keeps_going", "A project with a very long name that keeps going"),
        ];
        foreach (var (folder, name) in projects)
        {
            var path = Path.Combine(root, folder);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "project.orblit"), $"config/name=\"{name}\"\nconfig/tags=PackedStringArray(\"3D\", \"C#\")\n");
            list.Add(path);
        }
    }

    static void Then(Action first, Action second)
    {
        first();
        second();
    }

    static void Shut(LauncherWindow launcher)
    {
        foreach (var dialog in ((Panel)launcher.Content!).Children.OfType<Dialog>())
        {
            dialog.Close();
        }

        launcher.Close();
    }
}

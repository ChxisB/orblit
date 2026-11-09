using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

/// <summary>The editor window at the size of the reference screen, and the helpers that measure it.</summary>
internal static class ReferenceWindow
{
    // The reference editor window, measured on a 3440 by 1279 screen.
    const double Width = 3440;
    const double Height = 1279;

    public static EditorWindow OpenAtReferenceSize() => OpenAtReferenceSize(new FakeScene());

    public static EditorWindow OpenAtReferenceSize(FakeScene scene, Func<CancellationToken, Task<string>>? readStore = null)
    {
        var window = new EditorWindow(readStore: readStore) { Width = Width, Height = Height };
        window.Show();
        window.Load(scene);
        window.UpdateLayout();
        return window;
    }

    public static Rect BoxOf(Visual visual, Visual window) => new(visual.TranslatePoint(new Point(0, 0), window)!.Value, visual.Bounds.Size);

    public static void AssertBox(double x, double y, double width, double height, Rect actual)
    {
        Assert.Equal(new Rect(x, y, width, height), actual);
    }

    // Layout rounds to whole pixels, and a half or a third of a size is not one.
    /// <summary>The icon a tree row draws for its entry. The arrow in front of it is an icon too.</summary>
    public static EditorIcon RowIconOf(Visual item) =>
        item.GetVisualDescendants().OfType<EditorIcon>().First(icon => icon.FindAncestorOfType<ToggleButton>() is null);

    public static void Near(double expected, double actual) => Assert.InRange(actual, expected - 1, expected + 1);

    // Raises the click event only, so the handlers on it run and the control's own press code does not.
    public static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    // Duplicates the first entity under the root, which is an edit the file does not hold yet.
    public static void MakeAnEdit(EditorWindow window)
    {
        window.Outline.SelectedItem = window.Outline.ItemsSource!.Cast<OutlineNode>().Single().Children[0];
        Click(window.Tools.DuplicateButton);
    }

    // A real press and release, so the control runs its own click code and not only the handlers on the event.
    public static void ClickOn(Control control, EditorWindow window)
    {
        var centre = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
    }

    // Every row of the file tree, parents before their children, including the rows of closed folders.
    public static FileNode[] FileNodes(EditorWindow window) => [.. Flatten((IEnumerable<FileNode>)window.Scenes.Tree.ItemsSource!)];

    // Selects the scene's row and presses Enter, as a user does to open it.
    public static void OpenScene(EditorWindow window, string path)
    {
        window.Scenes.Tree.SelectedItem = FileNodes(window).Single(node => node.Path == path);
        window.Scenes.Tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
    }

    static IEnumerable<FileNode> Flatten(IEnumerable<FileNode> nodes) => nodes.SelectMany(node => Flatten(node.Children).Prepend(node));
}

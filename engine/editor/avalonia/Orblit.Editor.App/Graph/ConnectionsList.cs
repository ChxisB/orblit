using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>One step that names a signal, in the function that holds it. Verb says what the step does with the signal.</summary>
internal sealed record SignalLink(string Signal, string Function, int Step, string Verb);

/// <summary>
/// Each signal of a script with the steps that use it, so a person sees who sends a signal and who hears it.
/// A row passes its function and step to the jump it was given.
/// </summary>
internal sealed class ConnectionsList : StackPanel
{
    const string NoSignal = "(no signal)";

    // The kinds that name a signal in their signal parameter, and what each does with it.
    static readonly Dictionary<string, string> s_verbs = new()
    {
        ["emit"] = "emits",
        ["emit_on"] = "emits on",
        ["signal_received"] = "listens for",
        ["connect"] = "connects to",
        ["await_signal"] = "waits for",
    };

    public ConnectionsList(GraphDocument document, Action<string, int> jump)
    {
        Spacing = 2;
        var links = Find(document).ToLookup(link => link.Signal);
        var names = document.Signals.Select(signal => signal.Name).Concat(links.Select(group => group.Key)).Distinct().ToList();
        if (names.Count == 0)
        {
            Children.Add(Note("connections:empty", "No signals."));
        }

        foreach (var name in names)
        {
            AddSignal(name, links[name].ToList(), jump);
        }
    }

    /// <summary>
    /// Every step of every function that names a signal, by signal, then function, then step.
    /// shortcut: a connect or await step with no target wired is taken to name a signal of the owner. Read the
    /// target when the editor can tell which object a wire leads to.
    /// </summary>
    public static IReadOnlyList<SignalLink> Find(GraphDocument document) =>
        [.. document.Functions
            .SelectMany(function => function.Steps
                .Where(step => s_verbs.ContainsKey(step.Kind))
                .Select(step => new SignalLink(SignalOf(step), function.Name, step.Id, s_verbs[step.Kind])))
            .OrderBy(link => link.Signal, StringComparer.Ordinal)
            .ThenBy(link => link.Function, StringComparer.Ordinal)
            .ThenBy(link => link.Step)];

    /// <summary>The control with this id, such as <c>connection:main:4</c>. Null when the list has none.</summary>
    internal T? Find<T>(string id) where T : Control => this.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    // A signal parameter is a string or a string name, as in "scored" or &"scored".
    static string SignalOf(GraphStep step) => VariantText.Unquote(step.Parameters.GetValueOrDefault("signal", "").TrimStart('&'));

    void AddSignal(string name, IReadOnlyList<SignalLink> links, Action<string, int> jump)
    {
        var shown = name == "" ? NoSignal : name;
        Children.Add(new TextBlock { Text = shown, Tag = $"connections:{name}", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        if (links.Count == 0)
        {
            Children.Add(Note($"connections:{name}:none", "No links."));
        }

        foreach (var link in links)
        {
            var row = PanelFields.Small($"connection:{link.Function}:{link.Step}", $"{link.Function}: {link.Verb} {shown}", () => jump(link.Function, link.Step));
            row.HorizontalAlignment = HorizontalAlignment.Left;
            row.Margin = new Thickness(16, 0, 0, 0);
            Children.Add(row);
        }
    }

    static TextBlock Note(string id, string text) => new() { Text = text, Tag = id, FontSize = 12, Margin = new Thickness(16, 0, 0, 0), Opacity = 0.7 };
}

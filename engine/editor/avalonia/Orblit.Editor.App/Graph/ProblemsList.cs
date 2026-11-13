using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>One thing wrong with the script. A step of 0 means the problem is not on a step.</summary>
internal sealed record Problem(bool IsError, string Function, int Step, string Text);

/// <summary>
/// The errors and warnings of a script, errors first. A problem on a step is a row that passes its function and
/// step to the jump it was given.
/// </summary>
internal sealed class ProblemsList : StackPanel
{
    const string UnknownStep = "Unknown step";

    public ProblemsList(GraphDocument document, Action<string, int> jump)
    {
        Spacing = 2;
        var problems = Find(document);
        if (problems.Count == 0)
        {
            Children.Add(Line("problems:empty", "No problems."));
        }

        for (var at = 0; at < problems.Count; at++)
        {
            Children.Add(Row(problems[at], at, jump));
        }
    }

    /// <summary>Every problem of the script: its own error, then the errors of steps, then the warnings, each by function and step.</summary>
    public static IReadOnlyList<Problem> Find(GraphDocument document)
    {
        var errors = document.Functions
            .SelectMany(function => function.Steps
                .Where(step => !step.Known || step.Problem.Length > 0)
                .Select(step => new Problem(true, function.Name, step.Id, step.Known ? step.Problem : UnknownStep)))
            .OrderBy(problem => problem.Function, StringComparer.Ordinal)
            .ThenBy(problem => problem.Step);
        var warnings = document.Warnings
            .Select(warning => new Problem(false, warning.Function, warning.Step, warning.Text))
            .OrderBy(problem => problem.Function.Length == 0)
            .ThenBy(problem => problem.Function, StringComparer.Ordinal)
            .ThenBy(problem => problem.Step);
        var script = document.Error.Length > 0 ? new[] { new Problem(true, "", 0, document.Error) } : [];
        return [.. script, .. errors, .. warnings];
    }

    /// <summary>The control with this id, such as <c>problem:main:4</c>. Null when the list has none.</summary>
    internal T? Find<T>(string id) where T : Control => this.GetVisualDescendants().OfType<T>().FirstOrDefault(control => (control.Tag as string) == id);

    // The jump is a short button and the reason sits under it, so a long reason wraps and is not cut off.
    static Control Row(Problem problem, int at, Action<string, int> jump)
    {
        if (problem.Step == 0)
        {
            return Line($"problem:{at}", problem.Text, problem.IsError);
        }

        var id = $"problem:{problem.Function}:{problem.Step}";
        var button = PanelFields.Small(id, $"{(problem.IsError ? "Error" : "Warning")} in {problem.Function}, step {problem.Step}", () => jump(problem.Function, problem.Step));
        button.HorizontalAlignment = HorizontalAlignment.Left;
        var row = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        row.Children.Add(button);
        row.Children.Add(Line($"{id}:text", problem.Text, problem.IsError));
        return row;
    }

    static TextBlock Line(string id, string text, bool isError = false) =>
        new() { Text = text, Tag = id, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = isError ? Brushes.IndianRed : null, Opacity = isError ? 1 : 0.85 };
}

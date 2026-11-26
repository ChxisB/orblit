using Avalonia;
using Avalonia.Headless;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Layout.Checks;

// Skia with the editor's own fonts, so every word has its true width. The window tests draw with stub
// widths and cannot see a cut word. Pass --shots and a folder to save a picture of each view.
AppBuilder.Configure<ChecksApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var at = Array.IndexOf(args, "--shots");
var look = new Look(at >= 0 && at + 1 < args.Length ? args[at + 1] : null);
var missed = SelfCheck.Missed().ToList();
if (missed.Count > 0)
{
    Console.WriteLine($"FAIL the audit missed its own broken layout: {string.Join(", ", missed)}");
    return 1;
}

var failed = 0;
var count = 0;
foreach (var (name, faults) in EditorViews.All(look).Concat(LauncherViews.All(look)))
{
    count++;
    if (faults.Count == 0)
    {
        Console.WriteLine($"PASS {name}");
        continue;
    }

    failed++;
    Console.WriteLine($"FAIL {name}: {string.Join("\n  ", faults.Prepend($"{faults.Count} faults"))}");
}

Console.WriteLine($"{count - failed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

internal sealed class ChecksApp : Application
{
    public override void Initialize() => EditorTheme.Apply(this);
}

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Orblit.Editor.Layout.Checks;

/// <summary>Lays a window out after a view is set up, finds its faults and saves its picture when asked.</summary>
internal sealed class Look(string? shots)
{
    public IReadOnlyList<string> At(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var faults = LayoutAudit.Faults(window);
        if (shots is not null)
        {
            Directory.CreateDirectory(shots);
            using var frame = window.CaptureRenderedFrame();
            frame?.Save(Path.Combine(shots, $"{string.Concat(name.Split(Path.GetInvalidFileNameChars()))}.png"), new PngBitmapEncoderOptions());
        }

        return faults;
    }

    // A view that cannot be set up is a fault of its own, so one broken view does not stop the rest.
    public IReadOnlyList<string> Try(Window window, string name, Action show)
    {
        try
        {
            show();
            return At(window, name);
        }
        catch (Exception error)
        {
            return [$"could not set up: {error.GetType().Name} {error.Message}"];
        }
    }
}

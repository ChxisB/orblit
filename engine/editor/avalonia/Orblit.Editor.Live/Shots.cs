using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Orblit.Editor.Live;

/// <summary>Saves what a run drew: a window as Avalonia painted it, engine pixels, or the desktop's own picture of a window.</summary>
internal static class Shots
{
    // A compiled tool that prints the ids of this process's on-screen windows. The run script builds it. Without it
    // there are no desktop shots, which is how a machine with no Screen Recording grant behaves anyway.
    const string WindowIdTool = "ORBLIT_LIVE_WINDOW_IDS";

    public static void Save(TopLevel top, string path)
    {
        var scale = top.RenderScaling;
        var size = new PixelSize((int)(top.Bounds.Width * scale), (int)(top.Bounds.Height * scale));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        bitmap.Render(top);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    /// <summary>Saves RGBA bytes, four a pixel with no row padding, as a PNG. Needs a started Avalonia platform.</summary>
    public static void SavePixels(string path, byte[] rgba, int width, int height)
    {
        using var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using (var locked = bitmap.Lock())
        {
            for (var y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(rgba, y * width * 4, locked.Address + y * locked.RowBytes, width * 4);
            }
        }
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    /// <summary>Takes the desktop's picture of each on-screen window of this process. Answers none when the tool is not set.</summary>
    public static async Task<IReadOnlyList<string>> OfProcess(string folder, string name)
    {
        if (Environment.GetEnvironmentVariable(WindowIdTool) is not { Length: > 0 } tool)
        {
            return [];
        }
        var saved = new List<string>();
        foreach (var id in (await Run(tool, Environment.ProcessId.ToString())).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var path = Path.Combine(folder, $"{name}-{id.Trim()}.png");
            await Run("/usr/sbin/screencapture", "-x", "-o", "-l", id.Trim(), path);
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                saved.Add(path);
            }
        }
        return saved;
    }

    static async Task<string> Run(string tool, params string[] arguments)
    {
        var start = new ProcessStartInfo(tool) { RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}

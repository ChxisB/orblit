using System;

namespace SurfaceSpike;

// path A readback, B native child, C shared texture.
internal sealed record Options(
    char Path, int Width, int Height, int Views, int Frames, int Warmup, bool Resize, bool Popup, string? Out, string? Shot)
{
    public static Options Parse(string[] args)
    {
        string Value(string name, string fallback)
        {
            var at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : fallback;
        }
        var pixels = Value("--pixels", "1920x1080").Split('x');
        return new Options(
            Value("--path", "C")[0],
            int.Parse(pixels[0]),
            int.Parse(pixels[1]),
            int.Parse(Value("--views", "1")),
            int.Parse(Value("--frames", "300")),
            int.Parse(Value("--warmup", "60")),
            Array.IndexOf(args, "--resize") >= 0,
            Array.IndexOf(args, "--popup") >= 0,
            Value("--out", "") is { Length: > 0 } o ? o : null,
            Value("--shot", "") is { Length: > 0 } s ? s : null);
    }
}

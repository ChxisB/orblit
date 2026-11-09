using System.Diagnostics;

namespace AvaloniaEngine;

/// <summary>Windowed engine, no Avalonia: proves the readback returns the picture, and times it.</summary>
public static class ReadbackCheck
{
    public static int Run(string library, string project, string outputPath)
    {
        var engine = Engine.Create(library,
            ["--path", project, "--resolution", "1280x720", "--rendering-driver", "metal"]);
        if (engine is null || !engine.Start())
        {
            Console.Error.WriteLine("engine did not start");
            return 1;
        }

        var readback = new ViewportReadback(engine);
        var pixels = new byte[0];
        int width = 0, height = 0;
        var iterate = new List<double>();
        var copy = new List<double>();
        for (var frame = 0; frame < 200; frame++)
        {
            var timer = Stopwatch.StartNew();
            engine.Iteration();
            var afterIteration = timer.Elapsed.TotalMilliseconds;
            var got = readback.TryRead(out width, out height, ref pixels);
            if (frame >= 20 && got)
            {
                iterate.Add(afterIteration);
                copy.Add(timer.Elapsed.TotalMilliseconds - afterIteration);
            }
        }

        File.WriteAllBytes(outputPath, pixels.AsSpan(0, width * height * 4).ToArray());
        Console.WriteLine($"size: {width}x{height}");
        Console.WriteLine($"iteration ms: {Summary(iterate)}");
        Console.WriteLine($"readback ms: {Summary(copy)}");
        readback.Close();
        engine.Dispose();
        return 0;
    }

    static string Summary(List<double> values)
    {
        values.Sort();
        var p95 = values[(int)(values.Count * 0.95)];
        return $"avg {values.Average():F2}, median {values[values.Count / 2]:F2}, p95 {p95:F2}, n {values.Count}";
    }
}

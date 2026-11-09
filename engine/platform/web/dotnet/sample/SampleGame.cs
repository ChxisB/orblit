using Orblit.Web;

// Lifts a grid of 25 cubes in a wave. One call moves all of them.
public sealed class SampleGame : WebGame
{
    private const int Side = 5;
    private const double Spacing = 1.5;

    private readonly EntityPosition[] cubes = new EntityPosition[Side * Side];
    private int orbiter;

    public override void OnStart()
    {
        for (int i = 0; i < cubes.Length; i++)
        {
            cubes[i].Handle = WebEngine.BindEntity($"Main/Cube{i}");
            cubes[i].X = (float)((i % Side - Side / 2) * Spacing);
            cubes[i].Z = (float)((i / Side - Side / 2) * Spacing);
        }
        orbiter = WebEngine.BindEntity("Main/Orbiter");
        Console.WriteLine($"sample started, {cubes.Length} cubes bound, first handle {cubes[0].Handle}");
    }

    // A released handle must go dead, a second release must do nothing, and a
    // stale handle must not move the entity that took its slot.
    private void CheckRelease()
    {
        int stale = cubes[0].Handle;
        cubes[0].Handle = 0;
        WebEngine.Release(stale);
        WebEngine.Release(stale);
        int first = WebEngine.BindEntity("Main/Marker");
        int second = WebEngine.BindEntity("Main/Marker2");
        WebEngine.SetPositions([new EntityPosition { Handle = stale, X = 99, Y = 99, Z = 99 }]);
        EntityPosition[] read = [new() { Handle = first }, new() { Handle = second }, new() { Handle = stale, X = -1 }];
        WebEngine.GetPositions(read);
        bool distinct = first != second && first != stale && second != stale;
        bool unmoved = read[0].X == 0 && read[0].Y == 0.5f && read[1].X == 2 && read[2].X == -1;
        Console.WriteLine($"release check stale={stale} first={first} second={second} distinct={distinct} unmoved={unmoved}");
    }

    public override void OnFrame(double delta)
    {
        double time = FrameCount / 60.0;
        for (int i = 0; i < cubes.Length; i++)
        {
            cubes[i].Y = (float)Math.Sin(time * 2.0 + i * 0.5);
        }
        WebEngine.SetPositions(cubes);

        if (FrameCount == 120)
        {
            CheckRelease();
        }

        if (FrameCount % 60 == 0)
        {
            // Read back from the engine, so the log shows what the engine holds.
            EntityPosition[] readBack = [cubes[0], new() { Handle = orbiter }];
            WebEngine.GetPositions(readBack);
            Console.WriteLine($"frame={FrameCount} sent y={cubes[0].Y:F3} engine y={readBack[0].Y:F3} orbiter x={readBack[1].X:F2} y={readBack[1].Y:F2} z={readBack[1].Z:F2}");
        }
    }
}

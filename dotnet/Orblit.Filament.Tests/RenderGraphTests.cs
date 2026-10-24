namespace Orblit.Filament.Tests;

/// <summary>Ported from graph_test.dart: the render graph, its schedule, its faults and its wire form.</summary>
public sealed class RenderGraphTests
{
    private const int Stride = RenderGraph.PassStride;

    private static string Said(RenderGraph graph) => string.Join(" ", graph.Problems.Select(problem => problem.What));

    private static string[] Names(RenderGraph graph) => [.. graph.Schedule.Select(pass => pass.Name)];

    private static RenderTarget Target(string name) => new() { Name = name };

    private static RenderPass Pass(string name, string? into = null, params string[] reads) =>
        new() { Name = name, Into = into, Reads = reads };

    private static RenderGraph BounceGraph(IReadOnlyList<double>? dials) => new()
    {
        Targets = [Target("frame")],
        Passes =
        [
            Pass("world", "frame"),
            new RenderPass
            {
                Name = "bounce",
                Kind = PassKind.Effect,
                Effect = PassEffect.Bounce,
                Reads = ["frame"],
                Plane = dials,
            },
        ],
    };

    [Fact]
    public void BounceReadsOneTargetAndGetsBothThePictureAndTheDepth()
    {
        // The colour and the depth of a target are two halves of one thing, so a graph names it
        // once. Making a host list it twice is how the two end up naming different targets.
        var graph = BounceGraph(null);

        Assert.Empty(graph.Problems);
        var pass = graph.Schedule[^1];
        Assert.Equal(["frame"], pass.Reads);
        Assert.Equal(PassEffect.Bounce, pass.Effect);
    }

    [Fact]
    public void ATargetBounceReadsHasToKeepItsDepth()
    {
        // Colour alone is a target the march cannot use: without depth there is no telling what
        // is in front of what, and the pass would gather light from surfaces on the far side of
        // a wall.
        var graph = new RenderGraph
        {
            Targets = [new RenderTarget { Name = "flat", Depth = false }],
            Passes =
            [
                Pass("world", "flat"),
                new RenderPass { Name = "bounce", Kind = PassKind.Effect, Effect = PassEffect.Bounce, Reads = ["flat"] },
            ],
        };

        // A bounce over a target with no depth should be reported.
        Assert.Contains("depth", Said(graph));
    }

    [Fact]
    public void BounceDialsCrossInTheOrderTheRendererReadsThem()
    {
        var packed = BounceGraph([3, 6, 0.4, 8]).PackedPasses;

        // Reach, strength, thickness and directions.
        Assert.Equal(3f, packed[Stride + 8]);
        Assert.Equal(6f, packed[Stride + 9]);
        Assert.Equal(0.4, packed[Stride + 10], 1e-6);
        Assert.Equal(8f, packed[Stride + 11]);
        Assert.Equal((float)(int)PassEffect.Bounce, packed[Stride + 12]);
    }

    [Fact]
    public void TheDefaultGraphIsTheFrameAsItWasBeforeThereWasAGraph()
    {
        var graph = RenderGraph.Standard();

        Assert.True(graph.IsRunnable);
        Assert.Equal(["scene"], Names(graph));
        Assert.Null(graph.Schedule.Single().Into);
    }

    [Fact]
    public void AnEmptyGraphIsNotABrokenOne()
    {
        // A host that never mentions a graph is not a host with a wrong graph.
        var graph = new RenderGraph();

        Assert.Empty(graph.Problems);
        Assert.Empty(graph.Schedule);
    }

    private static readonly RenderGraph Reflection = new()
    {
        Targets = [new RenderTarget { Name = "mirror", Scale = 0.5 }],
        Passes =
        [
            // Declared out of order on purpose: the frame first, then the thing it needs. An order
            // kept by hand would draw the mirror empty.
            Pass("frame", null, "mirror"),
            new RenderPass { Name = "water", Kind = PassKind.Reflection, Into = "mirror", Plane = [0, 1, 0, 0] },
        ],
    };

    [Fact]
    public void ATargetIsWrittenBeforeItIsRead()
    {
        Assert.True(Reflection.IsRunnable);
        Assert.Equal(["water", "frame"], Names(Reflection));
    }

    [Fact]
    public void TheFrameIsLastWhateverElseThereIs()
    {
        var graph = Reflection.WithTarget(Target("monitor")).With(Pass("prepass", "monitor"));

        Assert.Equal("frame", graph.Schedule[^1].Name);
        Assert.Contains("prepass", Names(graph));
    }

    [Fact]
    public void PassesThatDoNotDependOnEachOtherKeepTheirOrder()
    {
        // Two independent passes have no correct order, so the one somebody wrote down is the one
        // a capture should show.
        var graph = new RenderGraph
        {
            Targets = [Target("a"), Target("b")],
            Passes = [Pass("second", "b"), Pass("first", "a"), Pass("frame", null, "a", "b")],
        };

        Assert.Equal(["second", "first", "frame"], Names(graph));
    }

    [Fact]
    public void AChainOfThreeIsOrderedEndToEnd()
    {
        var graph = new RenderGraph
        {
            Targets = [Target("one"), Target("two")],
            Passes = [Pass("frame", null, "two"), Pass("middle", "two", "one"), Pass("first", "one")],
        };

        Assert.Equal(["first", "middle", "frame"], Names(graph));
    }

    [Fact]
    public void APassSwitchedOffIsSkippedNotScheduledAround()
    {
        var graph = new RenderGraph
        {
            Targets = [Target("mirror")],
            Passes =
            [
                new RenderPass
                {
                    Name = "water",
                    Into = "mirror",
                    Enabled = false,
                    Kind = PassKind.Reflection,
                    Plane = [0, 1, 0, 0],
                },
                Pass("frame", null, "mirror"),
            ],
        };

        // The frame still draws. Whatever the mirror held last is what it samples, which is the
        // honest behaviour for a pass somebody turned off.
        Assert.Equal(["frame"], Names(graph));
        Assert.Empty(graph.Problems);
    }

    [Fact]
    public void TwoPassesCannotBothDrawTheFrame()
    {
        var graph = new RenderGraph { Passes = [Pass("one"), Pass("two")] };

        Assert.Contains("overwrite", Said(graph));
        // Nothing runs: a graph the renderer cannot make sense of should draw what it drew last
        // rather than half of a new idea.
        Assert.Empty(graph.Schedule);
    }

    [Fact]
    public void AGraphWithNoFramePassSaysSo()
    {
        var graph = new RenderGraph { Targets = [Target("a")], Passes = [Pass("only", "a")] };

        Assert.Contains("no pass draws the frame", Said(graph));
    }

    [Fact]
    public void ATargetNobodyDeclaredIsNamedNotGuessedAt()
    {
        var graph = new RenderGraph { Passes = [Pass("frame", null, "nowhere")] };

        Assert.Equal("frame", graph.Problems[0].Pass);
        Assert.Contains("nowhere", graph.Problems[0].What);
    }

    [Fact]
    public void TwoPassesCannotWriteTheSameTarget()
    {
        var graph = new RenderGraph
        {
            Targets = [Target("a")],
            Passes = [Pass("one", "a"), Pass("two", "a"), Pass("frame", null, "a")],
        };

        Assert.Contains("already writes", Said(graph));
    }

    [Fact]
    public void APassCannotReadWhatItWrites()
    {
        var graph = new RenderGraph
        {
            Targets = [Target("a")],
            Passes = [Pass("itself", "a", "a"), Pass("frame")],
        };

        Assert.Contains("reads the target it writes", Said(graph));
    }

    [Fact]
    public void TwoPassesWaitingOnEachOtherAreNamed()
    {
        var graph = new RenderGraph
        {
            Targets = [Target("a"), Target("b")],
            Passes = [Pass("one", "a", "b"), Pass("two", "b", "a"), Pass("frame")],
        };

        var waiting = graph.Problems.Where(problem => problem.What.Contains("waits on")).Select(problem => problem.Pass);

        Assert.Contains("one", waiting);
        Assert.Contains("two", waiting);
    }

    [Fact]
    public void AReflectionWithNoPlaneIsNotAReflection()
    {
        var graph = new RenderGraph
        {
            Targets = [Target("mirror")],
            Passes =
            [
                new RenderPass { Name = "water", Kind = PassKind.Reflection, Into = "mirror" },
                Pass("frame", null, "mirror"),
            ],
        };

        Assert.Contains("no plane", graph.Problems[0].What);
    }

    [Fact]
    public void AGraphThatHasRunAwayIsRefusedRatherThanAllocated()
    {
        var graph = new RenderGraph
        {
            Passes = [.. Enumerable.Range(0, RenderGraph.MaxPasses + 1).Select(i => Pass($"pass{i}"))],
        };

        Assert.Contains("more than", graph.Problems[0].What);
    }

    [Fact]
    public void ATargetNothingReadsIsWorthSayingAndIsNotAnError()
    {
        var graph = new RenderGraph
        {
            Targets = [Target("thumbnail")],
            Passes = [Pass("shot", "thumbnail"), Pass("frame")],
        };

        Assert.Empty(graph.Problems);
        Assert.Equal(["thumbnail"], graph.UnreadTargets);
    }

    private static readonly RenderGraph Wire = new()
    {
        Targets =
        [
            new RenderTarget { Name = "mirror", Scale = 0.5, Colour = true },
            new RenderTarget { Name = "shadowless", Depth = true, Colour = false },
        ],
        Passes =
        [
            new RenderPass { Name = "frame", Reads = ["mirror", "shadowless"], Layers = 0x0F },
            Pass("prepass", "shadowless"),
            new RenderPass { Name = "water", Kind = PassKind.Reflection, Into = "mirror", Plane = [0, 1, 0, -2] },
        ],
    };

    [Fact]
    public void PassesCrossInTheOrderTheyRun()
    {
        var packed = Wire.PackedPasses;

        Assert.Equal(3 * Stride, packed.Length);
        // Targets travel as indices. The names are for people; matching strings sixty times a
        // second is matching the same strings sixty times.
        Assert.Equal(1f, packed[(0 * Stride) + 1]);
        Assert.Equal(0f, packed[(1 * Stride) + 1]);
        Assert.Equal(-1f, packed[(2 * Stride) + 1]);
    }

    [Fact]
    public void APassCarriesItsReadsItsLayersAndItsPlane()
    {
        var packed = Wire.PackedPasses;
        var frame = 2 * Stride;

        Assert.Equal(0x0F, packed[frame + 2]);
        // It reads the mirror, and the depth, and nothing else.
        Assert.Equal(0f, packed[frame + 4]);
        Assert.Equal(1f, packed[frame + 5]);
        Assert.Equal(-1f, packed[frame + 6]);

        var water = 1 * Stride;
        // The plane's normal, and its distance.
        Assert.Equal(1f, packed[water + 8 + 1]);
        Assert.Equal(-2f, packed[water + 8 + 3]);
    }

    [Fact]
    public void TargetsCarryTheirSizeAndWhatTheyKeep()
    {
        var packed = Wire.PackedTargets;
        const int stride = RenderGraph.TargetStride;

        Assert.Equal(2 * stride, packed.Length);
        Assert.Equal(0.5f, packed[(0 * stride) + 2]);
        // Depth keeps depth, and no colour.
        Assert.Equal(1f, packed[(1 * stride) + 3]);
        Assert.Equal(0f, packed[(1 * stride) + 4]);
    }

    [Fact]
    public void ACaptureSaysWhatRanAndWhatItCost()
    {
        var capture = FrameCapture.From([0.4f, 120, 2.6f, 4300, 0.2f, 8], ["prepass", "scene", "outline"]);

        Assert.Equal(["prepass", "scene", "outline"], capture.Passes.Select(pass => pass.Name));
        Assert.Equal(3.2, capture.Milliseconds, 0.0001);
        Assert.Equal(4428, capture.Draws);
        Assert.Equal("scene", capture.Slowest!.Name);
    }

    [Fact]
    public void AFrameTheRendererHasNotReportedOnYetIsEmptyNotWrong()
    {
        var capture = FrameCapture.From([], ["scene"]);

        Assert.Empty(capture.Passes);
        Assert.Null(capture.Slowest);
        Assert.Equal(0, capture.Milliseconds);
    }

    private static RenderGraph SharpenGraph() => new()
    {
        Targets = [Target("frame")],
        Passes =
        [
            Pass("world", "frame"),
            new RenderPass { Name = "sharpen", Kind = PassKind.Effect, Effect = PassEffect.Sharpen, Reads = ["frame"] },
        ],
    };

    [Fact]
    public void AnEffectNamesItselfAndEveryOtherKindSaysNone()
    {
        var packed = SharpenGraph().PackedPasses;

        // A scene pass has no effect, and must say so rather than leaving the slot at zero.
        // Zero is a real effect.
        Assert.Equal(-1f, packed[12]);
        Assert.Equal((float)(int)PassEffect.Sharpen, packed[Stride + 12]);
        Assert.Equal((float)(int)PassKind.Effect, packed[Stride]);
    }

    [Fact]
    public void AnEffectReadsTheTargetTheSceneWrote()
    {
        var packed = SharpenGraph().PackedPasses;

        // The first read is target 0, and the rest are empty.
        Assert.Equal(0f, packed[Stride + 4]);
        Assert.Equal(-1f, packed[Stride + 5]);
    }

    [Fact]
    public void TheEffectSurvivesACopy()
    {
        var pass = new RenderPass { Name = "sharpen", Kind = PassKind.Effect, Effect = PassEffect.Sharpen };

        Assert.Equal(PassEffect.Sharpen, (pass with { Name = "other" }).Effect);
    }
}

using System.Runtime.CompilerServices;
using Orblit.Core;

namespace Orblit.Native.Tests;

public sealed class ScriptTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly ITestOutputHelper _output;
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_script");
    private readonly World _world = new();
    private readonly List<string> _said = [];
    private readonly List<ScriptRunner> _runners = [];

    public ScriptTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        foreach (var runner in _runners)
        {
            runner.Dispose();
        }

        _world.Dispose();
        _root.Delete(recursive: true);
    }

    [Fact]
    public void ThereIsACompilerToTestWith() =>
        // These tests compile real C++. With no toolchain they would pass by not running.
        Assert.True(Toolchain.Find() is not null, "no C++ compiler on the PATH");

    [Fact]
    public void AScriptCompilesAndStarts()
    {
        var host = Runner();

        var built = host.Add(Write("counter", Scripts.Counter));

        Assert.True(built.Ok, built.Output);
        Assert.Contains("counter: started", _said);
    }

    [Fact]
    public void WhatItDidIsInTheWorldNotJustInWhatItPrinted()
    {
        Runner().Add(Write("counter", Scripts.Counter));

        Assert.Equal(1, _world.EntityCount);
        Assert.Equal(2, Rows("Ticks").Length);
    }

    [Fact]
    public void SteppingReachesIt()
    {
        var host = Runner();
        host.Add(Write("counter", Scripts.Counter));

        host.Step(0.5);
        host.Step(0.25);

        var ticks = Rows("Ticks");
        Assert.Equal(0.75, ticks[0], 1e-9);
        Assert.Equal(2, ticks[1]);
    }

    [Fact]
    public void AScriptThatDoesNotCompileSaysWhyAndDoesNotLoad()
    {
        var built = Runner().Add(Write("broken", "this is not C++ at all"));

        Assert.False(built.Ok);
        Assert.NotEmpty(built.Output);
        Assert.Contains("broken.cpp", built.Command);
        Assert.Equal(0, _world.EntityCount);
    }

    [Fact]
    public void ABrokenBuildLeavesTheWorkingVersionRunning()
    {
        var host = Runner();
        var source = Write("counter", Scripts.Counter);
        host.Add(source);
        host.Step(1);

        File.WriteAllText(source.FullName, "still not C++");
        Assert.False(host.Add(source).Ok);

        host.Step(1);
        Assert.Equal(2, Rows("Ticks")[1]);
    }

    [Fact]
    public void ItStaysListedWithWhatTheCompilerSaidAboutIt()
    {
        var host = Runner();
        host.Add(Write("broken", "nope"));

        var listed = Assert.Single(host.Scripts);
        Assert.Equal("broken", listed.Name);
        Assert.False(listed.Running);
        Assert.NotEmpty(listed.Output);
    }

    [Fact]
    public void NewCodeReplacesOldCode()
    {
        var host = Runner();
        var source = Write("counter", Scripts.Counter);
        host.Add(source);
        host.Step(1);

        File.WriteAllText(source.FullName, Scripts.Counter.Replace("held->calls += 1.0;", "held->calls += 10.0;"));
        Assert.True(host.Add(source).Ok);

        host.Step(1);

        // Two entities: the rebuilt script started again rather than resuming,
        // and only the new one's rows came from the new code.
        var ticks = Rows("Ticks");
        Assert.Equal(2, _world.EntityCount);
        Assert.Equal(1, ticks[1]);
        Assert.Equal(10, ticks[3]);
    }

    [Fact]
    public void EveryBuildGoesToAPathOfItsOwnAndTakesTheLastOneAway()
    {
        var host = Runner();
        var source = Write("counter", Scripts.Counter);
        host.Add(source);
        host.Add(source);
        host.Add(source);

        // A new path each time, because a loader hands back the image it already
        // has for a path it has already seen. And only the current one left on
        // disk, because the others were unloaded: an afternoon of saving would
        // otherwise leave an afternoon of libraries.
        var built = Libraries();
        Assert.Contains("counter.2", Assert.Single(built));
        Assert.Equal(2, Assert.Single(host.Scripts).Revision);
    }

    [Fact]
    public void TheOldOneIsStoppedOnce()
    {
        var host = Runner();
        var source = Write("counter", Scripts.Counter);
        host.Add(source);
        host.Add(source);

        Assert.Single(_said, line => line == "counter: stopped");
    }

    [Fact]
    public async Task SavingTheSourceRebuildsIt()
    {
        var host = Runner();
        var source = Write("counter", Scripts.Counter);
        host.Add(source);

        var rebuilt = new TaskCompletionSource<BuildResult>();
        host.Watch(source, built => rebuilt.TrySetResult(built));

        File.WriteAllText(source.FullName, Scripts.Counter.Replace("held->calls += 1.0;", "held->calls += 3.0;"));

        var built = await rebuilt.Task.WaitAsync(Patience, TestContext.Current.CancellationToken);
        Assert.True(built.Ok, built.Output);

        host.Step(1);
        Assert.Equal(3, Rows("Ticks")[3]);
    }

    [Fact]
    public void ALibraryThatIsNotAScriptIsRefusedByName()
    {
        // Compiles perfectly well. It simply is not one of ours.
        var built = Runner().Add(Write("stranger", "extern \"C\" int nothing() { return 1; }"));

        Assert.False(built.Ok);
        Assert.Contains("orblit_script_abi", built.Output);
    }

    [Fact]
    public void AScriptBuiltAgainstAnotherContractIsRefusedNotCalled()
    {
        var built = Runner().Add(Write("ancient", Scripts.Ancient));

        Assert.False(built.Ok);
        Assert.Contains("99", built.Output);
        Assert.Contains("Rebuild", built.Output);
    }

    [Fact]
    public void TheHeaderAndTheStructAgreeAboutTheTable()
    {
        // Checked by the compiler rather than asserted here: C++ is the side
        // that has to agree, and a static_assert fails the build.
        var built = Runner().Add(Write("sizes", Scripts.Sizes(Unsafe.SizeOf<ScriptHostTable>())));

        Assert.True(built.Ok, built.Output);
    }

    [Fact]
    public void TheExampleScriptBuilds()
    {
        var built = Runner().Add(new FileInfo(Path.Combine(AppContext.BaseDirectory, "example", "spinner.cpp")));

        Assert.True(built.Ok, built.Output);
        Assert.Contains("spinner: started", _said);
    }

    [Fact]
    public void AScriptReadsWhatTheEmbedderSupplies()
    {
        var host = Runner(new MapValues(
            new() { ["ball.odata/speed"] = 12.5 },
            new() { ["ball.odata/bouncy"] = true },
            new() { ["ball.odata/label"] = "Ball" }));
        host.Add(Write("reader", Scripts.Reader));
        host.Step(0.016);

        var speed = Rows("Speed");
        Assert.Equal(12.5, speed[0]);
        Assert.Equal(1, speed[1]);
        Assert.Contains("Ball", _said);
    }

    [Fact]
    public void AValueThatIsNotThereFallsBackToWhatTheScriptAskedFor()
    {
        var host = Runner(new MapValues());
        host.Add(Write("reader", Scripts.Reader));
        host.Step(0.016);

        Assert.Equal(-1.0, Rows("Speed")[0]);
    }

    [Fact]
    public void ChangingOneReachesTheNextFrameThroughTheAddress()
    {
        var values = new MapValues(new() { ["ball.odata/speed"] = 1.0 });
        var host = Runner(values);
        host.Add(Write("reader", Scripts.Reader));

        host.Step(0.016);
        Assert.Equal(1.0, Rows("Speed")[0]);

        values.Numbers["ball.odata/speed"] = 9.0;
        values.Changed();
        host.Step(0.016);

        // The script never called in for it: it holds the address and the host
        // rewrote what is there. That is what makes a value safe to read inside
        // a loop over a hundred thousand entities.
        Assert.Equal(9.0, Rows("Speed")[0]);
    }

    [Fact]
    public void AValueNobodySaidHadChangedIsNotReRead()
    {
        var values = new CountingValues();
        var host = Runner(values);
        host.Add(Write("reader", Scripts.Reader));

        // The first frame fills the addresses the script resolved.
        host.Step(0.016);
        var settled = values.Asked;
        Assert.True(settled > 0);

        for (var i = 0; i < 100; i++)
        {
            host.Step(0.016);
        }

        // A hundred more frames and not one read: refreshing costs nothing when
        // nothing has moved, which is most frames.
        Assert.Equal(settled, values.Asked);
    }

    [Fact]
    public void TheCallsStillWorkForAKeyThatIsNotAConstant()
    {
        var host = Runner(new MapValues(
            new() { ["ball.odata/speed"] = 3.5 },
            new() { ["ball.odata/bouncy"] = true },
            new() { ["ball.odata/label"] = "Ball" }));
        host.Add(Write("caller", Scripts.Caller));
        host.Step(0.016);

        Assert.Equal(3.5, Rows("Speed")[0]);
        Assert.Contains("Ball", _said);
    }

    [Fact]
    public void ATextThatChangesReplacesWhatTheAddressPointsAt()
    {
        var values = new MapValues(texts: new() { ["ball.odata/label"] = "Ball" });
        var host = Runner(values);
        host.Add(Write("reader", Scripts.Reader));

        host.Step(0.016);
        Assert.Contains("Ball", _said);

        values.Texts["ball.odata/label"] = "Crate";
        values.Changed();
        host.Step(0.016);

        Assert.Contains("Crate", _said);
    }

    [Fact]
    public void AMissingTextIsANullPointerNotAnEmptyString()
    {
        var host = Runner(new MapValues(new() { ["ball.odata/speed"] = 1.0 }));
        host.Add(Write("reader", Scripts.Reader));
        host.Step(0.016);

        // The script only logs when the pointer is non-null.
        Assert.Empty(_said);
    }

    [Fact]
    public void ReadingThroughTheAddressIsFarCheaperThanCallingIn()
    {
        var host = Runner(new MapValues(new() { ["ball.odata/speed"] = 1.0 }));

        var calling = Time(host, "calling", byAddress: false);
        var addressed = Time(host, "addressed", byAddress: true);

        _output.WriteLine(
            $"a million reads: {calling.TotalMicroseconds:F0}us calling in, {addressed.TotalMicroseconds:F0}us through the address");

        // A crossing into managed code per read against a load from memory. The
        // margin is deliberately loose: the point is the order of magnitude, and a
        // tight bound would be a test that fails on a busy machine rather than on
        // a regression.
        Assert.True(
            addressed.TotalMicroseconds * 10 < calling.TotalMicroseconds,
            "addressed reads should be at least ten times cheaper");
    }

    [Fact]
    public void RebuildingDoesNotLeakTheLibraryItReplaced()
    {
        var host = Runner();
        var source = Write("counter", Scripts.Counter);

        for (var i = 0; i < 40; i++)
        {
            var built = host.Add(source);
            Assert.True(built.Ok, built.Output);
        }

        // Forty saves, one library. Without a real unload this folder would hold
        // forty of them and the process would be mapping all forty.
        Assert.Single(Libraries());
    }

    private ScriptRunner Runner(IScriptValues? values = null)
    {
        var runner = new ScriptRunner(_world, new DirectoryInfo(Path.Combine(_root.FullName, "build")), values, onLog: _said.Add);
        _runners.Add(runner);
        return runner;
    }

    private FileInfo Write(string name, string source)
    {
        var file = new FileInfo(Path.Combine(_root.FullName, name + ".cpp"));
        File.WriteAllText(file.FullName, source);
        return file;
    }

    /// <summary>
    /// A component the C++ side registered, looked up by registering the same
    /// layout, which the core answers with the existing id.
    /// </summary>
    private ComponentType Pair(string name) => _world.RegisterComponent(name, ComponentKind.Float64, 2);

    /// <summary>The rows of a component, as the script left them.</summary>
    private double[] Rows(string name)
    {
        using var query = _world.Query(Pair(name));
        return query.Chunks.First().Float64(0).ToArray();
    }

    private string[] Libraries() =>
        Directory.GetFiles(Path.Combine(_root.FullName, "build"), "*" + Toolchain.LibrarySuffix);

    private TimeSpan Time(ScriptRunner host, string name, bool byAddress)
    {
        var built = host.Add(Write(name, Scripts.Loop(byAddress)));
        Assert.True(built.Ok, built.Output);
        host.Step(0.016); // Warm.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        host.Step(0.016);
        watch.Stop();
        host.Remove(name);
        return watch.Elapsed;
    }
}

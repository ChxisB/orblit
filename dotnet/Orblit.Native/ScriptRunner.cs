using Orblit.Core;

namespace Orblit.Native;

/// <summary>
/// The C++ scripts a world is running.
/// Compiles, loads, steps and rebuilds. Everything above this sees start, step
/// and stop; what is underneath is a compiler and a dynamic library.
/// <para>
/// <b>Reloading replaces, it does not resume.</b> A rebuilt script is a second
/// library loaded beside the first, and the old one's state goes when it is
/// stopped. State a script wants to keep across a rebuild belongs in the world,
/// where the rest of the engine can see it, and a script that kept it in a
/// static would lose it on a scene load anyway.
/// </para>
/// <para>
/// A rebuild started by <see cref="Watch"/> runs on a worker thread, so the
/// operations below take one lock. A script starts on whichever thread rebuilt
/// it, which is the caller's to allow.
/// </para>
/// </summary>
public sealed class ScriptRunner : IDisposable
{
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(150);

    private readonly Lock _gate = new();
    private readonly Action<string>? _onLog;
    private readonly ScriptHost _host;
    private readonly OrderedDictionary<string, Loaded> _scripts = [];
    private readonly List<IDisposable> _watches = [];
    private bool _disposed;

    /// <param name="world">The world the scripts run in.</param>
    /// <param name="build">
    /// Where built libraries go. Somewhere disposable: nothing here is a build
    /// artefact worth keeping, and every rebuild leaves the last one behind.
    /// </param>
    /// <param name="values">Where a script's values come from. Nothing, when null.</param>
    /// <param name="toolchain">The compiler. The first one on the PATH, when null.</param>
    /// <param name="includes">The headers a script is compiled against. <see cref="EngineIncludes"/>, when null.</param>
    /// <param name="onLog">Where a script's log and a rebuild's news go. The console, when null.</param>
    public ScriptRunner(
        World world,
        DirectoryInfo build,
        IScriptValues? values = null,
        Toolchain? toolchain = null,
        IReadOnlyList<string>? includes = null,
        Action<string>? onLog = null)
    {
        World = world;
        Build = build;
        Toolchain = toolchain ?? Toolchain.Find();
        Includes = includes ?? EngineIncludes();
        _onLog = onLog;
        _host = new ScriptHost(world, values, onLog);
    }

    public World World { get; }

    public DirectoryInfo Build { get; }

    /// <summary>
    /// Null when nothing on this machine can compile C++, which is a thing to say
    /// plainly rather than to fail at the first script.
    /// </summary>
    public Toolchain? Toolchain { get; }

    public IReadOnlyList<string> Includes { get; }

    public bool CanCompile => Toolchain is not null;

    public IReadOnlyList<ScriptStatus> Scripts
    {
        get
        {
            lock (_gate)
            {
                return
                [
                    .. _scripts.Values.Select(loaded => new ScriptStatus(
                        loaded.Name,
                        loaded.Source,
                        loaded.Script?.IsRunning ?? false,
                        loaded.Output,
                        loaded.Revision)),
                ];
            }
        }
    }

    /// <summary>
    /// The headers a script is compiled against. <c>ORBLIT_INCLUDE</c> first,
    /// then the <c>include</c> folder beside the assembly, which is where
    /// Orblit.Native ships them. A shipped game has neither, and runs scripts
    /// that were compiled before it shipped.
    /// </summary>
    public static IReadOnlyList<string> EngineIncludes()
    {
        var said = Environment.GetEnvironmentVariable("ORBLIT_INCLUDE");
        if (!string.IsNullOrEmpty(said))
        {
            return said.Split(Path.PathSeparator);
        }

        var beside = Path.Combine(AppContext.BaseDirectory, "include");
        return Directory.Exists(beside) ? [beside] : [];
    }

    /// <summary>
    /// Builds a source file, loads what comes out, and starts it. Returns what the
    /// compiler said. A script already loaded under the same name is replaced,
    /// which is what a rebuild is.
    /// </summary>
    public BuildResult Add(FileInfo source)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Toolchain is null)
            {
                return new BuildResult(
                    null,
                    "No C++ compiler found. Install the Xcode command line tools, or clang, or gcc.",
                    string.Empty);
            }

            var name = ScriptNames.Of(source.Name);
            _scripts.TryGetValue(name, out var existing);
            var revision = (existing?.Revision ?? -1) + 1;
            var built = Toolchain.Compile(source, Build, Includes, revision);

            // Kept listed with the compiler's complaint against it. Dropping it
            // would lose the one thing somebody needs to fix it.
            if (built.Library is null)
            {
                _scripts[name] = new Loaded(name, source, existing?.Script, revision, built.Output);
                return built;
            }

            NativeScript loaded;
            try
            {
                loaded = NativeScript.Open(built.Library);
            }
            catch (ScriptException error)
            {
                _scripts[name] = new Loaded(name, source, existing?.Script, revision, error.Message);
                return new BuildResult(null, error.Message, built.Command);
            }

            // The old one goes only once the new one has loaded, so a script that
            // fails to load leaves the working version running rather than
            // leaving nothing running. Closed rather than stopped: its code is
            // unloaded and its file deleted, or an afternoon of saving leaks a
            // library a save.
            existing?.Script?.Close();

            loaded.Start(_host);
            _scripts[name] = new Loaded(name, source, loaded, revision, built.Output);
            return built;
        }
    }

    /// <summary>
    /// Steps every script that is running, in the order they were added. What a
    /// script does to the process is the script's own business, and that is the
    /// trade for running native code.
    /// </summary>
    public void Step(double delta)
    {
        lock (_gate)
        {
            // Once a frame, and only when something has actually changed: a script
            // reading a value through its address needs the address to hold the
            // current value, and this is the one place that can be true without a
            // call per read.
            _host.Refresh();

            foreach (var loaded in _scripts.Values)
            {
                loaded.Script?.Step(delta);
            }
        }
    }

    /// <summary>Stops one, unloads it and forgets it.</summary>
    public void Remove(string name)
    {
        lock (_gate)
        {
            if (_scripts.Remove(name, out var loaded))
            {
                loaded.Script?.Close();
            }
        }
    }

    /// <summary>
    /// Rebuilds a script whenever its source is written. Coalesced: a save from an
    /// editor is often several events, and building three times for one save is
    /// three seconds of somebody's attention.
    /// </summary>
    public void Watch(FileInfo source, Action<BuildResult>? onBuilt = null)
    {
        var timer = new Timer(_ => Rebuild(source, onBuilt));
        // A file always has a parent folder.
        var watcher = new FileSystemWatcher(source.DirectoryName!, source.Name) { EnableRaisingEvents = true };
        FileSystemEventHandler changed = (_, _) => timer.Change(Settle, Timeout.InfiniteTimeSpan);
        watcher.Changed += changed;
        watcher.Created += changed;
        watcher.Renamed += (_, _) => timer.Change(Settle, Timeout.InfiniteTimeSpan);
        lock (_gate)
        {
            _watches.Add(timer);
            _watches.Add(watcher);
        }
    }

    /// <summary>Stops everything and frees the table.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var watch in _watches)
            {
                watch.Dispose();
            }

            _watches.Clear();
            foreach (var loaded in _scripts.Values)
            {
                loaded.Script?.Close();
            }

            _scripts.Clear();

            // After every script has stopped: the table is what they were holding.
            _host.Dispose();
        }
    }

    private void Rebuild(FileInfo source, Action<BuildResult>? onBuilt)
    {
        source.Refresh();
        if (!source.Exists)
        {
            return;
        }

        BuildResult built;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            built = Add(source);
        }

        _onLog?.Invoke(built.Ok ? $"Rebuilt {source.Name}." : $"Could not build {source.Name}:\n{built.Output}");
        onBuilt?.Invoke(built);
    }

    private sealed record Loaded(string Name, FileInfo Source, NativeScript? Script, int Revision, string Output);
}

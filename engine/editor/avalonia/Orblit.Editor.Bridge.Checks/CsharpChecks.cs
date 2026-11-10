using System.Diagnostics;

namespace Orblit.Editor.Bridge.Checks;

/// <summary>
/// Script attach against an engine that has the C# module. The module reads the project assembly when the
/// project opens, so the fixture is built first. Run it with <c>--csharp</c> and a library built with
/// <c>module_csharp_enabled=yes</c>.
/// </summary>
internal sealed class CsharpChecks : IChecks
{
    const string ProbePath = "res://Probe.cs";

    readonly string _library = EngineLibrary.Find();
    readonly string _folder = EngineProject.CopyProject("csharp_probe");
    // The module looks for its assemblies beside the host executable, which is this program and not the engine.
    readonly string? _assemblyLink;
    readonly EngineHost _host;
    readonly EditorSession _editor;

    public CsharpChecks()
    {
        var assemblies = Path.Combine(Path.GetDirectoryName(_library)!, "OrblitSharp");
        _assemblyLink = LinkAssemblies(assemblies);
        BuildProbe(Path.Combine(assemblies, "Tools", "nupkgs"));
        _host = EngineHost.Create(_library, ["--headless", "--editor", "--path", _folder]);
        _host.Start();
        var scene = new EditorInterfaceScene(_host);
        scene.PumpUntilScanned();
        _editor = new EditorSession(_host, scene);
    }

    public IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("an attached C# script is read back, saved with the scene, kept by a reopen and dropped by a clear", AttachIsSaved),
        ("a C# file that is not there is refused and leaves the entity as it was", MissingFileIsRefused),
    ];

    void AttachIsSaved()
    {
        var holder = HolderOf();
        Assert.Null(_editor.ScriptOf(holder));

        _editor.AttachScript(holder, ProbePath);

        Assert.Equal(ProbePath, _editor.ScriptOf(holder));
        _editor.Save();
        Assert.Contains("path=\"res://Probe.cs\"", SavedScene());
        Assert.Contains("script = ExtResource(", SavedScene());
        _editor.OpenScene("res://main.oscn");
        holder = HolderOf();
        Assert.Equal(ProbePath, _editor.ScriptOf(holder));

        _editor.ClearScript(holder);

        Assert.Null(_editor.ScriptOf(holder));
        _editor.Save();
        Assert.DoesNotContain("Probe.cs", SavedScene());
    }

    void MissingFileIsRefused()
    {
        var holder = HolderOf();

        Assert.Throws<IOException>(() => _editor.AttachScript(holder, "res://missing.cs"));

        Assert.Null(_editor.ScriptOf(holder));
    }

    nint HolderOf() => _editor.Outline()!.Children.Single(c => c.Name == "Holder").Handle;

    string SavedScene() => File.ReadAllText(Path.Combine(_folder, "main.oscn"));

    // Returns the link when this run made it, so only that run removes it.
    static string? LinkAssemblies(string assemblies)
    {
        var link = Path.Combine(AppContext.BaseDirectory, "OrblitSharp");
        if (Directory.Exists(link) || !Directory.Exists(assemblies))
        {
            return null;
        }
        Directory.CreateSymbolicLink(link, assemblies);
        return link;
    }

    // shortcut: finds dotnet on PATH, which the gate scripts set; upgrade when the checks run where it is not.
    void BuildProbe(string packages)
    {
        File.WriteAllText(
            Path.Combine(_folder, "nuget.config"),
            $"<configuration><packageSources><clear /><add key=\"local\" value=\"{packages}\" /></packageSources></configuration>");
        var build = Process.Start(new ProcessStartInfo("dotnet", ["build", Path.Combine(_folder, "CsharpProbe.csproj"), "--nologo"])
        {
            RedirectStandardOutput = true,
            WorkingDirectory = _folder,
        })!;
        var output = build.StandardOutput.ReadToEnd();
        build.WaitForExit();
        Assert.True(build.ExitCode == 0, output);
    }

    public void Dispose()
    {
        _host.Dispose();
        Directory.Delete(_folder, true);
        if (_assemblyLink is not null)
        {
            Directory.Delete(_assemblyLink);
        }
    }
}

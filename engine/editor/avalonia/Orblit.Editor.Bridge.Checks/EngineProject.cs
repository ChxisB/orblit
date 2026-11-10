namespace Orblit.Editor.Bridge.Checks;

/// <summary>Makes a scratch copy of a fixture project.</summary>
internal static class EngineProject
{
    // The editor writes a cache folder and may rewrite project files, so the checks work on a copy.
    public static string CopyProject(string fixture)
    {
        var target = Path.Combine(Path.GetTempPath(), $"orblit_bridge_checks_{Environment.ProcessId}");
        Directory.CreateDirectory(target);
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        }
        return target;
    }
}

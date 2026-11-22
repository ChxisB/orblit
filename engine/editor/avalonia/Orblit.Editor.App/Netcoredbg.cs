namespace Orblit.Editor.App;

/// <summary>Finds the netcoredbg the person installed. The editor ships none, so C# runs undebugged without one.</summary>
internal static class Netcoredbg
{
    /// <summary>The environment variable that names the program. It comes ahead of the path.</summary>
    public const string Setting = "ORBLIT_NETCOREDBG";

    /// <summary>Where to download netcoredbg.</summary>
    public const string Releases = "https://github.com/Samsung/netcoredbg/releases";

    static readonly string s_name = OperatingSystem.IsWindows() ? "netcoredbg.exe" : "netcoredbg";

    /// <summary>The program the setting names, or else the first netcoredbg on the path. Null when neither is there.</summary>
    public static string? Find(string? setting, string? path, Func<string, bool> exists) =>
        new[] { setting ?? "" }
            .Concat((path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(folder => Path.Combine(folder, s_name)))
            .FirstOrDefault(file => file.Length > 0 && exists(file));

    /// <summary>The netcoredbg of the editor's environment, or null.</summary>
    public static string? Find() => Find(Environment.GetEnvironmentVariable(Setting), Environment.GetEnvironmentVariable("PATH"), File.Exists);
}

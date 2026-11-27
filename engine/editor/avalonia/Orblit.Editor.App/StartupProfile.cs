using System.Runtime;

namespace Orblit.Editor.App;

/// <summary>
/// Compiling the editor's code is about half of the time before its first window. The runtime records what one
/// run compiles in this folder, and the next run compiles it on other cores meanwhile.
/// </summary>
internal static class StartupProfile
{
    public static void Start(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return; // The editor starts the same without the record, only later.
        }
        ProfileOptimization.SetProfileRoot(folder);
        ProfileOptimization.StartProfile("startup.jit");
    }
}

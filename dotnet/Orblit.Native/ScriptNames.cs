namespace Orblit.Native;

internal static class ScriptNames
{
    /// <summary>
    /// The file name without the revision or the extension, which is what
    /// somebody called the script. <c>spinner.3.dylib</c> and <c>spinner.cpp</c>
    /// are both <c>spinner</c>.
    /// </summary>
    public static string Of(string path) => Path.GetFileName(path).Split('.')[0];
}

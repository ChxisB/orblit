namespace Orblit.Asset;

/// <summary>
/// The files an importer hands a native program. The programs read and write files and an importer deals
/// in bytes, so every importer that drives one needs a directory for the length of one import and the
/// paths inside it.
/// </summary>
public static class NativeWork
{
    /// <summary>
    /// A path to <paramref name="name"/> inside <paramref name="directory"/>. Written out because the native
    /// tools take paths, every importer therefore builds some, and a separator dropped into a string
    /// interpolation is a mistake that only shows up on the platform nobody tested on.
    /// </summary>
    public static string Inside(DirectoryInfo directory, string name) => Beside(directory.FullName, name);

    /// <summary>A path to <paramref name="name"/> inside the directory at <paramref name="path"/>.</summary>
    public static string Beside(string path, string name) => $"{path}{Path.DirectorySeparatorChar}{name}";

    /// <summary>
    /// Runs <paramref name="body"/> in a directory that exists for that one import and is gone afterwards.
    /// It is here rather than repeated so that the cleanup is written once: a cook that fails partway must
    /// not leave a half-written file where the next run could pick it up.
    /// </summary>
    internal static async Task<T> WithTemporaryDirectoryAsync<T>(string prefix, Func<DirectoryInfo, Task<T>> body)
    {
        var directory = Directory.CreateTempSubdirectory($"orblit_{prefix}");
        try
        {
            return await body(directory).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                directory.Delete(recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A temporary directory that outlives the process is untidy, not broken, and the operating
                // system clears it eventually. Failing the import over it would turn a successful cook into a
                // failure.
            }
        }
    }
}

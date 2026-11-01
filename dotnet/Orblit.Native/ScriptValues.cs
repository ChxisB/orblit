namespace Orblit.Native;

/// <summary>
/// Where a script's values come from.
/// Supplied by whatever embeds the engine: the editor reads a data file, a test
/// answers from a map. The script does not know which, so the same script runs
/// in the editor and in the game.
/// </summary>
public interface IScriptValues
{
    /// <summary>
    /// Changes whenever any value here changes. It lets a script read a value
    /// through a pointer and still see an edit: the host rewrites the resolved
    /// values in place when this moves, and does nothing when it has not.
    /// </summary>
    int Revision { get; }

    double Number(string asset, string key, double fallback);

    bool Toggle(string asset, string key, bool fallback);

    /// <summary>Null when there is no such value, which the script sees as a null pointer.</summary>
    string? Text(string asset, string key);
}

/// <summary>Nothing to read. What a script gets when the embedder supplies no values.</summary>
public sealed class NoValues : IScriptValues
{
    public int Revision => 0;

    public double Number(string asset, string key, double fallback) => fallback;

    public bool Toggle(string asset, string key, bool fallback) => fallback;

    public string? Text(string asset, string key) => null;
}

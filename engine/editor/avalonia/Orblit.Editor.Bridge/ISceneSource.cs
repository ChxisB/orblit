namespace Orblit.Editor.Bridge;

/// <summary>
/// Where <see cref="EditorSession"/> finds, opens and saves the scene it edits. An editor engine and a
/// running game keep the scene in different places, so each has its own source.
/// </summary>
public interface ISceneSource
{
    /// <summary>The root of the open scene, or zero when none is open.</summary>
    nint Root();

    void Open(string resourcePath);

    /// <summary>Writes the open scene to its file. Throws when the engine cannot.</summary>
    void Save();

    /// <summary>Closes the open scene and drops its edits. Root is zero afterwards.</summary>
    void Close();
}

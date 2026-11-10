namespace Orblit.Editor.App;

/// <summary>
/// The project folder to edit, the scene to open first, and whether to show the engine's viewport. A run that
/// must not reach the network passes a store reader that answers from a file.
/// </summary>
internal sealed record EditorOptions(string Project, string Scene, bool Viewport, Func<CancellationToken, Task<string>>? StoreReader = null)
{
    const string Prefix = "res://";

    /// <summary>
    /// Whether the scene to open first is a file in the project. A project may name its main scene
    /// something else. The engine then keeps the scene it started with, and the scenes list opens another.
    /// </summary>
    public bool HasScene => Scene.StartsWith(Prefix) && File.Exists(Path.Combine(Project, Scene[Prefix.Length..]));
}

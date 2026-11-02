using Avalonia.Controls;
using Orblit.Filament;
using Orblit.Filament.Avalonia;

namespace Orblit.Game.Avalonia;

/// <summary>The screen of a real run: the scene goes to the renderer.</summary>
internal sealed class RendererScreen : IGameScreen
{
    private readonly RendererViewport _viewport = new();

    public Control View => _viewport;

    // A frame the renderer is too busy for is skipped. The next frame carries the newer scene.
    public void Draw(RenderScene scene, double seconds) => _ = _viewport.Frame(scene, seconds);
}

using Avalonia.Controls;
using Avalonia.Media;
using Orblit.Filament;

namespace Orblit.Game.Avalonia.Tests;

// Stands in for the renderer's control: something to click and size, and a record of every scene drawn.
internal sealed class RecordedScreen : IGameScreen
{
    public Control View { get; } = new Border { Background = Brushes.Transparent };

    public List<(RenderScene Scene, double Seconds)> Drawn { get; } = [];

    public void Draw(RenderScene scene, double seconds)
    {
        Drawn.Add((scene, seconds));

        // The renderer's control shows each frame by redrawing itself, and a headless window ticks only
        // while something on it is waiting to be drawn.
        View.InvalidateVisual();
    }
}

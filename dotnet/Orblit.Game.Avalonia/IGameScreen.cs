using Avalonia.Controls;
using Orblit.Filament;

namespace Orblit.Game.Avalonia;

/// <summary>
/// What the window draws on. The renderer's control cannot run without a graphics device, so the window
/// asks for this much of it and a test supplies a control that only records what it was given.
/// </summary>
internal interface IGameScreen
{
    /// <summary>The control the player sees. It also takes the keys and the pointer.</summary>
    Control View { get; }

    /// <summary>Draws <paramref name="scene"/> as it is at <paramref name="seconds"/> into the run.</summary>
    void Draw(RenderScene scene, double seconds);
}

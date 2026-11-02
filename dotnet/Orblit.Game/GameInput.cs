using Orblit.Input;

namespace Orblit.Game;

/// <summary>What the player is doing this frame.</summary>
/// <param name="Controls">The keys, touch regions and pad held now.</param>
/// <param name="Pointer">The pointer, or null when it is not over the view.</param>
public sealed record GameInput(FrameControls Controls, GamePointer? Pointer = null);

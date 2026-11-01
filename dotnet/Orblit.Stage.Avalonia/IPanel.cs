using Avalonia;

namespace Orblit.Stage.Avalonia;

/// <summary>How a pointer on a panel changed.</summary>
public enum PointerPhase
{
    Down,
    Move,
    Up,
    Cancel,
}

/// <summary>One pointer event, in the panel's own pixels from its top left.</summary>
public sealed record PanelPointer(PointerPhase Phase, int Id, Point Position);

/// <summary>
/// What a <see cref="WorldPanel"/> needs of the interface it shows: how big it is, whether it changed,
/// a picture of it and a way to press it. Declared here because the world panel is the only caller.
/// </summary>
public interface IPanel
{
    Size Size { get; }

    bool IsDirty { get; }

    /// <summary>Draws the panel and returns the picture as PNG bytes. It is no longer dirty after.</summary>
    byte[] Paint();

    void Send(PanelPointer input);
}

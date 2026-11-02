using Avalonia.Controls;

namespace Orblit.Game.Avalonia;

/// <summary>
/// A game with an interface of its own: a score, a menu, a bar of buttons. The host that can show a
/// control asks for it once, after the game starts, and puts it over the view.
/// </summary>
/// <remarks>
/// Declared here and not in <c>Orblit.Game</c> because only a host with a toolkit asks for a control.
/// A game that implements it can still run anywhere that has no screen, by being run as an
/// <see cref="IGame"/>.
/// </remarks>
public interface IInterfaceGame : IGame
{
    /// <summary>Builds the control. Called once. The host removes it when the game stops.</summary>
    Control CreateInterface();
}

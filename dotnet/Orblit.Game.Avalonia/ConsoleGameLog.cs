namespace Orblit.Game.Avalonia;

/// <summary>Where a standalone game's log goes: the terminal that started it.</summary>
internal sealed class ConsoleGameLog : IGameLog
{
    public void Say(string message) => Console.WriteLine(message);

    public void Error(string message, Exception error) => Console.Error.WriteLine($"{message}{Environment.NewLine}{error}");
}

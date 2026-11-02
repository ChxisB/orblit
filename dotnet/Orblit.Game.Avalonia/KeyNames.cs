using Avalonia.Input;

namespace Orblit.Game.Avalonia;

/// <summary>
/// The names a game reads keys by. A key is named by its place on the keyboard and not by the letter
/// it types, so a game's WASD stays under the same fingers on a French layout.
/// </summary>
internal static class KeyNames
{
    /// <summary>The name of <paramref name="key"/>, or null for no key at all.</summary>
    internal static string? Of(PhysicalKey key)
    {
        var name = key.ToString();
        return key == PhysicalKey.None ? null : name is [var letter] && char.IsAsciiLetterUpper(letter) ? "Key" + name : name;
    }
}

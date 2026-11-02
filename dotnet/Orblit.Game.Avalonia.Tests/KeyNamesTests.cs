using Avalonia.Input;

namespace Orblit.Game.Avalonia.Tests;

public sealed class KeyNamesTests
{
    [Theory]
    [InlineData(PhysicalKey.W, "KeyW")]
    [InlineData(PhysicalKey.A, "KeyA")]
    [InlineData(PhysicalKey.Space, "Space")]
    [InlineData(PhysicalKey.Enter, "Enter")]
    [InlineData(PhysicalKey.ArrowLeft, "ArrowLeft")]
    [InlineData(PhysicalKey.Digit1, "Digit1")]
    [InlineData(PhysicalKey.ShiftLeft, "ShiftLeft")]
    public void AKeyIsNamedByItsPlaceOnTheKeyboard(PhysicalKey key, string name) =>
        Assert.Equal(name, KeyNames.Of(key));

    [Fact]
    public void NoKeyHasNoName() => Assert.Null(KeyNames.Of(PhysicalKey.None));

    [Fact]
    public void EveryOtherKeyHasAUniqueName()
    {
        var names = Enum.GetValues<PhysicalKey>()
            .Where(key => key != PhysicalKey.None)
            .Select(key => KeyNames.Of(key))
            .ToList();

        Assert.All(names, name => Assert.False(string.IsNullOrEmpty(name)));
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}

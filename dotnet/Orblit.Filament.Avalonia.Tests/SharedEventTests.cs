namespace Orblit.Filament.Avalonia.Tests;

public sealed class SharedEventTests
{
    [AppleFact]
    public void AnEventStartsAtZeroAndCountsWhatIsSignalled()
    {
        using var shared = new SharedEvent();

        Assert.NotEqual(nint.Zero, shared.Handle);
        Assert.Equal(0UL, shared.Value);
        shared.Signal(7);
        Assert.Equal(7UL, shared.Value);
    }

    [AppleFact]
    public void ADisposedEventRefusesToBeUsedAndCanBeDisposedAgain()
    {
        var shared = new SharedEvent();
        shared.Dispose();
        shared.Dispose();

        Assert.Throws<ObjectDisposedException>(() => shared.Value);
        Assert.Throws<ObjectDisposedException>(() => shared.Handle);
    }
}

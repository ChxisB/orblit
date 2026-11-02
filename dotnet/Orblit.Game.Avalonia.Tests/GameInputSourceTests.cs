using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Game.Avalonia.Tests;

public sealed class GameInputSourceTests
{
    private static readonly RenderCamera Camera = new()
    {
        Position = new Vector3(0, 0, 5),
        Target = Vector3.Zero(),
        FieldOfView = 90,
    };

    [Fact]
    public Task AKeyIsHeldByItsNameUntilItIsReleased() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();

        window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.None);
        var held = source.Read(Camera).Controls.Keys.ToList();
        window.KeyReleaseQwerty(PhysicalKey.W, RawInputModifiers.None);
        var released = source.Read(Camera).Controls.Keys.ToList();

        Assert.Equal(["KeyW"], held);
        Assert.Empty(released);
    });

    [Fact]
    public Task AKeyStaysHeldAcrossTheEndOfAFrame() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);

        source.EndFrame();

        Assert.Equal(["Space"], source.Read(Camera).Controls.Keys);
    });

    [Fact]
    public Task LosingFocusReleasesEveryKey() => Headless.Run(() =>
    {
        // A key released while another control has the focus never reaches the surface, so it would stay held.
        var other = new TextBox();
        var (window, surface, source) = Shown(other);
        window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.None);

        other.Focus();

        Assert.False(surface.IsFocused);
        Assert.Empty(source.Read(Camera).Controls.Keys);
    });

    [Fact]
    public Task AReadIsASnapshot() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();
        window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.None);
        var before = source.Read(Camera).Controls;

        window.KeyReleaseQwerty(PhysicalKey.W, RawInputModifiers.None);

        Assert.Equal(["KeyW"], before.Keys);
    });

    [Fact]
    public Task ThereIsNoPointerUntilItIsOverTheView() => Headless.Run(() =>
    {
        var (_, _, source) = Shown();

        Assert.Null(source.Read(Camera).Pointer);
    });

    [Fact]
    public Task TheRayRunsThroughThePointUnderThePointer() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();

        window.MouseMove(new Point(100, 50));
        var pointer = source.Read(Camera).Pointer!;

        Assert.Equal(new Vector3(0, 0, 5), pointer.Origin);
        Assert.Equal(0, pointer.Direction.X, 1e-9);
        Assert.Equal(0, pointer.Direction.Y, 1e-9);
        Assert.Equal(-1, pointer.Direction.Z, 1e-9);
        Assert.False(pointer.Pressed);
    });

    [Fact]
    public Task AClickIsPressedForOneFrame() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();

        window.MouseDown(new Point(100, 50), MouseButton.Left);
        window.MouseUp(new Point(100, 50), MouseButton.Left);
        var first = source.Read(Camera).Pointer!;
        source.EndFrame();
        var second = source.Read(Camera).Pointer!;

        Assert.True(first.Pressed);
        Assert.False(second.Pressed);
    });

    [Fact]
    public Task ARightDragSumsTheMovementUntilTheFrameEnds() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();

        window.MouseDown(new Point(10, 10), MouseButton.Right);
        window.MouseMove(new Point(30, 25));
        window.MouseMove(new Point(35, 20));
        var moved = source.Read(Camera).Pointer!.Drag!;
        source.EndFrame();
        var held = source.Read(Camera).Pointer!.Drag!;
        window.MouseUp(new Point(35, 20), MouseButton.Right);
        var released = source.Read(Camera).Pointer!.Drag;

        Assert.Equal(25, moved.X, 1e-9);
        Assert.Equal(10, moved.Y, 1e-9);
        Assert.Equal(0, held.Length, 1e-9);
        Assert.Null(released);
    });

    [Fact]
    public Task AFlickReleasedWithinOneFrameStillTurnsTheView() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();

        window.MouseDown(new Point(10, 10), MouseButton.Right);
        window.MouseMove(new Point(40, 10));
        window.MouseUp(new Point(40, 10), MouseButton.Right);
        var flicked = source.Read(Camera).Pointer!.Drag;
        source.EndFrame();
        var after = source.Read(Camera).Pointer!.Drag;

        Assert.Equal(30, flicked!.X, 1e-9);
        Assert.Null(after);
    });

    [Fact]
    public Task ALostCaptureEndsTheDrag() => Headless.Run(() =>
    {
        // The system takes the capture on a task switch, and the release then never reaches the surface.
        var (window, surface, source) = Shown();
        window.MouseDown(new Point(10, 10), MouseButton.Right);
        source.EndFrame();

        surface.RaiseEvent(new PointerCaptureLostEventArgs(surface, new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true)));
        window.MouseMove(new Point(60, 40));

        Assert.Null(source.Read(Camera).Pointer!.Drag);
    });

    [Fact]
    public Task TheWheelSumsNotchesUntilTheFrameEnds() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();

        window.MouseWheel(new Point(100, 50), new Vector(0, 1));
        window.MouseWheel(new Point(100, 50), new Vector(0, 2));
        var turned = source.Read(Camera).Pointer!.Wheel;
        source.EndFrame();
        var after = source.Read(Camera).Pointer!.Wheel;

        Assert.Equal(3, turned, 1e-9);
        Assert.Equal(0, after, 1e-9);
    });

    [Fact]
    public Task ThePointerIsGoneOnceItLeavesTheView() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();
        window.MouseMove(new Point(100, 50));

        window.MouseMove(new Point(250, 50));

        Assert.Null(source.Read(Camera).Pointer);
    });

    [Fact]
    public Task ADetachedSourceHearsNothing() => Headless.Run(() =>
    {
        var (window, _, source) = Shown();
        source.Detach();

        window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.None);
        window.MouseDown(new Point(100, 50), MouseButton.Left);

        var input = source.Read(Camera);
        Assert.Empty(input.Controls.Keys);
        Assert.Null(input.Pointer);
    });

    private static (Window Window, Border Surface, GameInputSource Source) Shown(Control? beside = null)
    {
        // At the left edge, so a point in the window is the same point on the surface.
        var surface = new Border { Background = Brushes.Transparent, Width = 200, Height = 100, HorizontalAlignment = HorizontalAlignment.Left };
        var content = new StackPanel { Children = { surface } };
        if (beside is not null)
        {
            content.Children.Add(beside);
        }

        var window = new Window { Width = 300, Height = 300, Content = content };
        window.Show();
        window.UpdateLayout();
        var source = new GameInputSource(surface);
        surface.Focus();
        return (window, surface, source);
    }
}

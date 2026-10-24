using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;

namespace Orblit.Filament.Avalonia;

/// <summary>Picks the path a control shows frames by.</summary>
internal static class Presenters
{
    /// <summary>Picks the path for a control. The viewport takes it as a parameter so a test can pick another.</summary>
    internal delegate Task<(IFramePresenter Presenter, string? Fallback)> Choose(Control owner);

    /// <summary>
    /// The shared texture path where the compositor has it, and the readback path with the reason
    /// where it does not. A fault while setting the faster path up is a reason too, because a
    /// viewport that shows something slowly is better than one that shows nothing.
    /// </summary>
    internal static async Task<(IFramePresenter Presenter, string? Fallback)> ChooseAsync(Control owner)
    {
        try
        {
            if (!OperatingSystem.IsMacOS())
            {
                return Readback(owner, "Only macOS has a shared texture path so far.");
            }

            var compositor = ElementComposition.GetElementVisual(owner)?.Compositor;
            var interop = compositor is null ? null : await compositor.TryGetCompositionGpuInterop();
            if (compositor is null || interop is null)
            {
                return Readback(owner, "The compositor has no GPU interop.");
            }

            if (!interop.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef)
                || !interop.SupportedSemaphoreTypes.Contains(KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent))
            {
                return Readback(owner, "The compositor cannot import an IOSurface with a Metal shared event.");
            }

            return (new SharedTexturePresenter(owner, compositor, interop), null);
        }
        catch (Exception ex)
        {
            return Readback(owner, $"The shared texture path would not start: {ex.Message}");
        }
    }

    private static (IFramePresenter Presenter, string? Fallback) Readback(Control owner, string reason) =>
        (new ReadbackPresenter(owner), reason);
}

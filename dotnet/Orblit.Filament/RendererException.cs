namespace Orblit.Filament;

/// <summary>A call of the renderer's C API refused what it was given. The reason was logged by the renderer.</summary>
public sealed class RendererException(RendererResult result, string call)
    : InvalidOperationException($"{call} came to {result}.")
{
    public RendererResult Result { get; } = result;
}

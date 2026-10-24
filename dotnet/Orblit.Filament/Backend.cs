namespace Orblit.Filament;

/// <summary>
/// Which graphics API the renderer draws with. <see cref="Default"/> is the
/// platform's own choice. The environment variable <c>ORBLIT_BACKEND</c>
/// (metal, vulkan, opengl, webgpu) overrides it, for testing a backend on a
/// machine whose default is another.
/// </summary>
public enum Backend
{
    Default = 0,
    Metal = 1,
    Vulkan = 2,
    OpenGl = 3,
    WebGpu = 4,
}

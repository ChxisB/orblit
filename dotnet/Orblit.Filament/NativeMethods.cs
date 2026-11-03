using System.Runtime.InteropServices;
using Orblit.Interop;

namespace Orblit.Filament;

/// <summary>
/// The C ABI of <c>orblit_renderer.h</c>, declared and nothing else. Handles
/// are <see cref="nint"/>. Each parameter has the name the header gives it,
/// and an array arrives with the length the header asks for beside it.
/// </summary>
internal static partial class NativeMethods
{
    internal const string Library = "orblit_filament";

    static NativeMethods() => NativeLoader.Register(typeof(NativeMethods).Assembly, Library);

    /// <summary>The rows the scene calls take, as <c>orblit_stride</c> spells them.</summary>
    internal enum Stride
    {
        Transform = 0,
        Colour,
        Material,
        MaterialMaps,
        Video,
        Light,
        Decal,
        Fog,
        Probe,
        Field,
        Environment,
        Pass,
        Target,
        GodRays,
        Distortion,
        PopulationBounds,
        Splat,
        SplatRecordBytes,
        Sky,
        Precipitation,
        Outline,
        Pipeline,
        SpriteLayer,
        Sprite,
        PoseInts,
        Pose,
        TerrainInts,
        TerrainRegionInts,
        Terrain,
        TerrainSet,
    }

    [LibraryImport(Library, EntryPoint = "orblit_renderer_stride")]
    internal static partial uint StrideOf(Stride which);

    // ---- Bytes by name ----

    [LibraryImport(Library, EntryPoint = "orblit_renderer_provide_resource", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ProvideResource(string name, ReadOnlySpan<byte> bytes, nuint length);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_release_resource", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ReleaseResource(string name);

    // ---- Feeds ----

    [LibraryImport(Library, EntryPoint = "orblit_renderer_create_feed")]
    internal static partial long CreateFeed(int width, int height);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_release_feed")]
    internal static partial void ReleaseFeed(long feed);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_submit_feed_buffer")]
    internal static partial int SubmitFeedBuffer(long feed, nint pixelBuffer);

    // ---- Lifetime ----

    [LibraryImport(Library, EntryPoint = "orblit_renderer_create")]
    internal static partial nint Create(Backend backend, in Surface surface, uint width, uint height);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_destroy")]
    internal static partial void Destroy(nint renderer);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_backend")]
    internal static partial Backend BackendOf(nint renderer);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_resize")]
    internal static partial int Resize(nint renderer, uint width, uint height);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_attach_surface")]
    internal static partial int AttachSurface(nint renderer, in Surface surface, uint width, uint height);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_detach_surface")]
    internal static partial int DetachSurface(nint renderer);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_draw")]
    internal static partial int Draw(nint renderer, double seconds);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_rendered_frames")]
    internal static partial ulong RenderedFrames(nint renderer);

    // ---- The scene ----

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_objects")]
    internal static partial int ApplyObjects(
        nint renderer,
        uint count,
        ReadOnlySpan<long> keys,
        ReadOnlySpan<float> transforms,
        nuint transformFloats,
        ReadOnlySpan<float> colours,
        nuint colourFloats,
        ReadOnlySpan<int> meshes,
        ReadOnlySpan<int> flags,
        ReadOnlySpan<int> materials,
        ReadOnlySpan<int> morphCounts,
        ReadOnlySpan<float> morphWeights,
        nuint morphWeightFloats,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] paths,
        uint pathCount);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_poses")]
    internal static partial int ApplyPoses(
        nint renderer,
        uint count,
        ReadOnlySpan<long> keys,
        ReadOnlySpan<int> ints,
        nuint intCount,
        ReadOnlySpan<float> floats,
        nuint floatCount,
        ReadOnlySpan<int> jointCounts,
        ReadOnlySpan<int> joints,
        nuint jointInts,
        ReadOnlySpan<float> jointTransforms,
        nuint jointTransformFloats,
        double at);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_batching")]
    internal static partial int SetBatching(nint renderer, int enabled);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_depth_prepass")]
    internal static partial int SetDepthPrepass(nint renderer, int enabled);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_custom_materials")]
    internal static partial int SetCustomMaterials(
        nint renderer,
        uint count,
        ReadOnlySpan<long> keys,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] packages,
        ReadOnlySpan<int> entryCounts,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] names,
        ReadOnlySpan<int> widths,
        ReadOnlySpan<int> maps,
        uint entryTotal,
        ReadOnlySpan<float> values,
        nuint valueFloats,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] texturePaths,
        ReadOnlySpan<int> textureSrgb,
        uint textureCount);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_materials")]
    internal static partial int ApplyMaterials(
        nint renderer,
        uint count,
        ReadOnlySpan<long> keys,
        ReadOnlySpan<int> flags,
        ReadOnlySpan<float> @params,
        nuint paramFloats,
        ReadOnlySpan<int> maps,
        nuint mapCount,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] texturePaths,
        ReadOnlySpan<int> textureSrgb,
        uint textureCount,
        ReadOnlySpan<int> videos);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_pipeline")]
    internal static partial int SetPipeline(nint renderer, ReadOnlySpan<float> @params, nuint count);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_videos")]
    internal static partial int ApplyVideos(
        nint renderer,
        uint count,
        ReadOnlySpan<long> keys,
        ReadOnlySpan<int> flags,
        ReadOnlySpan<float> @params,
        nuint paramFloats,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] paths,
        uint pathCount);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_lights")]
    internal static partial int ApplyLights(
        nint renderer,
        uint count,
        ReadOnlySpan<long> keys,
        ReadOnlySpan<int> kinds,
        ReadOnlySpan<int> flags,
        ReadOnlySpan<float> @params,
        nuint paramFloats);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_decals")]
    internal static partial int ApplyDecals(
        nint renderer,
        uint count,
        ReadOnlySpan<float> @params,
        nuint paramFloats,
        ReadOnlySpan<int> images,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] paths,
        uint pathCount);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_fog")]
    internal static partial int SetFog(nint renderer, int enabled, ReadOnlySpan<float> @params, nuint count);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_post_process")]
    internal static partial int SetPostProcess(nint renderer, ReadOnlySpan<float> @params, nuint count);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_probes")]
    internal static partial int ApplyProbes(
        nint renderer,
        uint count,
        ReadOnlySpan<long> keys,
        ReadOnlySpan<float> @params,
        nuint paramFloats);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_field", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int ApplyField(nint renderer, ReadOnlySpan<float> @params, nuint count, string? from);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_environment", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int SetEnvironment(
        nint renderer,
        string? radiance,
        string? skybox,
        ReadOnlySpan<float> @params,
        nuint count);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_render_graph")]
    internal static partial int SetRenderGraph(
        nint renderer,
        uint passCount,
        ReadOnlySpan<float> passes,
        nuint passFloats,
        uint targetCount,
        ReadOnlySpan<float> targets,
        nuint targetFloats,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] names,
        uint nameCount);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_god_rays")]
    internal static partial int SetGodRays(
        nint renderer,
        ReadOnlySpan<float> godRays,
        nuint godRayFloats,
        ReadOnlySpan<float> distortions,
        nuint distortionFloats);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_populations")]
    internal static partial int ApplyPopulations(
        nint renderer,
        uint count,
        ReadOnlySpan<int> keys,
        ReadOnlySpan<int> counts,
        ReadOnlySpan<int> meshes,
        ReadOnlySpan<int> flags,
        ReadOnlySpan<int> revisions,
        ReadOnlySpan<float> ranges,
        ReadOnlySpan<float> bounds,
        nuint boundFloats,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] paths,
        uint pathCount,
        ReadOnlySpan<int> changed,
        uint changedCount,
        ReadOnlySpan<float> transforms,
        nuint transformFloats,
        ReadOnlySpan<float> colours,
        nuint colourFloats);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_splats")]
    internal static partial int ApplySplats(
        nint renderer,
        uint count,
        ReadOnlySpan<int> keys,
        ReadOnlySpan<int> flags,
        ReadOnlySpan<int> revisions,
        ReadOnlySpan<float> @params,
        nuint paramFloats,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] paths,
        uint pathCount,
        ReadOnlySpan<int> changed,
        ReadOnlySpan<int> changedCounts,
        uint changedCount,
        ReadOnlySpan<byte> data,
        nuint dataLength);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_sprites")]
    internal static partial int ApplySprites(
        nint renderer,
        uint count,
        ReadOnlySpan<int> keys,
        ReadOnlySpan<int> flags,
        ReadOnlySpan<int> orders,
        ReadOnlySpan<int> revisions,
        ReadOnlySpan<float> @params,
        nuint paramFloats,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string[] paths,
        uint pathCount,
        ReadOnlySpan<int> changed,
        ReadOnlySpan<int> changedCounts,
        uint changedCount,
        ReadOnlySpan<float> records,
        nuint recordFloats);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_apply_terrain")]
    internal static partial int ApplyTerrain(
        nint renderer,
        ReadOnlySpan<int> ints,
        nuint intCount,
        ReadOnlySpan<float> floats,
        nuint floatCount,
        ReadOnlySpan<byte> data,
        nuint dataLength);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_sky")]
    internal static partial int SetSky(nint renderer, int enabled, ReadOnlySpan<float> @params, nuint count);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_precipitation")]
    internal static partial int SetPrecipitation(nint renderer, int enabled, ReadOnlySpan<float> @params, nuint count);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_sky_colour")]
    internal static partial int SetSkyColour(nint renderer, ReadOnlySpan<float> colour, float ambient, int showBody);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_camera")]
    internal static partial int SetCamera(
        nint renderer,
        ReadOnlySpan<float> position,
        ReadOnlySpan<float> target,
        float fieldOfView,
        int orthographic,
        float viewHeight,
        double at);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_planes")]
    internal static partial int SetPlanes(nint renderer, float nearPlane, float farPlane);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_exposure")]
    internal static partial int SetExposure(nint renderer, float aperture, float shutter, float sensitivity);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_set_outline")]
    internal static partial int SetOutline(
        nint renderer,
        ReadOnlySpan<long> keys,
        uint count,
        ReadOnlySpan<float> @params,
        nuint paramFloats);

    // ---- What the device can do ----

    [LibraryImport(Library, EntryPoint = "orblit_renderer_capability")]
    internal static partial int CapabilityOf(nint renderer, Capability which);

    // ---- What it drew, and what it cost ----

    [LibraryImport(Library, EntryPoint = "orblit_renderer_request_capture")]
    internal static partial int RequestCapture(nint renderer);

    // F2: the ABI's own signature.
    [LibraryImport(Library, EntryPoint = "orblit_renderer_read_capture")]
    internal static partial nuint ReadCapture(
        nint renderer,
        Span<byte> rgba,
        nuint capacity,
        out uint width,
        out uint height);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_stats")]
    internal static partial int ReadStats(nint renderer, out Stats stats);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_pass_timings")]
    internal static partial uint PassTimings(nint renderer, Span<double> milliseconds, Span<int> drawn, uint capacity);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_copy_presented")]
    internal static partial nint CopyPresented(nint renderer);

    // ---- What it could not do ----

    [LibraryImport(Library, EntryPoint = "orblit_renderer_notes")]
    internal static partial uint NoteCount(nint renderer);

    [LibraryImport(Library, EntryPoint = "orblit_renderer_note")]
    internal static partial int NoteAt(nint renderer, uint index, out nint about, out nint saying);
}

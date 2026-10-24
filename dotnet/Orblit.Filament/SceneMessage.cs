namespace Orblit.Filament;

/// <summary>How a scene becomes the flat arrays the renderer reads, one topic at a time.</summary>
internal static class SceneMessage
{
    /// <summary>The objects, and the mesh paths and materials they point at.</summary>
    internal static void Objects(
        IReadOnlyList<RenderObject> objects,
        IReadOnlyList<RenderMaterial> materials,
        IDictionary<string, object> into)
    {
        // Materials are pointed at by position in this frame's list, so the renderer never searches.
        // Keys are what survive between frames and positions are what travel in one.
        var materialAt = PositionOf(materials.Select(material => material.Key));
        var columns = new ObjectColumns(objects.Count);
        for (var i = 0; i < objects.Count; i++)
        {
            columns.Set(i, objects[i], materialAt);
        }

        into["objectKeys"] = columns.Keys;
        into["transforms"] = columns.Transforms;
        into["colours"] = columns.Colours;
        into["meshes"] = columns.Meshes;
        into["objectFlags"] = columns.Flags;
        into["objectMorphCounts"] = columns.MorphCounts;
        into["objectMorphWeights"] = columns.MorphWeights;
        into["meshPaths"] = columns.Paths.Items.ToArray();
        into["objectMaterials"] = columns.Materials;
    }

    /// <summary>The materials, and the textures they draw from. Both travel by position.</summary>
    internal static void Materials(
        IReadOnlyList<RenderMaterial> materials,
        IReadOnlyList<Video> videos,
        IDictionary<string, object> into)
    {
        var count = materials.Count;
        var keys = new long[count];
        var flags = new int[count];
        var packed = new float[count * RenderMaterial.Stride];
        var maps = new int[count * RenderMaterial.MapCount];
        var screens = new int[count];
        var videoAt = PositionOf(videos.Select(video => video.Key));
        var textures = new IndexedSet<Texture>();

        for (var i = 0; i < count; i++)
        {
            var material = materials[i];
            keys[i] = material.Key;
            flags[i] = material.Flags;
            material.Pack(packed, i * RenderMaterial.Stride);
            screens[i] = material.Video is { } video ? videoAt.GetValueOrDefault(video, -1) : -1;
            for (var m = 0; m < RenderMaterial.MapCount; m++)
            {
                maps[(i * RenderMaterial.MapCount) + m] = material.Maps[m] is { } map ? textures.IndexOf(map) : -1;
            }
        }

        into["materialKeys"] = keys;
        into["materialFlags"] = flags;
        into["materialParams"] = packed;
        into["materialMaps"] = maps;
        into["materialVideos"] = screens;
        into["texturePaths"] = textures.Items.Select(texture => texture.Path).ToArray();
        into["textureSrgb"] = textures.Items.Select(texture => texture.Srgb ? 1 : 0).ToArray();
    }

    /// <summary>The shaders somebody wrote. Adds nothing when no material wears one, which is almost every scene.</summary>
    internal static void Surfaces(IReadOnlyList<RenderMaterial> materials, IDictionary<string, object> into)
    {
        var columns = new SurfaceColumns();
        foreach (var material in materials)
        {
            if (material.Surface is { } surface)
            {
                columns.Add(material.Key, surface);
            }
        }

        if (columns.Keys.Count == 0)
        {
            return;
        }

        into["surfaceKeys"] = columns.Keys.ToArray();
        into["surfacePackages"] = columns.Packages.ToArray();
        into["surfaceEntryCounts"] = columns.EntryCounts.ToArray();
        into["surfaceNames"] = columns.Names.ToArray();
        into["surfaceWidths"] = columns.Widths.ToArray();
        into["surfaceMaps"] = columns.Maps.ToArray();
        into["surfaceValues"] = columns.Numbers.ToArray();
        into["surfaceTexturePaths"] = columns.Textures.Items.Select(texture => texture.Path).ToArray();
        into["surfaceTextureSrgb"] = columns.Textures.Items.Select(texture => texture.Srgb ? 1 : 0).ToArray();
    }

    internal static void Videos(IReadOnlyList<Video> videos, IDictionary<string, object> into)
    {
        var keys = new long[videos.Count];
        var flags = new int[videos.Count];
        var packed = new float[videos.Count * Video.Stride];
        var paths = new string[videos.Count];

        for (var i = 0; i < videos.Count; i++)
        {
            var video = videos[i];
            keys[i] = video.Key;
            flags[i] = video.Flags;
            paths[i] = video.Path;
            // A seek of minus one is no seek asked for, so a token that moved with no target is a no-op rather than a jump to the start.
            Packing.Floats(video.Rate, video.Volume, video.SeekTo ?? -1, video.SeekToken).CopyTo(packed, i * Video.Stride);
        }

        into["videoKeys"] = keys;
        into["videoFlags"] = flags;
        into["videoParams"] = packed;
        into["videoPaths"] = paths;
    }

    /// <summary>The decals, with their pictures sent once and pointed at.</summary>
    internal static void Decals(IReadOnlyList<Decal> decals, IDictionary<string, object> into)
    {
        var packed = new float[decals.Count * Decal.Stride];
        var images = new int[decals.Count];
        var paths = new IndexedSet<string>();

        for (var i = 0; i < decals.Count; i++)
        {
            decals[i].Pack(packed, i * Decal.Stride);
            images[i] = decals[i].Texture is { } texture ? paths.IndexOf(texture.Path) : -1;
        }

        into["decalParams"] = packed;
        into["decalImages"] = images;
        into["decalPaths"] = paths.Items.ToArray();
    }

    /// <summary>
    /// The selection outline, the shafts of light and the bending of air. The shafts follow the
    /// first directional light, which is the one the renderer draws, and thin under cloud only when
    /// the sky that carries the cloud is drawn.
    /// </summary>
    internal static void Screen(RenderScene scene, IDictionary<string, object> into)
    {
        var sun = scene.Sun;
        var clouds = scene.Sky.Drawn && scene.Sky.Clouds.IsVisible ? scene.Sky.Clouds.Cover : 0;

        into["outlineKeys"] = scene.Outline.PackedKeys;
        into["outlineParams"] = scene.Outline.Packed;
        into["godRayParams"] = scene.GodRays.Pack(sun is null ? null : -sun.Direction, sun?.Colour, clouds);
        into["distortionParams"] = Distortion.PackAll(scene.Distortions);
    }

    internal static void Probes(IReadOnlyList<Probe> probes, IDictionary<string, object> into)
    {
        var keys = new long[probes.Count];
        var packed = new float[probes.Count * Probe.Stride];
        for (var i = 0; i < probes.Count; i++)
        {
            keys[i] = probes[i].Key;
            probes[i].Pack(packed, i * Probe.Stride);
        }

        into["probeKeys"] = keys;
        into["probeParams"] = packed;
    }

    /// <summary>
    /// How the frame is made: the image work, the pipeline's dials, the light kept in the world,
    /// the place the scene stands in and the passes. The graph is the one that is drawn, with the
    /// passes for the shafts and the bending of air put in.
    /// </summary>
    internal static void Picture(RenderScene scene, IDictionary<string, object> into)
    {
        var graph = scene.DrawnGraph;
        into["postParams"] = scene.Post.Packed;
        into["pipelineParams"] = scene.Pipeline.Packed;
        into["fieldParams"] = scene.Field.Packed;
        into["fieldFrom"] = scene.Field.From;
        into["environmentRadiance"] = scene.Environment.Radiance ?? "";
        into["environmentSkybox"] = scene.Environment.Skybox ?? "";
        into["environmentParams"] = scene.Environment.Packed;
        into["graphPasses"] = graph.PackedPasses;
        into["graphTargets"] = graph.PackedTargets;
        into["graphTargetNames"] = graph.Targets.Select(target => target.Name).ToArray();
    }

    internal static void Lights(IReadOnlyList<RenderLight> lights, IDictionary<string, object> into)
    {
        var keys = new long[lights.Count];
        var kinds = new int[lights.Count];
        var flags = new int[lights.Count];
        var packed = new float[lights.Count * RenderLight.Stride];

        for (var i = 0; i < lights.Count; i++)
        {
            keys[i] = lights[i].Key;
            kinds[i] = (int)lights[i].Kind;
            flags[i] = lights[i].CastShadows ? 1 : 0;
            lights[i].Pack(packed, i * RenderLight.Stride);
        }

        into["lightKeys"] = keys;
        into["lightKinds"] = kinds;
        into["lightFlags"] = flags;
        into["lightParams"] = packed;
    }

    internal static void Camera(RenderCamera camera, IDictionary<string, object> into)
    {
        into["cameraPosition"] = Packing.Floats(camera.Position.X, camera.Position.Y, camera.Position.Z);
        into["cameraTarget"] = Packing.Floats(camera.Target.X, camera.Target.Y, camera.Target.Z);
        into["fieldOfView"] = camera.FieldOfView;
        into["orthographic"] = camera.Orthographic;
        into["viewHeight"] = camera.ViewHeight;
        into["aperture"] = camera.Aperture;
        into["shutterSpeed"] = camera.ShutterSpeed;
        into["sensitivity"] = camera.Sensitivity;
    }

    /// <summary>The sky and the weather in front of it.</summary>
    internal static void Air(Sky sky, Fog fog, Precipitation precipitation, IDictionary<string, object> into)
    {
        into["skyColour"] = Packing.Floats(sky.Colour.X, sky.Colour.Y, sky.Colour.Z);
        into["ambient"] = sky.Ambient;
        into["showBody"] = sky.ShowBody;
        into["skyParams"] = sky.Packed;
        into["fogEnabled"] = fog.IsVisible;
        into["fogParams"] = fog.Packed;
        into["precipitationEnabled"] = precipitation.IsVisible;
        into["precipitationParams"] = precipitation.Packed;
        into["skyEnabled"] = sky.Drawn;
    }

    /// <summary>Where each key sits in <paramref name="keys"/>. A key given twice points at its last place.</summary>
    internal static Dictionary<TKey, int> PositionOf<TKey>(IEnumerable<TKey> keys)
        where TKey : notnull
    {
        var at = new Dictionary<TKey, int>();
        var i = 0;
        foreach (var key in keys)
        {
            at[key] = i++;
        }

        return at;
    }

    /// <summary>
    /// The surface columns: a key, a package and an entry count for each surface, then a name, a
    /// width and a texture position for each entry, then every number end to end. A width of
    /// nought is a texture, and its position points into the surface textures' own list.
    /// </summary>
    private sealed class SurfaceColumns
    {
        internal List<long> Keys { get; } = [];

        internal List<string> Packages { get; } = [];

        internal List<int> EntryCounts { get; } = [];

        internal List<string> Names { get; } = [];

        internal List<int> Widths { get; } = [];

        internal List<int> Maps { get; } = [];

        internal List<float> Numbers { get; } = [];

        internal IndexedSet<Texture> Textures { get; } = new();

        internal void Add(long key, CustomSurface surface)
        {
            Keys.Add(key);
            Packages.Add(surface.Package);
            EntryCounts.Add(surface.Values.Count + surface.Textures.Count);
            foreach (var (name, numbers) in surface.Values)
            {
                Names.Add(name);
                Widths.Add(numbers.Count);
                Maps.Add(-1);
                Numbers.AddRange(numbers.Select(number => (float)number));
            }

            foreach (var (name, texture) in surface.Textures)
            {
                Names.Add(name);
                Widths.Add(0);
                Maps.Add(Textures.IndexOf(texture));
            }
        }
    }

    /// <summary>
    /// The object columns. Paths are sent once and pointed at by index, because the same mesh is
    /// usually on many objects. Morph weights are packed end to end with a count each, because a
    /// face rig has dozens and a crate has none, and a width that suits both is wrong for both.
    /// </summary>
    private sealed class ObjectColumns(int count)
    {
        private readonly List<float> _weights = [];

        internal long[] Keys { get; } = new long[count];

        internal float[] Transforms { get; } = new float[count * 16];

        internal float[] Colours { get; } = new float[count * 3];

        internal int[] Meshes { get; } = new int[count];

        internal int[] Flags { get; } = new int[count];

        internal int[] Materials { get; } = new int[count];

        internal int[] MorphCounts { get; } = new int[count];

        internal IndexedSet<string> Paths { get; } = new();

        /// <summary>Never empty, because the far side takes a pointer and an empty array has none to give.</summary>
        internal float[] MorphWeights => _weights.Count == 0 ? [0f] : _weights.ToArray();

        internal void Set(int i, RenderObject one, Dictionary<long, int> materialAt)
        {
            Keys[i] = one.Key;
            Meshes[i] = one.Mesh is null ? -1 : Paths.IndexOf(one.Mesh);
            Materials[i] = one.Material is { } material ? materialAt.GetValueOrDefault(material, -1) : -1;
            Flags[i] = one.Flags;
            for (var k = 0; k < 16; k++)
            {
                // The matrix storage is column-major already, as the renderer's matrix is.
                Transforms[(i * 16) + k] = (float)one.Transform[k];
            }

            Colours[i * 3] = (float)one.Colour.X;
            Colours[(i * 3) + 1] = (float)one.Colour.Y;
            Colours[(i * 3) + 2] = (float)one.Colour.Z;

            if (one.MorphWeights is { Count: > 0 } weights)
            {
                MorphCounts[i] = weights.Count;
                _weights.AddRange(weights.Select(weight => (float)weight));
            }
        }
    }
}

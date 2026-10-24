namespace Orblit.Filament;

public sealed partial class Renderer
{
    /// <summary>
    /// Describes the scene to the renderer, whole. The calls go in the order the renderer's own
    /// scene holder makes them, because a later call reads what an earlier one set: the
    /// environment and the passes first, since everything after is drawn into and lit by them.
    /// Poses are sent even when no object has any, so an object that stops animating returns to rest.
    /// </summary>
    public void Apply(RenderScene scene, double at = 0)
    {
        // Volumes first: the sky's ambient, the fog's visibility and the exposure below are read
        // from the scene and not from the message, and a volume moves all three.
        scene = scene.Resolved();
        var message = scene.ToMessage(at, _held);

        SetEnvironment(message);
        SetRenderGraph(message);
        Check(NativeMethods.SetBatching(Alive, scene.Batching ? 1 : 0), "set_batching");
        Check(NativeMethods.SetDepthPrepass(Alive, scene.DepthPrepass ? 1 : 0), "set_depth_prepass");
        SetGodRays(message);
        SetCustomMaterials(message);
        ApplyVideos(message);
        ApplyMaterials(message);
        ApplyObjects(message);
        ApplyPoses(message, at);
        ApplyPopulations(message);
        ApplySplats(message);
        ApplySprites(message);
        ApplyTerrain(message);
        ApplyLights(message);
        ApplyDecals(message);
        ApplyProbes(message);
        ApplyField(message);
        ApplyAir(scene, message);
        SetCamera(scene.Camera, message, at);
        SetOutline(message);
        _held.Remember(scene);
    }

    private void SetEnvironment(OrderedDictionary<string, object> message)
    {
        var packed = Packing.Read<float[]>(message, "environmentParams");
        Check(
            NativeMethods.SetEnvironment(
                Alive,
                Packing.Read<string>(message, "environmentRadiance"),
                Packing.Read<string>(message, "environmentSkybox"),
                packed,
                (nuint)packed.Length),
            "set_environment");
    }

    private void SetRenderGraph(OrderedDictionary<string, object> message)
    {
        var passes = Packing.Read<float[]>(message, "graphPasses");
        var targets = Packing.Read<float[]>(message, "graphTargets");
        var names = Packing.Read<string[]>(message, "graphTargetNames");

        // F2: the ABI's own signature.
        Check(
            NativeMethods.SetRenderGraph(
                Alive,
                (uint)(passes.Length / RenderGraph.PassStride),
                passes,
                (nuint)passes.Length,
                (uint)names.Length,
                targets,
                (nuint)targets.Length,
                names,
                (uint)names.Length),
            "set_render_graph");
    }

    private void SetGodRays(OrderedDictionary<string, object> message)
    {
        var godRays = Packing.Read<float[]>(message, "godRayParams");
        var distortions = Packing.Read<float[]>(message, "distortionParams");
        Check(
            NativeMethods.SetGodRays(Alive, godRays, (nuint)godRays.Length, distortions, (nuint)distortions.Length),
            "set_god_rays");
    }

    /// <summary>The shaders somebody wrote, sent only when a material wears one.</summary>
    private void SetCustomMaterials(OrderedDictionary<string, object> message)
    {
        if (!message.TryGetValue("surfaceKeys", out var held))
        {
            return;
        }

        var keys = (long[])held;
        var names = Packing.Read<string[]>(message, "surfaceNames");
        var values = Packing.Read<float[]>(message, "surfaceValues");
        var texturePaths = Packing.Read<string[]>(message, "surfaceTexturePaths");

        // F2: the ABI's own signature.
        Check(
            NativeMethods.SetCustomMaterials(
                Alive,
                (uint)keys.Length,
                keys,
                Packing.Read<string[]>(message, "surfacePackages"),
                Packing.Read<int[]>(message, "surfaceEntryCounts"),
                names,
                Packing.Read<int[]>(message, "surfaceWidths"),
                Packing.Read<int[]>(message, "surfaceMaps"),
                (uint)names.Length,
                values,
                (nuint)values.Length,
                texturePaths,
                Packing.Read<int[]>(message, "surfaceTextureSrgb"),
                (uint)texturePaths.Length),
            "set_custom_materials");
    }

    private void ApplyVideos(OrderedDictionary<string, object> message)
    {
        var keys = Packing.Read<long[]>(message, "videoKeys");
        var packed = Packing.Read<float[]>(message, "videoParams");
        var paths = Packing.Read<string[]>(message, "videoPaths");

        // F2: the ABI's own signature.
        Check(
            NativeMethods.ApplyVideos(
                Alive,
                (uint)keys.Length,
                keys,
                Packing.Read<int[]>(message, "videoFlags"),
                packed,
                (nuint)packed.Length,
                paths,
                (uint)paths.Length),
            "apply_videos");
    }

    private void ApplyMaterials(OrderedDictionary<string, object> message)
    {
        var keys = Packing.Read<long[]>(message, "materialKeys");
        var packed = Packing.Read<float[]>(message, "materialParams");
        var maps = Packing.Read<int[]>(message, "materialMaps");
        var texturePaths = Packing.Read<string[]>(message, "texturePaths");

        // F2: the ABI's own signature.
        Check(
            NativeMethods.ApplyMaterials(
                Alive,
                (uint)keys.Length,
                keys,
                Packing.Read<int[]>(message, "materialFlags"),
                packed,
                (nuint)packed.Length,
                maps,
                (nuint)maps.Length,
                texturePaths,
                Packing.Read<int[]>(message, "textureSrgb"),
                (uint)texturePaths.Length,
                Packing.Read<int[]>(message, "materialVideos")),
            "apply_materials");
    }

    private void ApplyDecals(OrderedDictionary<string, object> message)
    {
        var images = Packing.Read<int[]>(message, "decalImages");
        var packed = Packing.Read<float[]>(message, "decalParams");
        var paths = Packing.Read<string[]>(message, "decalPaths");

        // F2: the ABI's own signature.
        Check(
            NativeMethods.ApplyDecals(Alive, (uint)images.Length, packed, (nuint)packed.Length, images, paths, (uint)paths.Length),
            "apply_decals");
    }

    private void ApplyProbes(OrderedDictionary<string, object> message)
    {
        var keys = Packing.Read<long[]>(message, "probeKeys");
        var packed = Packing.Read<float[]>(message, "probeParams");
        Check(NativeMethods.ApplyProbes(Alive, (uint)keys.Length, keys, packed, (nuint)packed.Length), "apply_probes");
    }

    private void ApplyField(OrderedDictionary<string, object> message)
    {
        var packed = Packing.Read<float[]>(message, "fieldParams");
        Check(NativeMethods.ApplyField(Alive, packed, (nuint)packed.Length, Packing.Read<string>(message, "fieldFrom")), "apply_field");
    }

    private void SetOutline(OrderedDictionary<string, object> message)
    {
        var keys = Packing.Read<long[]>(message, "outlineKeys");
        var packed = Packing.Read<float[]>(message, "outlineParams");
        Check(NativeMethods.SetOutline(Alive, keys, (uint)keys.Length, packed, (nuint)packed.Length), "set_outline");
    }

    private void ApplyObjects(OrderedDictionary<string, object> message)
    {
        var keys = Packing.Read<long[]>(message, "objectKeys");
        var transforms = Packing.Read<float[]>(message, "transforms");
        var colours = Packing.Read<float[]>(message, "colours");
        var weights = Packing.Read<float[]>(message, "objectMorphWeights");
        var paths = Packing.Read<string[]>(message, "meshPaths");

        // F2: the ABI's own signature.
        Check(
            NativeMethods.ApplyObjects(
                Alive,
                (uint)keys.Length,
                keys,
                transforms,
                (nuint)transforms.Length,
                colours,
                (nuint)colours.Length,
                Packing.Read<int[]>(message, "meshes"),
                Packing.Read<int[]>(message, "objectFlags"),
                Packing.Read<int[]>(message, "objectMaterials"),
                Packing.Read<int[]>(message, "objectMorphCounts"),
                weights,
                (nuint)weights.Length,
                paths,
                (uint)paths.Length),
            "apply_objects");
    }

    private void ApplyPoses(OrderedDictionary<string, object> message, double at)
    {
        var keys = Packing.Read<long[]>(message, "poseKeys", []);
        var ints = Packing.Read<int[]>(message, "poseInts", []);
        var floats = Packing.Read<float[]>(message, "poseFloats", []);
        var joints = Packing.Read<int[]>(message, "poseJoints", []);
        var jointTransforms = Packing.Read<float[]>(message, "poseJointTransforms", []);

        // F2: the ABI's own signature.
        Check(
            NativeMethods.ApplyPoses(
                Alive,
                (uint)keys.Length,
                keys,
                ints,
                (nuint)ints.Length,
                floats,
                (nuint)floats.Length,
                Packing.Read<int[]>(message, "poseJointCounts", []),
                joints,
                (nuint)joints.Length,
                jointTransforms,
                (nuint)jointTransforms.Length,
                at),
            "apply_poses");
    }

    /// <summary>
    /// The populations. A call that names none clears every one the renderer holds, so it is made
    /// when the scene has lost its last, and skipped when there is nothing to lose.
    /// </summary>
    private void ApplyPopulations(OrderedDictionary<string, object> message)
    {
        if (!message.ContainsKey("populationKeys") && !_held.HasPopulations)
        {
            return;
        }

        var keys = Packing.Read<int[]>(message, "populationKeys", []);
        var bounds = Packing.Read<float[]>(message, "populationBounds", []);
        var paths = Packing.Read<string[]>(message, "populationPaths", []);
        var changed = Packing.Read<int[]>(message, "populationChanged", []);
        var transforms = Packing.Read<float[]>(message, "populationTransforms", []);
        var colours = Packing.Read<float[]>(message, "populationColours", []);

        // F2: the ABI's own signature.
        Check(
            NativeMethods.ApplyPopulations(
                Alive,
                (uint)keys.Length,
                keys,
                Packing.Read<int[]>(message, "populationCounts", []),
                Packing.Read<int[]>(message, "populationMeshes", []),
                Packing.Read<int[]>(message, "populationFlags", []),
                Packing.Read<int[]>(message, "populationRevisions", []),
                Packing.Read<float[]>(message, "populationRanges", []),
                bounds,
                (nuint)bounds.Length,
                paths,
                (uint)paths.Length,
                changed,
                (uint)changed.Length,
                transforms,
                (nuint)transforms.Length,
                colours,
                (nuint)colours.Length),
            "apply_populations");
    }

    /// <summary>
    /// The clouds. Skipped when there are none and the renderer holds none, so a scene without
    /// them costs nothing here.
    /// </summary>
    private void ApplySplats(OrderedDictionary<string, object> message)
    {
        if (!message.ContainsKey("splatKeys") && !_held.HasSplats)
        {
            return;
        }

        var keys = Packing.Read<int[]>(message, "splatKeys", []);
        var parameters = Packing.Read<float[]>(message, "splatParams", []);
        var paths = Packing.Read<string[]>(message, "splatPaths", []);
        var changed = Packing.Read<int[]>(message, "splatChanged", []);
        var data = Packing.Read<byte[]>(message, "splatData", []);

        // F2: the ABI's own signature.
        Check(
            NativeMethods.ApplySplats(
                Alive,
                (uint)keys.Length,
                keys,
                Packing.Read<int[]>(message, "splatFlags", []),
                Packing.Read<int[]>(message, "splatRevisions", []),
                parameters,
                (nuint)parameters.Length,
                paths,
                (uint)paths.Length,
                changed,
                Packing.Read<int[]>(message, "splatChangedCounts", []),
                (uint)changed.Length,
                data,
                (nuint)data.Length),
            "apply_splats");
    }

    /// <summary>
    /// The sprite layers. Skipped when there are none and the renderer holds none, so a scene
    /// without them costs nothing here.
    /// </summary>
    private void ApplySprites(OrderedDictionary<string, object> message)
    {
        if (!message.ContainsKey("spriteKeys") && !_held.HasSprites)
        {
            return;
        }

        var keys = Packing.Read<int[]>(message, "spriteKeys", []);
        var parameters = Packing.Read<float[]>(message, "spriteParams", []);
        var paths = Packing.Read<string[]>(message, "spritePaths", []);
        var changed = Packing.Read<int[]>(message, "spriteChanged", []);
        var records = Packing.Read<float[]>(message, "spriteData", []);

        // F2: the ABI's own signature.
        Check(
            NativeMethods.ApplySprites(
                Alive,
                (uint)keys.Length,
                keys,
                Packing.Read<int[]>(message, "spriteFlags", []),
                Packing.Read<int[]>(message, "spriteOrders", []),
                Packing.Read<int[]>(message, "spriteRevisions", []),
                parameters,
                (nuint)parameters.Length,
                paths,
                (uint)paths.Length,
                changed,
                Packing.Read<int[]>(message, "spriteChangedCounts", []),
                (uint)changed.Length,
                records,
                (nuint)records.Length),
            "apply_sprites");
    }

    /// <summary>The terrain. Always called: arrays with nothing in them take the terrain away.</summary>
    private void ApplyTerrain(OrderedDictionary<string, object> message)
    {
        var ints = Packing.Read<int[]>(message, "terrainInts", []);
        var floats = Packing.Read<float[]>(message, "terrainFloats", []);
        var data = Packing.Read<byte[]>(message, "terrainData", []);
        Check(
            NativeMethods.ApplyTerrain(Alive, ints, (nuint)ints.Length, floats, (nuint)floats.Length, data, (nuint)data.Length),
            "apply_terrain");
    }

    private void ApplyLights(OrderedDictionary<string, object> message)
    {
        var keys = Packing.Read<long[]>(message, "lightKeys");
        var packed = Packing.Read<float[]>(message, "lightParams");

        Check(
            NativeMethods.ApplyLights(
                Alive,
                (uint)keys.Length,
                keys,
                Packing.Read<int[]>(message, "lightKinds"),
                Packing.Read<int[]>(message, "lightFlags"),
                packed,
                (nuint)packed.Length),
            "apply_lights");
    }

    /// <summary>
    /// The sky, the fog, the image work and the pipeline, then the weather. The order is the
    /// renderer's own, because the pipeline reads what the fog and the post process set.
    /// </summary>
    private void ApplyAir(RenderScene scene, OrderedDictionary<string, object> message)
    {
        var sky = scene.Sky;
        Check(
            NativeMethods.SetSkyColour(Alive, Packing.Read<float[]>(message, "skyColour"), (float)sky.Ambient, sky.ShowBody ? 1 : 0),
            "set_sky_colour");

        var fog = Packing.Read<float[]>(message, "fogParams");
        Check(NativeMethods.SetFog(Alive, scene.Fog.IsVisible ? 1 : 0, fog, (nuint)fog.Length), "set_fog");

        var post = Packing.Read<float[]>(message, "postParams");
        Check(NativeMethods.SetPostProcess(Alive, post, (nuint)post.Length), "set_post_process");

        var pipeline = Packing.Read<float[]>(message, "pipelineParams");
        Check(NativeMethods.SetPipeline(Alive, pipeline, (nuint)pipeline.Length), "set_pipeline");

        var precipitation = Packing.Read<float[]>(message, "precipitationParams");
        Check(
            NativeMethods.SetPrecipitation(Alive, scene.Precipitation.IsVisible ? 1 : 0, precipitation, (nuint)precipitation.Length),
            "set_precipitation");

        var packed = Packing.Read<float[]>(message, "skyParams");
        Check(NativeMethods.SetSky(Alive, sky.Drawn ? 1 : 0, packed, (nuint)packed.Length), "set_sky");
    }

    private void SetCamera(RenderCamera camera, OrderedDictionary<string, object> message, double at)
    {
        SetCamera(
            Packing.Read<float[]>(message, "cameraPosition"),
            Packing.Read<float[]>(message, "cameraTarget"),
            (float)camera.FieldOfView,
            camera.Orthographic,
            (float)camera.ViewHeight,
            at);
        Check(NativeMethods.SetExposure(Alive, (float)camera.Aperture, (float)camera.ShutterSpeed, (float)camera.Sensitivity), "set_exposure");
    }
}

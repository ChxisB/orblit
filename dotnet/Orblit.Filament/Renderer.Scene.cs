namespace Orblit.Filament;

public sealed partial class Renderer
{
    /// <summary>
    /// Describes the scene to the renderer, whole. The calls go in the order the renderer's own
    /// scene holder makes them, because a later call reads what an earlier one set. Poses are sent
    /// even when no object has any, so an object that stops animating returns to rest.
    /// </summary>
    public void Apply(RenderScene scene, double at = 0)
    {
        var message = scene.ToMessage(at);

        Check(NativeMethods.SetBatching(Alive, scene.Batching ? 1 : 0), "set_batching");
        Check(NativeMethods.SetDepthPrepass(Alive, scene.DepthPrepass ? 1 : 0), "set_depth_prepass");
        ApplyObjects(message);
        ApplyPoses(message, at);
        ApplyLights(message);
        ApplyAir(scene, message);
        SetCamera(scene.Camera, message, at);
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

    private void ApplyAir(RenderScene scene, OrderedDictionary<string, object> message)
    {
        var sky = scene.Sky;
        Check(
            NativeMethods.SetSkyColour(Alive, Packing.Read<float[]>(message, "skyColour"), (float)sky.Ambient, sky.ShowBody ? 1 : 0),
            "set_sky_colour");

        var fog = Packing.Read<float[]>(message, "fogParams");
        Check(NativeMethods.SetFog(Alive, scene.Fog.IsVisible ? 1 : 0, fog, (nuint)fog.Length), "set_fog");

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

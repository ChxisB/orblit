namespace Orblit.Filament;

/// <summary>How a scene becomes the flat arrays the renderer reads, one topic at a time.</summary>
internal static class SceneMessage
{
    /// <summary>The objects, and the mesh paths they point at.</summary>
    internal static void Objects(IReadOnlyList<RenderObject> objects, IDictionary<string, object> into)
    {
        var columns = new ObjectColumns(objects.Count);
        for (var i = 0; i < objects.Count; i++)
        {
            columns.Set(i, objects[i]);
        }

        into["objectKeys"] = columns.Keys;
        into["transforms"] = columns.Transforms;
        into["colours"] = columns.Colours;
        into["meshes"] = columns.Meshes;
        into["objectFlags"] = columns.Flags;
        into["objectMorphCounts"] = columns.MorphCounts;
        into["objectMorphWeights"] = columns.MorphWeights;
        into["meshPaths"] = columns.Paths.ToArray();
        into["objectMaterials"] = columns.Materials;
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

    /// <summary>
    /// The object columns. Paths are sent once and pointed at by index, because the same mesh is
    /// usually on many objects. Morph weights are packed end to end with a count each, because a
    /// face rig has dozens and a crate has none, and a width that suits both is wrong for both.
    /// </summary>
    private sealed class ObjectColumns(int count)
    {
        private readonly Dictionary<string, int> _pathAt = [];
        private readonly List<float> _weights = [];

        internal long[] Keys { get; } = new long[count];

        internal float[] Transforms { get; } = new float[count * 16];

        internal float[] Colours { get; } = new float[count * 3];

        internal int[] Meshes { get; } = new int[count];

        internal int[] Flags { get; } = new int[count];

        internal int[] Materials { get; } = Enumerable.Repeat(-1, count).ToArray();

        internal int[] MorphCounts { get; } = new int[count];

        internal List<string> Paths { get; } = [];

        /// <summary>Never empty, because the far side takes a pointer and an empty array has none to give.</summary>
        internal float[] MorphWeights => _weights.Count == 0 ? [0f] : _weights.ToArray();

        internal void Set(int i, RenderObject one)
        {
            Keys[i] = one.Key;
            Meshes[i] = one.Mesh is null ? -1 : PathIndex(one.Mesh);
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

        private int PathIndex(string path)
        {
            if (_pathAt.TryGetValue(path, out var at))
            {
                return at;
            }

            _pathAt.Add(path, Paths.Count);
            Paths.Add(path);
            return Paths.Count - 1;
        }
    }
}

using System.Collections.Frozen;
using Orblit.Light;
using Orblit.Weather;

namespace Orblit.Scene;

/// <summary>
/// One step from an older scene format to the next.
/// </summary>
/// <remarks>
/// A list of these rather than conversions scattered through the reader. The difference shows the
/// third time the format moves: inline conversions have to be read in the reverse order they were
/// written, each one guarding on a version, and a field touched by two of them is only correct if
/// they happen to be in the right order in the function. As steps they compose by construction, a
/// version-one file runs every one of them in turn and arrives at the same place a version-three
/// file does after running the last.
/// <para>
/// Each works on decoded JSON rather than on objects. That is deliberate: a migration's whole job is
/// to read a shape this version of the code has no type for, and giving it one would mean keeping
/// every retired type alive forever.
/// </para>
/// </remarks>
public abstract class SceneMigration
{
    // Internal so the steps in SceneMigrations are the only ones there are.
    internal SceneMigration()
    {
    }

    /// <summary>The version this reads.</summary>
    public abstract int From { get; }

    /// <summary>The version it writes, always <see cref="From"/> plus one.</summary>
    public int To => From + 1;

    /// <summary>
    /// The file, moved on one version, as a new map: what comes in may be somebody's clipboard, and
    /// is not this migration's to change.
    /// <para>
    /// Anything worth telling somebody about goes in <paramref name="notes"/>. A migration that
    /// silently changes what a scene looks like is worse than one that refuses: people believe what
    /// they see, and a scene that quietly re-lit itself gets re-authored around the change.
    /// </para>
    /// </summary>
    public abstract JsonMap Apply(JsonMap json, List<string> notes);
}

/// <summary>Every migration there is, oldest first.</summary>
public static class SceneMigrations
{
    public static readonly IReadOnlyList<SceneMigration> Ordered =
    [
        new PowerInWatts(),
        new FogOnTheScene(),
        new ObjectsWithKinds(),
        new PrefabsAsCopies(),
    ];

    /// <summary>
    /// A file at <paramref name="version"/>, brought up to <see cref="SceneDocument.FormatVersion"/>.
    /// <para>
    /// The shape that predates versioning is normalised first, because it has no honest version of
    /// its own: it declares whatever it was written beside.
    /// </para>
    /// </summary>
    public static JsonMap Run(JsonMap json, long version, List<string> notes)
    {
        var (current, start) = BeforeVersions(json, version, notes);
        foreach (var migration in Ordered)
        {
            if (migration.From >= start)
            {
                current = migration.Apply(current, notes);
            }
        }

        return current;
    }

    /// <summary>
    /// One object from an older file, as an entity.
    /// <para>
    /// For the things that are made of scene objects without being scenes: what is on the clipboard,
    /// and what a prefab file holds. Both carry the format version they were written at and both
    /// have to survive one changing, and neither should grow its own half-copy of the conversion.
    /// The day those two disagree with the file's is the day pasting an object loses a field that
    /// saving it keeps.
    /// </para>
    /// <para>
    /// Runs the whole chain by wrapping the object in the scene it would be alone in, so a
    /// version-one object gets version one's light conversion exactly as a version-one file's would.
    /// Returns null when there is nothing usable in it.
    /// </para>
    /// </summary>
    public static JsonMap? Entity(JsonMap source, long version, List<string>? notes = null)
    {
        if (version >= SceneDocument.FormatVersion)
        {
            return source;
        }

        var migrated = Run(
            new OrderedDictionary<string, object?>
            {
                ["formatVersion"] = version,
                // Version four is where the list took the name it has now.
                [version >= 4 ? "entities" : "objects"] = new List<object?> { source },
            },
            version,
            notes ?? []);

        return Values.At(migrated, "entities") is JsonList { Count: > 0 } entities && entities[0] is JsonMap first
            ? first
            : null;
    }

    /// <summary>
    /// Scenes written before objects carried transforms.
    /// <para>
    /// The shape only existed briefly and it holds names but no positions, so it is converted rather
    /// than refused, and the conversion says what it could not recover instead of leaving somebody to
    /// wonder why everything is stacked at the origin.
    /// </para>
    /// <para>
    /// It takes the name and nothing else. The scene's own settings did not exist yet, and a file
    /// from then carrying a <c>sky</c> key is carrying one that meant something else.
    /// </para>
    /// <para>
    /// Gated on the version because version four calls its list <c>entities</c> too, and means
    /// something entirely different by it. The two can only be told apart by what the file says it is.
    /// </para>
    /// <para>
    /// It reports version three back, whatever the file claimed. The claim is not trustworthy, this
    /// shape predates the versions and files carrying it declare whatever number was current when
    /// they were written, and what it produces is already the version-three shape: ids, names and
    /// kinds, no fog block and no light stated in watts. Running the earlier migrations over it would
    /// divide a default sun's power by a sphere it was never spread on and hand somebody a scene
    /// twelve times darker than the one they saved.
    /// </para>
    /// </summary>
    private static (JsonMap Json, long Version) BeforeVersions(JsonMap json, long version, List<string> notes)
    {
        if (version >= 4 || Values.At(json, "objects") is not null || Values.At(json, "entities") is not JsonList entries)
        {
            return (json, version);
        }

        var objects = new List<object?>();
        foreach (var (index, entry) in entries.Index())
        {
            if (entry is JsonMap map)
            {
                objects.Add(LegacyObject(map, index));
            }
        }

        if (objects.Count > 0)
        {
            notes.Add(
                $"This scene was written before Orblit stored positions, so its {objects.Count} objects are all at the origin.");
        }

        var converted = Values.Pruned(new OrderedDictionary<string, object?>
        {
            ["name"] = Values.At(json, "name"),
            ["objects"] = objects,
        });
        return (converted, 3);
    }

    private static OrderedDictionary<string, object?> LegacyObject(JsonMap entry, int index)
    {
        var named = Values.At(entry, "components") is JsonList components ? string.Join(' ', components) : string.Empty;
        return new OrderedDictionary<string, object?>
        {
            ["id"] = $"legacy{index}",
            ["name"] = Values.Text(entry, "name") ?? "Object",
            // The old shape said what components a thing had, which is enough to tell a light from a
            // camera from everything else.
            ["kind"] = named.Contains("Light", StringComparison.Ordinal)
                ? "light"
                : named.Contains("Camera", StringComparison.Ordinal) ? "camera" : "mesh",
        };
    }

    /// <summary>
    /// Version one stated every light's power in watts.
    /// <para>
    /// And converted them all as if they were bulbs, a wattage spread over a sphere. A sun is not a
    /// bulb a long way off: it is a sheet of light arriving parallel, stated in watts per square
    /// metre. Version two tells them apart, so the same look is the old number over that sphere.
    /// </para>
    /// </summary>
    private sealed class PowerInWatts : SceneMigration
    {
        public override int From => 1;

        public override JsonMap Apply(JsonMap json, List<string> notes)
        {
            if (Values.At(json, "objects") is not JsonList objects)
            {
                return json;
            }

            var converted = 0;
            var next = new List<object?>(objects.Count);
            foreach (var entry in objects)
            {
                if (entry is JsonMap light && IsSun(light))
                {
                    var copy = new OrderedDictionary<string, object?>(light)
                    {
                        ["power"] = Values.Number(light, "power", 1000) / (4 * Math.PI),
                    };
                    next.Add(copy);
                    converted++;
                }
                else
                {
                    next.Add(entry);
                }
            }

            if (converted == 0)
            {
                return json;
            }

            notes.Add(
                converted == 1
                    ? "One sun in this scene was stated in watts, and is now stated in watts per square metre. It is the same brightness."
                    : $"{converted} suns in this scene were stated in watts, and are now stated in watts per square metre. They are the same brightness.");
            return new OrderedDictionary<string, object?>(json) { ["objects"] = next };
        }

        // Every light in a version-one file was drawn as a sun, whatever it called itself, so that is
        // what an unnamed one is read back as.
        private static bool IsSun(JsonMap entry) =>
            Values.At(entry, "kind") is "light" && (Values.Text(entry, "lightType") ?? "sun") == "sun";
    }

    /// <summary>
    /// Version two kept the air in a block on the scene itself.
    /// <para>
    /// Weather is a thing that changes, and a change needs somewhere to live that can hold both ends
    /// of it, a set of fields on the scene can only hold one. So the old fog becomes an object, which
    /// version four then makes a component.
    /// </para>
    /// </summary>
    private sealed class FogOnTheScene : SceneMigration
    {
        public override int From => 2;

        public override JsonMap Apply(JsonMap json, List<string> notes)
        {
            var fog = Values.Object(Values.At(json, "fog"));
            var density = Values.Number(fog, "density", 0);
            var next = new OrderedDictionary<string, object?>(json);
            next.Remove("fog");
            if (density <= 0 || Values.At(next, "objects") is not JsonList objects)
            {
                return next;
            }

            var taken = objects.OfType<JsonMap>().Select(entry => Values.Text(entry, "id")).ToHashSet();
            next["objects"] = new List<object?>(objects) { Weather(fog, density, FreeId(taken, "weather")) };

            notes.Add("The fog in this scene is now a Weather object, which can also do cloud and wind.");
            return next;
        }

        private static OrderedDictionary<string, object?> Weather(JsonMap fog, double density, string id)
        {
            var mist = Values.Number(fog, "mist", 0);
            var air = new WeatherState(
                // Nothing in the old shape said anything about cloud, so a converted scene starts with
                // a clear sky over its fog.
                CloudCover: 0,
                FogColour: Values.ReadTint(Values.At(fog, "colour"), Tint.Hex(0x7D8794)),
                FogDensity: density,
                FogHeight: Values.Number(fog, "height", 0),
                FogFalloff: Values.Number(fog, "falloff", 0.2),
                Mist: mist,
                MistSize: Values.Number(fog, "mistSize", 30),
                // The old drift was a rate the layer breathed at rather than a speed across the
                // ground. Four metres a second per unit of it is what makes a scene look about as
                // windy as it did.
                WindSpeed: Values.Number(fog, "mistSpeed", 0.08) * 4);

            return new OrderedDictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = "Weather",
                ["kind"] = "weather",
                ["condition"] = EnumNames.Of(mist > 0 ? WeatherCondition.Misty : WeatherCondition.Hazy),
                ["air"] = WeatherComponent.AirToJson(air),
            };
        }

        /// <summary>An id nothing is already using, for an object a conversion invents.</summary>
        private static string FreeId(HashSet<string?> taken, string wanted)
        {
            if (!taken.Contains(wanted))
            {
                return wanted;
            }

            var attempt = 2;
            while (taken.Contains($"{wanted}{attempt}"))
            {
                attempt++;
            }

            return $"{wanted}{attempt}";
        }
    }

    /// <summary>
    /// Version three said what an object <em>was</em>, and carried every kind's fields on every object.
    /// <para>
    /// A light held a mesh path it would never use; a camera held a shadow flag. Worse, a thing that
    /// was two of them at once could not be said at all, a lamp is a mesh and a light, and the old
    /// shape made somebody build it out of two objects and keep them in step by hand. Version four
    /// gives an entity a set of components and drops the kind.
    /// </para>
    /// </summary>
    private sealed class ObjectsWithKinds : SceneMigration
    {
        /// <summary>The kinds version three had, and what each one becomes.</summary>
        private static readonly FrozenSet<string> Kinds =
            new[] { "scene", "mesh", "light", "camera", "group", "weather", "canvas", "shape" }.ToFrozenSet();

        public override int From => 3;

        public override JsonMap Apply(JsonMap json, List<string> notes)
        {
            if (Values.At(json, "objects") is not JsonList objects)
            {
                throw new SceneFormatException("This scene has no list of objects in it.");
            }

            var entities = new List<object?>();
            var seen = new HashSet<string>();
            for (var index = 0; index < objects.Count; index++)
            {
                if (Checked(objects[index], index, seen, notes) is var (entry, id, kind))
                {
                    entities.Add(Entity(entry, id, kind));
                }
            }

            return Values.Pruned(new OrderedDictionary<string, object?>
            {
                ["name"] = Values.At(json, "name"),
                ["sky"] = Values.At(json, "sky"),
                ["ambient"] = Values.At(json, "ambient"),
                ["time"] = Values.At(json, "time"),
                ["entities"] = entities,
            });
        }

        // The object when it can become an entity, otherwise null with the reason in the notes.
        private static (JsonMap Entry, string Id, string Kind)? Checked(
            object? raw,
            int index,
            HashSet<string> seen,
            List<string> notes)
        {
            if (raw is not JsonMap entry)
            {
                notes.Add($"Object {index} is not an object, and was left out.");
                return null;
            }

            var id = Values.Text(entry, "id");
            if (string.IsNullOrEmpty(id))
            {
                notes.Add($"Object {index} has no id, and was left out.");
                return null;
            }

            if (!seen.Add(id))
            {
                notes.Add($"Two objects share the id \"{id}\"; the second was dropped.");
                return null;
            }

            if (Values.At(entry, "kind") is string kind && Kinds.Contains(kind))
            {
                return (entry, id, kind);
            }

            notes.Add(
                Values.At(entry, "kind") is string unknown
                    ? $"Object \"{id}\" is a \"{unknown}\", which this editor does not know about. It was left out."
                    : $"Object \"{id}\" has no kind this editor can read. It was left out.");
            return null;
        }

        /// <summary>One version-three object as an entity with components.</summary>
        private static OrderedDictionary<string, object?> Entity(JsonMap json, string id, string kind)
        {
            var components = new OrderedDictionary<string, object?>
            {
                // Every object in version three carried a transform, including the ones that had no
                // use for one, so every entity out of it gets one. Reading the defaults in rather
                // than leaving them out keeps a converted scene identical to what it was, which
                // matters more here than tidiness.
                [SceneComponents.Transform] = new OrderedDictionary<string, object?>
                {
                    ["position"] = Values.VectorToJson(Values.Vector(Values.At(json, "position"))),
                    ["rotation"] = Values.VectorToJson(Values.Vector(Values.At(json, "rotation"))),
                    ["scale"] = Values.VectorToJson(Values.Vector(Values.At(json, "scale"), 1)),
                },
            };

            // Version three had two drawable kinds, and the difference between them was whether the
            // geometry was this document's to edit.
            if (kind is "mesh" or "shape")
            {
                components[SceneComponents.Mesh] = Mesh(json, authored: kind == "shape");
            }

            Typed(components, json, kind);
            Carried(components, json, kind);

            return Values.Pruned(new OrderedDictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = Values.Text(json, "name") ?? id,
                ["parent"] = Values.At(json, "parent"),
                ["visible"] = Values.Flag(json, "visible", true) ? null : false,
                ["components"] = components,
            });
        }

        private static OrderedDictionary<string, object?> Mesh(JsonMap json, bool authored)
        {
            var sway = Values.Number(json, "sway", 0);
            return Values.Pruned(new OrderedDictionary<string, object?>
            {
                ["asset"] = Values.At(json, "mesh"),
                ["shape"] = Values.At(json, "shape"),
                ["geometry"] = Values.At(json, "geometry"),
                ["outline"] = Values.At(json, "outline"),
                ["boundary"] = Values.At(json, "boundary"),
                ["surfaces"] = Values.At(json, "surfaces"),
                ["colour"] = Values.TintToJson(Values.ReadTint(Values.At(json, "colour"))),
                ["castShadows"] = Values.Flag(json, "castShadows", true),
                ["receiveShadows"] = Values.Flag(json, "receiveShadows", true),
                ["sway"] = sway > 0 ? sway : null,
                ["authored"] = authored ? true : null,
            });
        }

        // The components only one kind had.
        private static void Typed(OrderedDictionary<string, object?> components, JsonMap json, string kind)
        {
            switch (kind)
            {
                case "light":
                    components[SceneComponents.Light] = Light(json);
                    break;
                case "camera":
                    components[SceneComponents.Camera] = new CameraComponent().ToJson();
                    break;
                case "weather":
                    components[SceneComponents.Weather] = Weather(json);
                    break;
            }
        }

        private static OrderedDictionary<string, object?> Light(JsonMap json) => new()
        {
            // Version three called this `lightType`; a component is already a light, so the field
            // says which sort rather than repeating itself.
            ["kind"] = Values.Text(json, "lightType") ?? "sun",
            ["power"] = Values.Number(json, "power", 1000),
            ["colour"] = Values.TintToJson(Values.ReadTint(Values.At(json, "colour"))),
            ["spotSize"] = Values.Number(json, "spotSize", 45),
            ["spotBlend"] = Values.Number(json, "spotBlend", 0.15),
            ["sourceRadius"] = Values.Number(json, "sourceRadius", 0.1),
            ["sunAngle"] = Values.Number(json, "sunAngle", 0.526),
            ["body"] = Values.Text(json, "body") ?? "sun",
            ["castShadows"] = Values.Flag(json, "castShadows", true),
        };

        private static OrderedDictionary<string, object?> Weather(JsonMap json)
        {
            var condition = Values.Text(json, "condition") ?? "clear";
            var air = WeatherComponent.AirFromJson(
                Values.At(json, "air"),
                Values.Named<WeatherCondition>(condition) ?? WeatherCondition.Clear);
            return Values.Pruned(new OrderedDictionary<string, object?>
            {
                ["condition"] = condition,
                ["cloudKind"] = Values.At(json, "cloudKind"),
                ["windDirection"] = Values.Number(json, "windDirection", 135),
                ["transition"] = Values.Number(json, "transition", 8),
                ["air"] = WeatherComponent.AirToJson(air),
            });
        }

        // A material, an interface and data files were never tied to a kind in version three, so they
        // are carried across for whatever object had them rather than only for the kind that usually
        // did.
        private static void Carried(OrderedDictionary<string, object?> components, JsonMap json, string kind)
        {
            if (Values.At(json, "material") is string material)
            {
                components[SceneComponents.Material] = new OrderedDictionary<string, object?> { ["asset"] = material };
            }

            if (kind == "canvas" || Values.At(json, "interface") is string)
            {
                components[SceneComponents.Canvas] = Values.Pruned(new OrderedDictionary<string, object?>
                {
                    ["asset"] = Values.At(json, "interface"),
                    ["scale"] = 1.0,
                });
            }

            if (Values.At(json, "prefab") is string prefab)
            {
                components[SceneComponents.Prefab] = new OrderedDictionary<string, object?> { ["asset"] = prefab };
            }

            var data = Values.Texts(Values.At(json, "data"));
            if (data.Count > 0)
            {
                components[SceneComponents.Data] = new OrderedDictionary<string, object?> { ["paths"] = data };
            }
        }
    }

    /// <summary>
    /// Version four stamped a copy of a prefab into the scene.
    /// <para>
    /// Every part of an instance was written out in full under an id of its own, each carrying the
    /// prefab's path, and the only link back was that path, so a change to the prefab reached an
    /// instance only by being pushed over it, and took the instance's own changes with it. Version
    /// five keeps a link and a diff instead.
    /// </para>
    /// <para>
    /// Turning a copy into that needs the prefab, to see which of the copy's parts are the prefab's
    /// and what they changed about it, and a migration cannot open files. So this only marks the
    /// copies as copies, and opening a document's instances relinks them once the prefab can be read.
    /// Until then each part is an ordinary entity that draws exactly as it did, and a file saved in
    /// between says the same thing it said before.
    /// </para>
    /// </summary>
    private sealed class PrefabsAsCopies : SceneMigration
    {
        public override int From => 4;

        public override JsonMap Apply(JsonMap json, List<string> notes) =>
            Values.At(json, "entities") is JsonList entities
                ? new OrderedDictionary<string, object?>(json) { ["entities"] = entities.Select(Marked).ToList() }
                : json;

        // The entry with its link marked, in new maps.
        private static object? Marked(object? entry)
        {
            if (entry is not JsonMap map
                || Values.At(map, "components") is not JsonMap components
                || Values.At(components, SceneComponents.Prefab) is not JsonMap prefab)
            {
                return entry;
            }

            var marked = new OrderedDictionary<string, object?>(components)
            {
                [SceneComponents.Prefab] = new OrderedDictionary<string, object?>(prefab)
                {
                    ["state"] = EnumNames.Of(PrefabState.Stamped),
                },
            };
            return new OrderedDictionary<string, object?>(map) { ["components"] = marked };
        }
    }
}

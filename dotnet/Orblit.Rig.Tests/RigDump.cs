using System.Text.Json.Nodes;
using static Orblit.Parity.Dump;

namespace Orblit.Rig.Tests;

/// <summary>Writes what the rig port computed in the shape the fixture exporter wrote.</summary>
internal static class RigDump
{
    public static JsonNode Vec(Vector3 v) => new JsonArray(Num(v.X), Num(v.Y), Num(v.Z));

    public static JsonNode Quat(Quaternion q) => new JsonArray(Num(q.X), Num(q.Y), Num(q.Z), Num(q.W));

    public static JsonNode Mat(Matrix4 m) => Arr(Enumerable.Range(0, 16), index => Num(m[index]));

    /// <summary>An enum member the way the fixture spells it: PascalCase becomes camelCase.</summary>
    public static string Lower(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    public static JsonNode? NullableText(string? value) => value is null ? null : Text(value);

    public static JsonObject Map<T>(IEnumerable<KeyValuePair<string, T>> entries, Func<T, JsonNode?> convert) =>
        new(entries.Select(entry => KeyValuePair.Create(entry.Key, convert(entry.Value))));

    public static JsonNode IkDump(IkSolution s) => new JsonObject
    {
        ["joint"] = Vec(s.Joint),
        ["end"] = Vec(s.End),
        ["reachable"] = s.Reachable,
        ["stretch"] = Num(s.Stretch),
    };

    public static JsonNode PoseDump(Pose pose) => new JsonObject
    {
        ["order"] = Arr(pose.EvaluationOrder, Text),
        ["bones"] = Arr(pose.Armature.Bones, bone => new JsonObject
        {
            ["name"] = Text(bone.Name),
            ["base"] = Mat(pose.BaseOf(bone.Name)),
            ["world"] = Mat(pose.WorldOf(bone.Name)),
            ["head"] = Vec(pose.HeadOf(bone.Name)),
            ["tail"] = Vec(pose.TailOf(bone.Name)),
            ["skin"] = Mat(pose.SkinningOf(bone.Name)),
        }),
    };

    public static JsonNode WidgetJson(BoneWidget w) => new JsonObject
    {
        ["shape"] = Text(Lower(w.Shape.ToString())),
        ["size"] = Num(w.Size),
        ["offset"] = Vec(w.Offset),
        ["roll"] = Num(w.Roll),
    };

    public static JsonNode ConstraintJson(BoneConstraint c) => new JsonObject
    {
        ["kind"] = Text(c.GetType().Name),
        ["influence"] = Num(c.Influence),
        ["influenceProperty"] = NullableText(c.InfluenceProperty),
        ["invertInfluence"] = c.InvertInfluence,
        ["dependencies"] = Arr(c.Dependencies, Text),
    };

    public static JsonNode ChainJson(IkChain c) => new JsonObject
    {
        ["root"] = Text(c.Root),
        ["mid"] = Text(c.Mid),
        ["target"] = Text(c.Target),
        ["pole"] = NullableText(c.Pole),
        ["influence"] = Num(c.Influence),
        ["influenceProperty"] = NullableText(c.InfluenceProperty),
        ["stretch"] = Num(c.Stretch),
        ["stretchProperty"] = NullableText(c.StretchProperty),
    };

    public static JsonNode CollectionJson(BoneCollections collections, BoneCollection c) => new JsonObject
    {
        ["name"] = Text(c.Name),
        ["visible"] = c.Visible,
        ["colour"] = c.Colour is { } colour ? Big(colour) : null,
        ["members"] = Arr(collections.MembersOf(c.Name).Order(StringComparer.Ordinal), Text),
    };
}

using System.Text.Json.Nodes;
using static Orblit.Parity.Dump;

namespace Orblit.Sprite.Tests;

/// <summary>Writes what the sprite port computed in the shape the Dart exporter wrote.</summary>
internal static class SpriteDump
{
    public static string Base64(byte[] bytes) => Convert.ToBase64String(bytes);

    public static JsonObject Region(Region r) => new()
    {
        ["name"] = r.Name,
        ["x"] = Big(r.X),
        ["y"] = Big(r.Y),
        ["width"] = Big(r.Width),
        ["height"] = Big(r.Height),
        ["rotated"] = r.Rotated,
        ["trimmed"] = r.Trimmed,
        ["offsetX"] = Big(r.OffsetX),
        ["offsetY"] = Big(r.OffsetY),
        ["sourceWidth"] = Big(r.SourceWidth),
        ["sourceHeight"] = Big(r.SourceHeight),
    };

    public static JsonObject? Atlas(Atlas? a) => a is null
        ? null
        : new JsonObject
        {
            ["image"] = a.Image,
            ["width"] = Big(a.Width),
            ["height"] = Big(a.Height),
            ["regions"] = Arr(a.Regions.Values, Region),
        };

    public static JsonObject Rect((long X, long Y, long Width, long Height)? r) => r is not { } box
        ? new JsonObject { ["none"] = true }
        : new JsonObject
        {
            ["x"] = Big(box.X),
            ["y"] = Big(box.Y),
            ["width"] = Big(box.Width),
            ["height"] = Big(box.Height),
        };
}

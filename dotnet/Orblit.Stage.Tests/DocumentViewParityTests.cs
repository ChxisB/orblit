using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Filament;
using Orblit.Parity;
using Orblit.Scene;
using Orblit.VectorMath;
using static Orblit.Parity.Dump;

namespace Orblit.Stage.Tests;

public class DocumentViewParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_stage", "document_view");

    [Fact]
    public void FirstStagingMatchesTheFixture() =>
        Expected.Check("stage", c => Dumped(new DocumentView(Read(c.GetProperty("in").GetString()!)).Scene));

    [Fact]
    public void AReplacedDocumentMatchesTheFixture() => Expected.Check("replace", c =>
    {
        var texts = c.GetProperty("in");
        var view = new DocumentView(Read(texts[0].GetString()!));
        view.Replace(Read(texts[1].GetString()!));
        return Dumped(view.Scene);
    });

    [Fact]
    public void ALongRunOfReplacementsMatchesTheFixture() => Expected.Check("run", c =>
    {
        var documents = c.GetProperty("in").EnumerateArray().Select(text => Read(text.GetString()!)).ToList();
        var view = new DocumentView(documents[0]);
        var seen = new List<JsonNode?> { Dumped(view.Scene) };
        foreach (var next in documents.Skip(1))
        {
            view.Replace(next);
            seen.Add(Dumped(view.Scene));
        }

        return new JsonArray([.. seen]);
    });

    private static SceneDocument Read(string text) => SceneDocument.Decode(text).Document;

    private static JsonObject Dumped(RenderScene scene) => new JsonObject
    {
        ["objects"] = Arr(scene.Objects, Object),
        ["lights"] = Arr(scene.Lights, Light),
        ["sprites"] = Arr(scene.Sprites, Sprite),
        ["splats"] = Arr(scene.Splats, Splat),
        ["sky"] = new JsonObject { ["colour"] = Point(scene.Sky.Colour), ["ambient"] = Num(scene.Sky.Ambient) },
        ["fog"] = new JsonObject
        {
            ["colour"] = Point(scene.Fog.Colour),
            ["density"] = Num(scene.Fog.Density),
            ["height"] = Num(scene.Fog.Height),
        },
        ["precipitation"] = new JsonObject
        {
            ["amount"] = Num(scene.Precipitation.Amount),
            ["fall"] = Num(scene.Precipitation.Fall),
            ["wind"] = new JsonArray(Num(scene.Precipitation.Wind.X), Num(scene.Precipitation.Wind.Y)),
        },
        ["camera"] = new JsonObject
        {
            ["position"] = Point(scene.Camera.Position),
            ["target"] = Point(scene.Camera.Target),
            ["fieldOfView"] = Num(scene.Camera.FieldOfView),
        },
    };

    private static JsonObject Object(RenderObject one) => new JsonObject
    {
        ["key"] = Big(one.Key),
        ["transform"] = Cells(one.Transform),
        ["mesh"] = one.Mesh,
        ["visible"] = one.Visible,
        ["colour"] = Point(one.Colour),
        ["castShadows"] = one.CastShadows,
        ["material"] = one.Material,
    };

    private static JsonObject Light(RenderLight one) => new JsonObject
    {
        ["key"] = Big(one.Key),
        ["kind"] = (int)one.Kind,
        ["intensity"] = Num(one.Intensity),
        ["colour"] = Point(one.Colour),
        ["position"] = Point(one.Position),
        ["direction"] = Point(one.Direction),
        ["falloffRadius"] = Num(one.FalloffRadius),
        ["innerConeAngle"] = Num(one.InnerConeAngle),
        ["outerConeAngle"] = Num(one.OuterConeAngle),
        ["sunAngularRadius"] = Num(one.SunAngularRadius),
        ["sourceRadius"] = Num(one.SourceRadius),
        ["haloSize"] = Num(one.HaloSize),
        ["haloFalloff"] = Num(one.HaloFalloff),
        ["castShadows"] = one.CastShadows,
        ["width"] = Num(one.Width),
        ["height"] = Num(one.Height),
    };

    private static JsonObject Sprite(SpriteLayer one) => new JsonObject
    {
        ["key"] = Big(one.Key),
        ["transform"] = Cells(one.Transform),
        ["blend"] = (int)one.Blend,
    };

    private static JsonObject Splat(SplatCloud one) => new JsonObject
    {
        ["key"] = Big(one.Key),
        ["path"] = one.Path,
        ["transform"] = Cells(one.Transform),
        ["harmonics"] = one.Harmonics,
        ["limit"] = one.Limit,
    };

    private static JsonArray Cells(Matrix4 matrix) => Arr(Enumerable.Range(0, 16), at => Num(matrix[at]));

    private static JsonArray Point(Vector3 point) => new(Num(point.X), Num(point.Y), Num(point.Z));
}

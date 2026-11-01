using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Orblit.Filament;
using Orblit.Parity;
using Orblit.VectorMath;
using static Orblit.Parity.Dump;

namespace Orblit.Stage.Avalonia.Tests;

public class PointingParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_stage", "pointing");

    private static Vector3 Read(JsonElement point) =>
        new(Fixture.Number(point[0]), Fixture.Number(point[1]), Fixture.Number(point[2]));

    private static JsonArray Dump3(Vector3 point) => new(Num(point.X), Num(point.Y), Num(point.Z));

    [Fact]
    public void ARayMatchesTheFixture() => Expected.Check("ray", c =>
    {
        var input = c.GetProperty("in");
        var camera = new RenderCamera
        {
            Position = Read(input.GetProperty("position")),
            Target = Read(input.GetProperty("target")),
            FieldOfView = Fixture.Number(input.GetProperty("fieldOfView")),
            Orthographic = input.GetProperty("orthographic").GetBoolean(),
            ViewHeight = Fixture.Number(input.GetProperty("viewHeight")),
        };
        var point = input.GetProperty("point");
        var view = input.GetProperty("view");

        var ray = PointerRay.FromCamera(
            camera,
            new Point(Fixture.Number(point[0]), Fixture.Number(point[1])),
            new Size(Fixture.Number(view[0]), Fixture.Number(view[1])));

        return new JsonObject { ["origin"] = Dump3(ray.Origin), ["direction"] = Dump3(ray.Direction) };
    });

    [Fact]
    public void APixelMatchesTheFixture() => Expected.Check("pixel", c =>
    {
        var input = c.GetProperty("in");
        var size = input.GetProperty("size");
        var transform = Matrix4.FromList([.. input.GetProperty("transform").EnumerateArray().Select(Fixture.Number)]);
        var wall = new WorldPanel(
            new FakePanel(new Size(Fixture.Number(size[0]), Fixture.Number(size[1]))),
            transform,
            new WorldPanelOptions { Id = "wall", ObjectKey = 1, MaterialKey = 2 });

        var pixel = wall.PixelAt(new PointerRay(Read(input.GetProperty("origin")), Read(input.GetProperty("direction"))));

        return pixel is { } found ? new JsonArray(Num(found.X), Num(found.Y)) : null;
    });
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Filament;
using Orblit.Particles;
using Orblit.Parity;
using Orblit.Scene;
using Orblit.VectorMath;
using static Orblit.Parity.Dump;

namespace Orblit.Stage.Tests;

public class ParticleParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_stage", "particles");

    [Fact]
    public void TheFramesOfAViewMatchTheFixture() => Expected.Check("particles", c =>
    {
        var input = c.GetProperty("in");
        var documents = input.GetProperty("documents").EnumerateArray().Select(text => Read(text.GetString()!)).ToList();
        var steps = input.GetProperty("steps").EnumerateArray().Select(step => step.GetDouble()).ToList();
        var view = new DocumentView(documents[0]);
        var particles = new ParticleView(view, input.GetProperty("key").GetInt32(), input.GetProperty("budget").GetInt32());

        var frames = new List<JsonNode?>();
        for (var i = 0; i < steps.Count; i++)
        {
            if (i > 0 && i < documents.Count)
            {
                view.Replace(documents[i]);
            }

            particles.Advance(steps[i]);
            frames.Add(Frame(particles));
        }

        return new JsonArray([.. frames]);
    });

    [Fact]
    public void ASheetMatchesTheFixture() => Expected.Check("sheet", c =>
    {
        var input = c.GetProperty("in");
        var columns = input.GetProperty("columns").GetInt32();
        var colour = input.GetProperty("colour").EnumerateArray().Select(one => one.GetDouble()).ToList();
        var sheet = new ParticleSheet(columns, input.GetProperty("rows").GetInt32(), input.GetProperty("spacing").GetDouble(), colour);

        return Posed(sheet.ToGlb(), sheet.Poses(i => ClothAt(columns, i)));
    });

    [Fact]
    public void ARibbonMatchesTheFixture() => Expected.Check("ribbon", c =>
    {
        var input = c.GetProperty("in");
        var sampled = input.GetProperty("sampled").GetInt32();
        var widths = input.GetProperty("widths").EnumerateArray().Select(one => one.GetDouble()).ToList();
        var eye = input.GetProperty("eye").EnumerateArray().Select(one => one.GetDouble()).ToList();
        var still = input.GetProperty("still").GetBoolean();

        var strip = new TrailStrip(sampled);
        new Trail(life: 1, rate: sampled - 1, width: widths)
            .SampleAlong(10, t => still ? new Vector3(1, 2, 3) : new Vector3(t * 3, t * t * 0.5, t * 0.3), strip);
        var ribbon = new TrailRibbon(input.GetProperty("points").GetInt32());

        return Posed(ribbon.ToGlb(), ribbon.Poses(strip, new Vector3(eye[0], eye[1], eye[2])));
    });

    private static SceneDocument Read(string text) => SceneDocument.Decode(text).Document;

    // The same arithmetic as the exporter, with no function of the libm in it, so the doubles agree.
    private static Vector3 ClothAt(int columns, int i)
    {
        var x = (i % columns) * 0.5;
        var y = -(i / columns) * 0.5;
        return new Vector3((x * 1.3) + (y * 0.4), y, ((i % 5) * 0.07) + ((i / 3) * 0.02));
    }

    private static JsonObject Frame(ParticleView particles) => new()
    {
        ["populations"] = Arr(particles.Populations, Population),
        ["rejected"] = Arr(particles.Rejected.Keys, Text),
    };

    private static JsonObject Population(Population one) => new()
    {
        ["key"] = Big(one.Key),
        ["revision"] = Big(one.Revision),
        ["count"] = one.Count,
        ["transforms"] = Arr(one.Transforms, value => Num(value)),
        ["colours"] = Arr(one.Colours, value => Num(value)),
        ["minimum"] = Point(one.Bounds.Minimum),
        ["maximum"] = Point(one.Bounds.Maximum),
        ["fade"] = (int)one.Fade,
        ["receiveShadows"] = one.ReceiveShadows,
    };

    private static JsonObject Posed(byte[] glb, IReadOnlyList<JointPose> poses) => new()
    {
        ["glb"] = Convert.ToBase64String(glb),
        ["poses"] = Arr(poses, pose => new JsonObject
        {
            ["skin"] = pose.Skin,
            ["joint"] = pose.Joint,
            ["transform"] = Cells(pose.Transform),
        }),
    };

    private static JsonArray Cells(Matrix4 matrix) => Arr(Enumerable.Range(0, 16), at => Num(matrix[at]));

    private static JsonArray Point(Vector3 point) => new(Num(point.X), Num(point.Y), Num(point.Z));
}

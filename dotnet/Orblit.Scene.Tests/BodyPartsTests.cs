using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class BodyPartsTests
{
    [Fact]
    public void CompoundGeometryRoundTripsWithoutLosingLocalPlacement()
    {
        var part = new BodyPart(
            BodyShape.Cylinder,
            radius: 0.3,
            height: 2,
            centre: new Vector3(1, 2, 3),
            rotation: Quaternion.AxisAngle(new Vector3(0, 0, 1), 0.7),
            scale: new Vector3(2, 1, 0.5));
        var body = new BodyComponent(
            shape: BodyShape.Compound,
            parts: [part],
            centre: new Vector3(0, 1, 0),
            shapeScale: new Vector3(1, 2, 1));
        var restored = BodyComponent.FromJson(body.ToJson());

        Same(body.ToJson(), restored.ToJson());
        Same(part.ToJson(), restored.CopyWith(mass: 3).Parts.Single().ToJson());
        Assert.Equal(new Vector3(1, 2, 1), restored.ShapeScale);
    }

    [Fact]
    public void OldFilesKeepTheirShapeAndGeometryScaleDefaults()
    {
        var body = BodyComponent.FromJson(Map(("shape", "sphere"), ("radius", 2L)));

        Assert.Equal(BodyShape.Sphere, body.Shape);
        Assert.Equal(Vector3.All(1), body.ShapeScale);
        Assert.Empty(body.Parts);

        var part = BodyPart.FromJson(Map(("centre", List(3L, 4L)), ("rotation", "bad")));

        Assert.Equal(new Vector3(3, 4, 0), part.Centre);
        Assert.Equal(Quaternion.Identity(), part.Rotation);
        Assert.Equal(Vector3.All(1), part.Scale);
    }

    [Fact]
    public void ThePartAndHullListsAreImmutableAndConstructorInputsAreCopied()
    {
        var centre = new Vector3(1, 0, 0);
        var points = new List<double> { 0, 1, 2 };
        var parts = new List<BodyPart> { new(centre: centre, hull: points) };
        var body = new BodyComponent(shape: BodyShape.Compound, parts: parts);
        centre.X = 7;
        points[0] = 9;
        parts.Clear();

        Assert.Equal(1, body.Parts.Single().Centre.X);
        Assert.Equal(0, body.Parts.Single().Hull[0]);
        Assert.Throws<NotSupportedException>(() => ((ICollection<BodyPart>)body.Parts).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<double>)body.Parts.Single().Hull).Clear());
    }
}

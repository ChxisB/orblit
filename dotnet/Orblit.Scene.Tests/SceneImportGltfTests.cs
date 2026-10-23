using System.Buffers.Binary;
using Orblit.Light;
using Orblit.Mesh;
using static Orblit.Scene.Tests.ImportDocs;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SceneImportGltfTests
{
    private const string DataPrefix = "data:application/octet-stream;base64,";

    private static byte[] CubeGlb() =>
        Shape.Of(ShapeKind.Cube).Build().ToGlb("Block", [new GlbMaterial { Name = "Paint" }]);

    private static SceneImported RoundTrip(SceneDocument scene) =>
        SceneImport.ReadSceneFrom(scene.WriteAs(SceneFormat.Glb).First.Bytes);

    private static T ComponentOf<T>(SceneEntity entity, string type)
        where T : SceneComponent => Assert.IsType<T>(entity[type]);

    private static OrderedDictionary<string, object?> Buffer(byte[] bytes) =>
        Map(("byteLength", (long)bytes.Length), ("uri", DataPrefix + Convert.ToBase64String(bytes)));

    private static OrderedDictionary<string, object?> View(long offset, long length, long? stride = null)
    {
        var view = Map(("buffer", 0L), ("byteOffset", offset), ("byteLength", length));
        if (stride is not null)
        {
            view["byteStride"] = stride;
        }

        return view;
    }

    private static OrderedDictionary<string, object?> Accessor(long view, int component, long count, string type) =>
        Map(("bufferView", view), ("componentType", (long)component), ("count", count), ("type", type));

    private static OrderedDictionary<string, object?> PositionOnly() =>
        Map(("primitives", List(Map(("attributes", Map(("POSITION", 0L)))))));

    // a scene that went out as glTF comes back

    [Fact]
    public void AnEmptyOneKeepsItsNameAndItsSettings()
    {
        var scene = new SceneDocument("Nothing", new SceneSettings(ambient: 900, timeOfDay: 3));
        var read = RoundTrip(scene);

        Assert.Equal("Nothing", read.Document.Name);
        Assert.Equal(900, read.Document.Settings.Ambient);
        Assert.Equal(3, read.Document.Settings.TimeOfDay);
    }

    [Fact]
    public void TheWholeFileComesBackByteForByte()
    {
        // The strongest thing there is to assert here, and the one the phase's acceptance criterion
        // actually asks for: not that the import is reasonable, but that it is the same scene.
        var scene = Populated();
        var read = RoundTrip(scene);

        Assert.True(read.WasWrittenHere);
        Assert.Empty(read.Problems);
        Assert.Equal(scene.Encode(), read.Document.Encode());
    }

    [Fact]
    public void IdsOrderAndParentageSurvive()
    {
        var read = RoundTrip(Populated());

        Assert.Equal(["yard", "wall", "lamp", "sun", "eye", "capture"], read.Document.Entities.Select(one => one.Id));
        Assert.Equal("yard", read.Document["wall"]!.Parent);
        Assert.Null(read.Document["sun"]!.Parent);
    }

    [Fact]
    public void HiddenIsStillHidden()
    {
        var read = RoundTrip(Populated());

        Assert.False(read.Document["eye"]!.Visible);
        Assert.True(read.Document["wall"]!.Visible);
    }

    [Fact]
    public void AComponentThisVersionHasNeverHeardOfSurvives()
    {
        // The case that decides whether a team can share a project across two editor versions.
        var odd = new UnknownComponent("buoyancy", Map(("litres", 12L), ("sealed", true)));
        var scene = new SceneDocument(entities: [Entity("odd", Parts(("buoyancy", odd)))]);
        var read = RoundTrip(scene);

        Same(Map(("litres", 12L), ("sealed", true)), read.Document["odd"]!["buoyancy"]!.ToJson());
    }

    [Fact]
    public void AGltfWithItsBufferBesideItReadsTheSameAsAGlb()
    {
        var scene = Populated();
        var written = scene.WriteAs(SceneFormat.Gltf, "old town");

        var read = SceneImport.ReadSceneFrom(
            written.First.Bytes,
            files: written.Files.Skip(1).ToDictionary(file => file.Name, file => file.Bytes));

        Assert.Equal(scene.Encode(), read.Document.Encode());
    }

    [Fact]
    public void TheGraftedCopyOfAModelDoesNotBecomeEntities()
    {
        // An imported model is copied into the document whole and its roots become children of the
        // node that draws it. Those nodes are geometry, not things somebody put in the scene, and
        // importing them as entities would double the outliner every time a scene made a round trip.
        var scene = new SceneDocument(entities:
        [
            Entity("tree", Parts((SceneComponents.Mesh, new MeshComponent(asset: "models/tree.glb"))), name: "Tree"),
        ]);

        var written = scene.WriteAs(SceneFormat.Glb, files: new Dictionary<string, byte[]> { ["models/tree.glb"] = CubeGlb() });
        var nodes = Assert.IsAssignableFrom<JsonList>(GltfFiles.GlbChunks(written.First.Bytes)!.Value.Json["nodes"]);
        Assert.True(nodes.Count > 1, "the model was grafted in, or this test proves nothing");

        var read = SceneImport.ReadSceneFrom(written.First.Bytes);
        Assert.Equal(["tree"], read.Document.Entities.Select(one => one.Id));
        Assert.Equal("models/tree.glb", ComponentOf<MeshComponent>(read.Document["tree"]!, "mesh").Asset);
    }

    [Fact]
    public void AnOpenInstanceComesBackOpenItsPartsWhereTheyWere()
    {
        var scene = new SceneDocument(entities:
        [
            Entity("lamp1", Parts((SceneComponents.Prefab, new PrefabComponent(asset: "props/lamp.oprefab", state: PrefabState.Open)))),
            Entity(
                "lamp1/bulb",
                Parts((SceneComponents.Transform, new TransformComponent(position: new Vector3(0, 2, 0)))),
                parent: "lamp1"),
        ]);
        var read = RoundTrip(scene);

        Assert.Equal(["lamp1", "lamp1/bulb"], read.Document.Entities.Select(one => one.Id));
        Assert.Equal("lamp1", read.Document["lamp1/bulb"]!.Parent);
        var link = ComponentOf<PrefabComponent>(read.Document["lamp1"]!, "prefab");
        Assert.Equal("props/lamp.oprefab", link.Asset);
        Assert.Equal(PrefabState.Open, link.State);
    }

    [Fact]
    public void OneWrittenBeforeInstancesWereLinksComesBackAsTheCopyItWas()
    {
        // Every part carried the link then, and the scene said no version, because the exporter did
        // not write one down yet.
        var link = Map(("prefab", Map(("asset", "props/lamp.oprefab"))));
        var read = SceneImport.ReadSceneFrom(Document(
            ("scenes", List(Map(("nodes", List(0L)), ("extras", Map(("orblit", Map(("settings", Map())))))))),
            ("nodes", List(Recorded("lamp", [1], link), Recorded("bulb", null, link)))));

        Assert.Equal(["lamp", "bulb"], read.Document.Entities.Select(one => one.Id));
        foreach (var one in read.Document.Entities)
        {
            Assert.True(ComponentOf<PrefabComponent>(one, "prefab").State == PrefabState.Stamped, one.Id);
        }
    }

    // a glTF from somewhere else

    [Fact]
    public void NodesBecomeEntitiesWithTheirNamesAndTheirTree()
    {
        var read = SceneImport.ReadSceneFrom(Document(
            ("scene", 0L),
            ("scenes", List(Map(("name", "Imported"), ("nodes", List(0L))))),
            ("nodes", List(Foreign("Root", children: [1]), Foreign("Child", translation: [1, 2, 3])))));

        Assert.False(read.WasWrittenHere);
        Assert.Equal("Imported", read.Document.Name);
        Assert.Equal(2, read.Document.Entities.Count);
        Assert.Equal("Root", read.Document.Entities[0].Name);
        Assert.Equal(read.Document.Entities[0].Id, read.Document.Entities[1].Parent);
        Assert.Equal(new Vector3(1, 2, 3), ComponentOf<TransformComponent>(read.Document.Entities[1], "transform").Position);
        Assert.True(read.Problems.Count == 1, "it should say it guessed");
    }

    [Fact]
    public void AQuaternionComesBackAsTheAnglesThatMadeIt()
    {
        // The exporter composes Z, then Y, then X. This is that taken apart again, and it is the one
        // piece of arithmetic here worth distrusting.
        const double X = 12.0, Y = 34.0, Z = 56.0;
        var m = Matrix4.RotationZ(Doubles.Radians(Z));
        m.Multiply(Matrix4.RotationY(Doubles.Radians(Y)));
        m.Multiply(Matrix4.RotationX(Doubles.Radians(X)));
        var q = Quaternion.FromRotation(m.GetRotation());
        q.Normalize();

        var read = SceneImport.ReadSceneFrom(Document(("nodes", List(Foreign("Turned", rotation: [q.X, q.Y, q.Z, q.W])))));

        var turned = ComponentOf<TransformComponent>(Assert.Single(read.Document.Entities), "transform");
        Assert.Equal(X, turned.Rotation.X, 1e-9);
        Assert.Equal(Y, turned.Rotation.Y, 1e-9);
        Assert.Equal(Z, turned.Rotation.Z, 1e-9);
    }

    [Fact]
    public void AMatrixIsTakenApartIntoWhereWhichWayAndHowBig()
    {
        var m = Matrix4.TranslationValues(5, 6, 7);
        m.Multiply(Matrix4.RotationY(Doubles.Radians(40)));
        m.Multiply(Matrix4.Diagonal3Values(2, 3, 4));

        var read = SceneImport.ReadSceneFrom(Document(
            ("nodes", List(Foreign("Placed", matrix: [.. Enumerable.Range(0, 16).Select(i => m[i])])))));

        var placed = ComponentOf<TransformComponent>(Assert.Single(read.Document.Entities), "transform");
        Assert.Equal(5, placed.Position.X, 1e-9);
        Assert.Equal(6, placed.Position.Y, 1e-9);
        Assert.Equal(7, placed.Position.Z, 1e-9);
        Assert.Equal(2, placed.Scale.X, 1e-9);
        Assert.Equal(3, placed.Scale.Y, 1e-9);
        Assert.Equal(4, placed.Scale.Z, 1e-9);
        Assert.Equal(0, placed.Rotation.X, 1e-9);
        Assert.Equal(40, placed.Rotation.Y, 1e-9);
        Assert.Equal(0, placed.Rotation.Z, 1e-9);
    }

    [Fact]
    public void APunctualLightComesBackTheBrightnessItLeft()
    {
        var sunLight = Map(
            ("type", "directional"),
            ("color", List(1.0, 1.0, 1.0)),
            ("intensity", 1.2 * Photometry.LuminousEfficacy));
        var spotLight = Map(
            ("type", "spot"),
            ("color", List(1.0, 1.0, 1.0)),
            ("intensity", Photometry.LumensToCandela(Photometry.WattsToLumens(60))),
            ("spot", Map(
                ("innerConeAngle", Doubles.Radians(35) * 0.7),
                ("outerConeAngle", Doubles.Radians(35)))));
        var read = SceneImport.ReadSceneFrom(Document(
            ("extensions", Map(("KHR_lights_punctual", Map(("lights", List(sunLight, spotLight)))))),
            ("nodes", List(Foreign("Sun", light: 0), Foreign("Lamp", light: 1)))));

        var sun = ComponentOf<LightComponent>(read.Document.Entities[0], "light");
        Assert.Equal(LightType.Sun, sun.Kind);
        Assert.Equal(1.2, sun.Power, 1e-9);

        var lamp = ComponentOf<LightComponent>(read.Document.Entities[1], "light");
        Assert.Equal(LightType.Spot, lamp.Kind);
        Assert.Equal(60, lamp.Power, 1e-9);
        Assert.Equal(70, lamp.SpotSize, 1e-9);
        Assert.Equal(0.3, lamp.SpotBlend, 1e-9);
    }

    [Fact]
    public void AnAreaLightIsOneAgainBecauseTheExporterSaidSo()
    {
        var softbox = Map(
            ("type", "point"),
            ("color", List(1.0, 1.0, 1.0)),
            ("intensity", 1L),
            ("extras", Map(("orblit", Map(("kind", "area"), ("sourceRadius", 0.75))))));
        var read = SceneImport.ReadSceneFrom(Document(
            ("extensions", Map(("KHR_lights_punctual", Map(("lights", List(softbox)))))),
            ("nodes", List(Foreign("Softbox", light: 0)))));

        var light = ComponentOf<LightComponent>(Assert.Single(read.Document.Entities), "light");
        Assert.Equal(LightType.Area, light.Kind);
        Assert.Equal(0.75, light.SourceRadius);
    }

    [Fact]
    public void ACameraComesBackAsOne()
    {
        var lens = Map(
            ("type", "perspective"),
            ("perspective", Map(("yfov", Doubles.Radians(65)), ("znear", 0.2), ("zfar", 800L))));
        var read = SceneImport.ReadSceneFrom(Document(
            ("cameras", List(lens)),
            ("nodes", List(Foreign("Eye", camera: 0)))));

        var eye = ComponentOf<CameraComponent>(Assert.Single(read.Document.Entities), "camera");
        Assert.Equal(65, eye.FieldOfView, 1e-9);
        Assert.Equal(0.2, eye.Near);
        Assert.Equal(800, eye.Far);
    }

    [Fact]
    public void TrianglesBecomeGeometrySomebodyCanStillEdit()
    {
        var bytes = new byte[42];
        float[] corners = [0, 0, 0, 1, 0, 0, 0, 1, 0];
        for (var i = 0; i < corners.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), corners[i]);
        }

        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(36), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(38), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40), 2);

        var read = SceneImport.ReadSceneFrom(Document(
            ("buffers", List(Buffer(bytes))),
            ("bufferViews", List(View(0, 36), View(36, 6))),
            ("accessors", List(
                Accessor(0, GltfComponent.Float, 3, "VEC3"),
                Accessor(1, GltfComponent.UnsignedShort, 3, "SCALAR"))),
            ("materials", List(Map(("pbrMetallicRoughness", Map(("baseColorFactor", List(1.0, 0.0, 0.0, 1.0))))))),
            ("meshes", List(Map(("primitives", List(Map(
                ("attributes", Map(("POSITION", 0L))),
                ("indices", 1L),
                ("material", 0L))))))),
            ("nodes", List(Foreign("Triangle", mesh: 0)))));

        var mesh = ComponentOf<MeshComponent>(Assert.Single(read.Document.Entities), "mesh");
        Assert.True(mesh.Authored);
        Assert.Equal([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)], mesh.Geometry!.Positions);
        Assert.Equal([0, 1, 2], Assert.Single(mesh.Geometry.Faces).Vertices);
        Assert.Equal(1, mesh.Colour.Red, 1e-6);
        Assert.Equal(0, mesh.Colour.Green, 1e-6);
    }

    [Fact]
    public void AnInterleavedBufferViewIsReadAtItsStride()
    {
        // Reading a strided view as though it were packed gives geometry that is wrong rather than
        // geometry that is missing, which is much harder to notice, so it is worth a test of its own.
        var bytes = new byte[3 * 20];
        float[][] corners = [[1, 2, 3], [4, 5, 6], [7, 8, 9]];
        for (var i = 0; i < 3; i++)
        {
            for (var c = 0; c < 3; c++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan((i * 20) + (c * 4)), corners[i][c]);
            }

            // Where the texture coordinates would be, filled with something that is not a plausible position.
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan((i * 20) + 12), -999);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan((i * 20) + 16), -999);
        }

        var read = SceneImport.ReadSceneFrom(Document(
            ("buffers", List(Buffer(bytes))),
            ("bufferViews", List(View(0, bytes.Length, 20))),
            ("accessors", List(Accessor(0, GltfComponent.Float, 3, "VEC3"))),
            ("meshes", List(PositionOnly())),
            ("nodes", List(Foreign("Strided", mesh: 0)))));

        var mesh = ComponentOf<MeshComponent>(Assert.Single(read.Document.Entities), "mesh");
        Assert.Equal([new Vector3(1, 2, 3), new Vector3(4, 5, 6), new Vector3(7, 8, 9)], mesh.Geometry!.Positions);
    }

    [Fact]
    public void WhatItCouldNotCarryIsSaidNotSwallowed()
    {
        var flat = Map(
            ("type", "orthographic"),
            ("orthographic", Map(("xmag", 2L), ("ymag", 2L), ("znear", 0L), ("zfar", 9L))));
        var read = SceneImport.ReadSceneFrom(Document(
            ("cameras", List(flat)),
            ("nodes", List(Foreign("Flat", camera: 0)))));

        Assert.Equal(2, read.Problems.Count);
        Assert.Contains("not written by Orblit", read.Problems[0]);
        Assert.Contains("orthographic", read.Problems[^1]);
        Assert.True(Assert.Single(read.Document.Entities).Has("camera"));
    }

    [Fact]
    public void ANodeNamingALightThatIsNotThereIsNotACrash()
    {
        var read = SceneImport.ReadSceneFrom(Document(("nodes", List(Foreign("Ghost", light: 3)))));

        Assert.False(Assert.Single(read.Document.Entities).Has("light"));
        Assert.Contains(read.Problems, one => one.Contains("light 3", StringComparison.Ordinal));
    }

    // bytes that are not a scene

    [Fact]
    public void RubbishIsRefusedRatherThanReadAsAnEmptyScene() =>
        Assert.Throws<SceneFormatException>(() => SceneImport.ReadSceneFrom([7, 7, 7, 7, 7, 7]));

    [Fact]
    public void JsonThatIsNotADocumentIsRefused() =>
        Assert.Throws<SceneFormatException>(() => SceneImport.ReadSceneFrom(Bytes("[1, 2, 3]")));

    [Fact]
    public void ADocumentWithGeometryAndNoBufferSaysSo()
    {
        var read = SceneImport.ReadSceneFrom(Document(
            ("accessors", List(Map(("componentType", (long)GltfComponent.Float), ("count", 3L)))),
            ("meshes", List(PositionOnly())),
            ("nodes", List(Foreign("Hollow", mesh: 0)))));

        Assert.False(Assert.Single(read.Document.Entities).Has("mesh"));
        Assert.Contains(read.Problems, one => one.Contains("no buffer", StringComparison.Ordinal));
    }
}

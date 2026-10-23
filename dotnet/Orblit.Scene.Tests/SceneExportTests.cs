using System.Globalization;
using System.Text;
using Orblit.Json;
using Orblit.Light;
using Orblit.Mesh;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SceneExportTests
{
    private static SceneEntity Entity(
        string id,
        IReadOnlyDictionary<string, SceneComponent> components,
        string? parent = null,
        string? name = null,
        bool visible = true) => new(id, name ?? id, parent, visible, components);

    private static Dictionary<string, SceneComponent> Parts(params (string Type, SceneComponent Component)[] pairs) =>
        pairs.ToDictionary(pair => pair.Type, pair => pair.Component);

    private static JsonMap HeaderOf(byte[] glb) => GltfFiles.GlbChunks(glb)!.Value.Json;

    private static byte[] CubeGlb() =>
        Shape.Of(ShapeKind.Cube).Build().ToGlb("Block", [new GlbMaterial { Name = "Paint" }]);

    private static Shape CubeShape() => new() { Kind = ShapeKind.Cube };

    private static JsonMap Map1(object? value) => Assert.IsAssignableFrom<JsonMap>(value);

    private static JsonList List1(object? value) => Assert.IsAssignableFrom<JsonList>(value);

    private static double D(object? number) => Convert.ToDouble(number, CultureInfo.InvariantCulture);

    /// <summary>A tree with every number a double, so a whole number written as <c>1</c> equals <c>1.0</c>.</summary>
    private static object? Loose(object? node) => node switch
    {
        IReadOnlyDictionary<string, object?> map => map.ToDictionary(pair => pair.Key, pair => Loose(pair.Value)),
        IEnumerable<object?> list => list.Select(Loose).ToList(),
        long or int or float => Convert.ToDouble(node, CultureInfo.InvariantCulture),
        _ => node,
    };

    private static JsonMap FirstNode(SceneDocument scene) => Map1(List1(HeaderOf(scene.WriteAs(SceneFormat.Glb).First.Bytes)["nodes"])[0]);

    // the tree it writes

    [Fact]
    public void AnEmptySceneIsAFileEveryLoaderOpens()
    {
        var written = new SceneDocument(name: "Nothing").WriteAs(SceneFormat.Glb);
        var json = HeaderOf(written.First.Bytes);

        Assert.Single(List1(json["scenes"]));
        // Not an empty list: every array in glTF must hold something.
        Assert.DoesNotContain("nodes", Map1(List1(json["scenes"])[0]).Keys);
        Assert.False(json.ContainsKey("buffers"));
        Assert.False(json.ContainsKey("accessors"));
    }

    [Fact]
    public void AChildIsAChildAndOnlyTheRootsAreInTheScene()
    {
        var scene = new SceneDocument(entities:
        [
            Entity("a", Parts()),
            Entity("b", Parts(), parent: "a"),
            Entity("c", Parts(), parent: "b"),
            Entity("d", Parts()),
        ]);
        var json = HeaderOf(scene.WriteAs(SceneFormat.Glb).First.Bytes);
        var nodes = List1(json["nodes"]);

        Same(List(0L, 3L), Map1(List1(json["scenes"])[0])["nodes"]);
        Same(List(1L), Map1(nodes[0])["children"]);
        Same(List(2L), Map1(nodes[1])["children"]);
        Assert.DoesNotContain("children", Map1(nodes[2]).Keys);
    }

    [Fact]
    public void AnEntityWhoseParentIsNotInTheSceneIsARoot()
    {
        var scene = new SceneDocument(entities: [Entity("orphan", Parts(), parent: "gone")]);
        var json = HeaderOf(scene.WriteAs(SceneFormat.Glb).First.Bytes);

        Same(List(0L), Map1(List1(json["scenes"])[0])["nodes"]);
    }

    [Fact]
    public void ARotationIsTheOneTheSceneDrawsInTheOrderItDrawsIt()
    {
        // Three axes at once, because the order only shows itself when more than one is turned: any
        // order agrees about a single axis.
        var euler = new Vector3(20, 40, 60);
        var scene = new SceneDocument(entities:
        [
            Entity("e", Parts((SceneComponents.Transform, new TransformComponent(
                position: new Vector3(1, 2, 3),
                rotation: euler,
                scale: new Vector3(2, 2, 2))))),
        ]);
        var node = FirstNode(scene);

        Same(List(1L, 2L, 3L), node["translation"]);
        Same(List(2L, 2L, 2L), node["scale"]);

        // Z, then Y, then X: the order the view draws with, and so the only one that reopens a saved
        // scene the way it was saved.
        var turn = Matrix4.RotationZ(Doubles.Radians(euler.Z));
        turn.Multiply(Matrix4.RotationY(Doubles.Radians(euler.Y)));
        turn.Multiply(Matrix4.RotationX(Doubles.Radians(euler.X)));
        var expected = turn.GetRotation();

        var q = List1(node["rotation"]).Select(D).ToArray();
        var written = new Quaternion(q[0], q[1], q[2], q[3]).AsRotationMatrix();

        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                Assert.Equal(expected.Entry(i, j), written.Entry(i, j), 1e-12);
            }
        }
    }

    [Fact]
    public void ATransformThatDoesNothingIsNotWrittenDown()
    {
        var scene = new SceneDocument(entities: [Entity("e", Parts((SceneComponents.Transform, new TransformComponent())))]);
        var node = FirstNode(scene);

        Assert.False(node.ContainsKey("translation"));
        Assert.False(node.ContainsKey("rotation"));
        Assert.False(node.ContainsKey("scale"));
    }

    // the lights it writes

    private static JsonMap LightOf(LightComponent light)
    {
        var scene = new SceneDocument(entities: [Entity("l", Parts((SceneComponents.Light, light)))]);
        var json = HeaderOf(scene.WriteAs(SceneFormat.Glb).First.Bytes);
        var punctual = Map1(Map1(json["extensions"])["KHR_lights_punctual"]);
        return Map1(List1(punctual["lights"])[0]);
    }

    [Fact]
    public void ASunIsStatedInLux()
    {
        var light = LightOf(new LightComponent(kind: LightType.Sun, power: 2));

        Assert.Equal("directional", light["type"]);
        Assert.Equal(2 * 683, D(light["intensity"]), 1e-6);
    }

    [Fact]
    public void ALampIsStatedInCandela()
    {
        var light = LightOf(new LightComponent(kind: LightType.Point, power: 100));

        Assert.Equal("point", light["type"]);
        Assert.Equal(100 * 683 / (4 * Math.PI), D(light["intensity"]), 1e-6);
    }

    [Fact]
    public void ASpotStatesTheHalfAngleNotTheWholeCone()
    {
        var light = LightOf(new LightComponent(kind: LightType.Spot, power: 10, spotSize: 60, spotBlend: 0.5));
        var spot = Map1(light["spot"]);

        Assert.Equal(Doubles.Radians(30), D(spot["outerConeAngle"]), 1e-9);
        Assert.Equal(Doubles.Radians(15), D(spot["innerConeAngle"]), 1e-9);
    }

    [Fact]
    public void AConeWiderThanTheFormatAllowsIsBroughtBackToTheLimit()
    {
        var light = LightOf(new LightComponent(kind: LightType.Spot, power: 1, spotSize: 300));

        Assert.Equal(Math.PI / 2, D(Map1(light["spot"])["outerConeAngle"]), 1e-9);
    }

    [Fact]
    public void AnAreaLightBecomesAPointAndSaysItWasOne()
    {
        var light = LightOf(new LightComponent(kind: LightType.Area, power: 10, sourceRadius: 0.75));

        Assert.Equal("point", light["type"]);
        Assert.Equal(0.75, Map1(Map1(light["extras"])["orblit"])["sourceRadius"]);
    }

    [Fact]
    public void AColourIsLinearNotTheSrgbItWasPickedIn()
    {
        var light = LightOf(new LightComponent(kind: LightType.Point, colour: Tint.Hex(0x808080)));
        var colour = List1(light["color"]).Select(D).ToArray();

        Assert.True(colour[0] < 0.5);
        Assert.True(colour[0] > 0.2);
    }

    // the materials it writes

    private static MaterialLibrary Library() => new(new Dictionary<string, MaterialDocument>
    {
        ["a.omat"] = MaterialDocument.FromJson(Map(("values", Map(
            ("baseColour", List(0.2, 0.4, 0.6, 1.0)),
            ("roughness", 0.3),
            ("metallic", 1.0),
            ("blend", "masked"),
            ("maskThreshold", 0.25),
            ("doubleSided", true))))).Document,
        ["b.omat"] = MaterialDocument.FromJson(Map(("values", Map(
            ("emissive", List(1.0, 0.5, 0.0)),
            ("emissiveIntensity", 4.0),
            ("shading", "unlit"))))).Document,
    });

    private static JsonList MaterialsOf(SceneDocument scene) =>
        List1(HeaderOf(scene.WriteAs(SceneFormat.Glb, materials: Library()).First.Bytes)["materials"]);

    private static SceneDocument Wearing(params string?[] assets) => new(entities:
    [
        .. assets.Select((asset, i) => Entity($"e{i}", Parts(
            (SceneComponents.Mesh, new MeshComponent(shape: CubeShape(), colour: Tint.Hex((0x100000 * i) + 0x203040))),
            (SceneComponents.Material, new MaterialComponent(asset: asset))))),
    ]);

    [Fact]
    public void AResolvedMaterialBringsItsValuesAndItsExtensions()
    {
        var material = Map1(MaterialsOf(Wearing("a.omat"))[0]);
        var pbr = Map1(material["pbrMetallicRoughness"]);

        Same(List(0.2, 0.4, 0.6, 1L), pbr["baseColorFactor"]);
        Assert.Equal(0.3, pbr["roughnessFactor"]);
        Assert.Equal(1.0, D(pbr["metallicFactor"]));
        Assert.Equal("MASK", material["alphaMode"]);
        Assert.Equal(0.25, material["alphaCutoff"]);
        Assert.Equal(true, material["doubleSided"]);
    }

    [Fact]
    public void AGlowBrighterThanOneGoesInTheExtensionNotTheClamp()
    {
        var material = Map1(MaterialsOf(Wearing("b.omat"))[0]);
        var factor = List1(material["emissiveFactor"]).Select(D).ToArray();

        Assert.Equal(1.0, factor[0]);
        Assert.Equal(0.5, factor[1], 1e-9);

        var extensions = Map1(material["extensions"]);
        var strength = Map1(extensions["KHR_materials_emissive_strength"]);
        Assert.Equal(4.0, D(strength["emissiveStrength"]), 1e-9);
        Assert.True(extensions.ContainsKey("KHR_materials_unlit"));
    }

    [Fact]
    public void AColourTheMaterialOverridesIsNotASecondMaterial() =>
        // Three entities, three different mesh colours, one material that states its own base colour.
        // One material comes out.
        Assert.Single(MaterialsOf(Wearing("a.omat", "a.omat", "a.omat")));

    [Fact]
    public void AColourNothingOverridesStillTellsThemApart() =>
        Assert.Equal(2, MaterialsOf(Wearing(null, null)).Count);

    [Fact]
    public void AnUnresolvedMaterialIsReportedOnceNotPerEntity()
    {
        var written = Wearing("a.omat", "a.omat").WriteAs(SceneFormat.Glb);

        var problem = Assert.Single(written.Problems);
        Assert.Contains("a.omat", problem);
    }

    // the looks it writes

    [Fact]
    public void ALookIsAVariantAndEachEntityMapsIntoIt()
    {
        var scene = new SceneDocument(entities:
        [
            Entity("e", Parts(
                (SceneComponents.Mesh, new MeshComponent(shape: CubeShape())),
                (SceneComponents.Material, new MaterialComponent(
                    asset: "summer.omat",
                    looks: new OrderedDictionary<string, string> { ["winter"] = "snow.omat", ["night"] = "dark.omat" })))),
        ]);
        var json = HeaderOf(scene.WriteAs(SceneFormat.Glb).First.Bytes);

        var variants = Map1(Map1(json["extensions"])["KHR_materials_variants"]);
        Same(List(Map(("name", "winter")), Map(("name", "night"))), variants["variants"]);
        Assert.Contains("KHR_materials_variants", List1(json["extensionsUsed"]).Cast<string>());

        var primitive = List1(Map1(List1(json["meshes"])[0])["primitives"]);
        var mappings = Map1(Map1(Map1(primitive[0])["extensions"])["KHR_materials_variants"]);
        Assert.Equal(2, List1(mappings["mappings"]).Count);
    }

    // the models it copies in

    private static SceneDocument Placing(int times) => new(entities:
    [
        .. Enumerable.Range(0, times).Select(i =>
            Entity($"e{i}", Parts((SceneComponents.Mesh, new MeshComponent(asset: "block.glb"))))),
    ]);

    private static Dictionary<string, byte[]> Block(byte[] bytes) => new() { ["block.glb"] = bytes };

    [Fact]
    public void AnImportedModelArrivesWholeUnderTheNodeThatPlacesIt()
    {
        var json = HeaderOf(Placing(1).WriteAs(SceneFormat.Glb, files: Block(CubeGlb())).First.Bytes);
        var nodes = List1(json["nodes"]);

        Assert.Equal(2, nodes.Count);
        Same(List(1L), Map1(nodes[0])["children"]);
        Assert.Equal(0L, Map1(nodes[1])["mesh"]);
        Assert.Single(List1(json["meshes"]));
        Assert.Single(List1(json["materials"]));
    }

    [Fact]
    public void TheSameModelTwiceIsCopiedOnceAndPlacedTwice()
    {
        var once = Placing(1).WriteAs(SceneFormat.Glb, files: Block(CubeGlb()));
        var twice = Placing(2).WriteAs(SceneFormat.Glb, files: Block(CubeGlb()));
        var json = HeaderOf(twice.First.Bytes);

        Assert.Single(List1(json["meshes"]));
        Assert.Equal(List1(HeaderOf(once.First.Bytes)["accessors"]).Count, List1(json["accessors"]).Count);
        var nodes = List1(json["nodes"]).Select(Map1).ToList();
        Assert.Equal(nodes[2]["mesh"], nodes[3]["mesh"]);
        // The bytes are the model's, once, plus the few a second node costs.
        Assert.True(twice.First.Bytes.Length - once.First.Bytes.Length < once.First.Bytes.Length / 4);
    }

    [Fact]
    public void AModelWhoseBytesWereNotSuppliedIsReportedNotDropped()
    {
        var written = Placing(1).WriteAs(SceneFormat.Glb);

        Assert.Contains("block.glb", Assert.Single(written.Problems));
        Assert.Single(List1(HeaderOf(written.First.Bytes)["nodes"]));
    }

    [Fact]
    public void AModelThatIsNotAGlbIsRefusedByName()
    {
        var written = Placing(1).WriteAs(SceneFormat.Glb, files: Block(Encoding.UTF8.GetBytes("{\"asset\":{}}")));

        Assert.Contains("not a GLB", Assert.Single(written.Problems));
    }

    [Fact]
    public void AModelWhoseBytesLiveElsewhereIsRefusedWithAReason()
    {
        var loose = GltfFiles.GlbBytes(
            Map(
                ("asset", Map(("version", "2.0"))),
                ("buffers", List(Map(("uri", "beside.bin"), ("byteLength", 4L))))),
            []);
        var written = Placing(1).WriteAs(SceneFormat.Glb, files: Block(loose));

        Assert.Contains("beside.bin", Assert.Single(written.Problems));
    }

    // the round trip

    [Fact]
    public void EveryComponentComesBackExactlyAsItWentIn()
    {
        var components = Parts(
            (SceneComponents.Transform, new TransformComponent(
                position: new Vector3(1, 2, 3),
                rotation: new Vector3(10, 20, 30),
                scale: new Vector3(1, 2, 1))),
            (SceneComponents.Mesh, new MeshComponent(
                shape: new Shape { Kind = ShapeKind.Stairs },
                colour: Tint.Hex(0x123456),
                sway: 0.4,
                castShadows: false)),
            (SceneComponents.Material, new MaterialComponent(
                asset: "x.omat",
                looks: new OrderedDictionary<string, string> { ["winter"] = "snow.omat" })),
            (SceneComponents.Light, new LightComponent(kind: LightType.Spot, power: 12, spotSize: 33)),
            (SceneComponents.Camera, new CameraComponent(fieldOfView: 41)),
            (SceneComponents.Splats, new SplatsComponent(asset: "cloud.osplat", budget: 1000)));
        var scene = new SceneDocument(name: "Round trip", entities: [Entity("e", components, name: "Thing", visible: false)]);

        var node = FirstNode(scene);
        var kept = Map1(Map1(node["extras"])["orblit"]);

        Assert.Equal("e", kept["id"]);
        Assert.Equal(false, kept["visible"]);
        var back = Map1(kept["components"]);
        foreach (var (type, component) in components)
        {
            Same(Loose(component.ToJson()), Loose(back[type]));
        }
    }

    [Fact]
    public void TheSettingsRideOnTheScene()
    {
        var scene = new SceneDocument(settings: new SceneSettings(ambient: 4200, timeOfDay: 7.5));
        var json = HeaderOf(scene.WriteAs(SceneFormat.Glb).First.Bytes);
        var extras = Map1(Map1(List1(json["scenes"])[0])["extras"]);

        Same(Loose(scene.Settings.ToJson()), Loose(Map1(extras["orblit"])["settings"]));
    }

    // the files it hands back

    private static SceneDocument OneCube() => new(entities:
    [
        Entity("e", Parts((SceneComponents.Mesh, new MeshComponent(shape: CubeShape())))),
    ]);

    [Fact]
    public void AGltfComesWithTheBytesItPointsAt()
    {
        var written = OneCube().WriteAs(SceneFormat.Gltf, "yard");

        Assert.Equal(["yard.gltf", "yard.bin"], written.Files.Select(f => f.Name));
        var json = Map1(JsonText.Decode(written.First.Text));
        Same(
            Map(("uri", "yard.bin"), ("byteLength", (long)written.Files[1].Bytes.Length)),
            List1(json["buffers"])[0]);
    }

    [Fact]
    public void AGltfWithNothingToPointAtNamesNoBufferAndNoSidecar()
    {
        var written = new SceneDocument().WriteAs(SceneFormat.Gltf);

        Assert.Single(written.Files);
        Assert.DoesNotContain("buffers", written.First.Text);
    }

    [Fact]
    public void AGlbIsOneFile()
    {
        var written = OneCube().WriteAs(SceneFormat.Glb, "yard");

        Assert.Equal(["yard.glb"], written.Files.Select(f => f.Name));
        Assert.NotNull(GltfFiles.GlbChunks(written.First.Bytes));
    }

    // the obj it writes

    private static SceneDocument Two() => new(entities:
    [
        Entity(
            "a",
            Parts(
                (SceneComponents.Transform, new TransformComponent(position: new Vector3(10, 0, 0))),
                (SceneComponents.Mesh, new MeshComponent(shape: CubeShape()))),
            name: "First one"),
        Entity(
            "b",
            Parts(
                (SceneComponents.Transform, new TransformComponent(position: new Vector3(0, 5, 0))),
                (SceneComponents.Mesh, new MeshComponent(shape: CubeShape()))),
            parent: "a",
            name: "Second"),
    ]);

    [Fact]
    public void ItIsFlattenedIntoWorldSpaceAGroupEach()
    {
        var text = Two().WriteAs(SceneFormat.Obj).First.Text;
        Assert.Contains("o First_one", text);
        Assert.Contains("o Second", text);

        // The child sits at ten across and five up: its parent's place plus its own, because an OBJ
        // has no tree to hang it from.
        var corners = text.Split('\n').Where(line => line.StartsWith("v ", StringComparison.Ordinal)).ToList();
        foreach (var line in corners.Skip(corners.Count / 2))
        {
            var parts = line.Split(' ');
            Assert.True(double.Parse(parts[1], CultureInfo.InvariantCulture) > 9);
            Assert.True(double.Parse(parts[2], CultureInfo.InvariantCulture) > 4);
        }
    }

    [Fact]
    public void ANameWithASpaceInItDoesNotBecomeTwoNames() =>
        Assert.DoesNotContain("o First one", Two().WriteAs(SceneFormat.Obj).First.Text);

    [Fact]
    public void FacesCountFromOne()
    {
        var text = Two().WriteAs(SceneFormat.Obj).First.Text;
        var faces = text.Split('\n').Where(line => line.StartsWith("f ", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(faces);
        foreach (var face in faces)
        {
            foreach (var corner in face[2..].Split(' '))
            {
                Assert.True(int.Parse(corner.Split('/')[0], CultureInfo.InvariantCulture) > 0);
            }
        }
    }

    [Fact]
    public void ALibraryComesWithItAndTheSameMaterialIsWrittenOnce()
    {
        var written = Two().WriteAs(SceneFormat.Obj, "yard");

        Assert.Equal(["yard.obj", "yard.mtl"], written.Files.Select(f => f.Name));
        Assert.Contains("mtllib yard.mtl", written.First.Text);
        Assert.Equal(1, written.Files[1].Text.Split("newmtl").Length - 1);
    }

    [Fact]
    public void WhatItCannotCarryItSays()
    {
        var scene = new SceneDocument(entities:
        [
            Entity("l", Parts((SceneComponents.Light, new LightComponent(kind: LightType.Sun)))),
            Entity("m", Parts((SceneComponents.Mesh, new MeshComponent(asset: "tree.glb")))),
        ]);
        var problems = scene.WriteAs(SceneFormat.Obj).Problems;

        Assert.Equal(2, problems.Count);
        Assert.Contains("no word for", problems[0]);
        Assert.Contains("tree.glb", problems[1]);
    }
}

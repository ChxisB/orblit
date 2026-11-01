using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class MaterialTests
{
    private static MaterialDocument Doc(JsonMap json) => MaterialDocument.FromJson(json).Document;

    private static IReadOnlyList<string> Problems(JsonMap json) => MaterialDocument.FromJson(json).Problems;

    private static OrderedDictionary<string, MaterialDocument> Docs(params (string Path, MaterialDocument Material)[] pairs)
    {
        var docs = new OrderedDictionary<string, MaterialDocument>();
        foreach (var (path, material) in pairs)
        {
            docs[path] = material;
        }

        return docs;
    }

    private static OrderedDictionary<string, object?> Values(params (string Name, object? Value)[] pairs) =>
        Map(("values", Map(pairs)));

    private static IReadOnlyList<double> Numbers(MaterialDocument material, string name) =>
        Assert.IsAssignableFrom<IReadOnlyList<double>>(material.Values[name]);

    // Reading a material.
    [Fact]
    public void KeepsEveryParameterItUnderstandsAsTheKindItIs()
    {
        var material = Doc(Values(
            ("metallic", 1L),
            ("doubleSided", true),
            ("baseColour", List(1L, 0.5, 0L, 1L)),
            ("emissive", List(0.1, 0.2, 0.3)),
            ("tiling", List(2L, 3L)),
            ("blend", "fade")));

        Assert.Equal(1.0, material.Values["metallic"]);
        Assert.Equal(true, material.Values["doubleSided"]);
        Assert.Equal([1.0, 0.5, 0.0, 1.0], Numbers(material, "baseColour"));
        Assert.Equal([0.1, 0.2, 0.3], Numbers(material, "emissive"));
        Assert.Equal([2.0, 3.0], Numbers(material, "tiling"));
        Assert.Equal("fade", material.Values["blend"]);
    }

    [Fact]
    public void AnIntegerAndADoubleAreTheSameNumber()
    {
        Assert.Equal(1.0, Doc(Values(("roughness", 1L))).Values["roughness"]);
        Assert.Equal(1.0, Doc(Values(("roughness", 1.0))).Values["roughness"]);
    }

    [Fact]
    public void NamesAParameterItDoesNotKnowRatherThanKeepingIt()
    {
        var json = Values(("shinyness", 1L));

        Assert.Empty(Doc(json).Values);
        Assert.Contains("shinyness", Assert.Single(Problems(json)));
    }

    [Fact]
    public void DropsAValueOfTheWrongShapeAndSaysWhatWasWanted()
    {
        var json = Values(
            ("roughness", "half"),
            ("baseColour", List(1L, 1L, 1L)),
            ("blend", "shiny"),
            ("depthWrite", 1L));

        var problems = Problems(json);

        Assert.Empty(Doc(json).Values);
        Assert.Equal(4, problems.Count);
        Assert.Contains("a number", problems[0]);
        Assert.Contains("four numbers", problems[1]);
        Assert.Contains("opaque", problems[2]);
        Assert.Contains("true or false", problems[3]);
    }

    [Fact]
    public void OneBadParameterDoesNotLoseTheGoodOnes()
    {
        var material = Doc(Values(("roughness", "half"), ("metallic", 1L)));

        var kept = Assert.Single(material.Values);
        Assert.Equal("metallic", kept.Key);
        Assert.Equal(1.0, kept.Value);
    }

    [Fact]
    public void KeepsMapsBySlotAndRefusesASlotItHasNoUseFor()
    {
        var json = Map(("maps", Map(
            ("baseColour", "textures/brass.png"),
            ("shinyness", "textures/x.png"),
            ("normal", ""))));

        var kept = Assert.Single(Doc(json).Maps);
        Assert.Equal("baseColour", kept.Key);
        Assert.Equal("textures/brass.png", kept.Value);
        Assert.Equal(2, Problems(json).Count);
    }

    [Fact]
    public void WritesBackWhatItReadAndNothingItDidNot()
    {
        var json = Map(
            ("parent", "materials/metal.omat"),
            ("values", Map(("metallic", 1.0))),
            ("maps", Map(("normal", "textures/n.png"))));

        Same(json, Doc(json).ToJson());

        // A material that states nothing states nothing, rather than a file full of empty objects.
        Assert.Empty(Doc(Map()).ToJson());
    }

    // Resolving.
    [Fact]
    public void AChildTakesWhatItDoesNotStateFromItsParent()
    {
        var library = new MaterialLibrary(Docs(
            ("base.omat", Doc(Values(("metallic", 1L), ("roughness", 0.2)))),
            ("child.omat", Doc(With(Values(("roughness", 0.8)), ("parent", "base.omat"))))));

        var resolved = library.Resolve("child.omat");

        Assert.Equal(1.0, resolved.Number("metallic"));
        Assert.Equal(0.8, resolved.Number("roughness"));
        Assert.Empty(resolved.Problems);
    }

    [Fact]
    public void TheNearestAncestorWinsDownALongChain()
    {
        var library = new MaterialLibrary(Docs(
            ("a.omat", Doc(Values(("roughness", 0.1)))),
            ("b.omat", Doc(With(Values(("roughness", 0.2)), ("parent", "a.omat")))),
            ("c.omat", Doc(With(Values(("roughness", 0.3)), ("parent", "b.omat")))),
            ("d.omat", Doc(Map(("parent", "c.omat"))))));

        Assert.Equal(0.3, library.Resolve("d.omat").Number("roughness"));
    }

    [Fact]
    public void MapsAreInheritedAndReplacedWhole()
    {
        var library = new MaterialLibrary(Docs(
            ("base.omat", Doc(Map(("maps", Map(("baseColour", "a.png"), ("normal", "n.png")))))),
            ("child.omat", Doc(Map(("parent", "base.omat"), ("maps", Map(("baseColour", "b.png"))))))));

        var maps = library.Resolve("child.omat").Maps;

        Assert.Equal(2, maps.Count);
        Assert.Equal("b.png", maps["baseColour"]);
        Assert.Equal("n.png", maps["normal"]);
    }

    [Fact]
    public void AGroupOverridesWhatTheMaterialItselfStates()
    {
        var library = new MaterialLibrary(
            Docs(("crate.omat", Doc(With(Values(("roughness", 0.1), ("metallic", 1L)), ("group", "props"))))),
            Docs(("props", Doc(Values(("roughness", 0.9))))));

        var resolved = library.Resolve("crate.omat");

        Assert.Equal(0.9, resolved.Number("roughness"));
        Assert.Equal(1.0, resolved.Number("metallic"));
    }

    [Fact]
    public void AMaterialChoosesItsOwnGroupOverAnAncestors()
    {
        var library = new MaterialLibrary(
            Docs(
                ("base.omat", Doc(Map(("group", "far")))),
                ("child.omat", Doc(Map(("parent", "base.omat"), ("group", "near"))))),
            Docs(
                ("far", Doc(Values(("roughness", 0.1)))),
                ("near", Doc(Values(("roughness", 0.9))))));

        Assert.Equal(0.9, library.Resolve("child.omat").Number("roughness"));
    }

    [Fact]
    public void AMissingParentIsReportedAndWhatIsLeftStillResolves()
    {
        var library = new MaterialLibrary(Docs(
            ("child.omat", Doc(With(Values(("roughness", 0.4)), ("parent", "gone.omat"))))));

        var resolved = library.Resolve("child.omat");

        Assert.Equal(0.4, resolved.Number("roughness"));
        Assert.Contains("gone.omat", Assert.Single(resolved.Problems));
    }

    [Fact]
    public void AChainThatLoopsIsReportedRatherThanHungOn()
    {
        var library = new MaterialLibrary(Docs(
            ("a.omat", Doc(With(Values(("metallic", 1L)), ("parent", "b.omat")))),
            ("b.omat", Doc(Map(("parent", "a.omat"))))));

        var resolved = library.Resolve("a.omat");

        Assert.Contains("inherits from itself", Assert.Single(resolved.Problems));
        Assert.Equal(1.0, resolved.Number("metallic"));
    }

    [Fact]
    public void AMissingGroupIsReportedNotGuessedAt()
    {
        var library = new MaterialLibrary(Docs(("a.omat", Doc(Map(("group", "nowhere"))))));

        Assert.Contains("nowhere", Assert.Single(library.Resolve("a.omat").Problems));
    }

    [Fact]
    public void AMaterialNobodyPutThereResolvesToNothingWithAProblem()
    {
        var resolved = new MaterialLibrary().Resolve("missing.omat");

        Assert.Empty(resolved.Values);
        Assert.Contains("missing.omat", Assert.Single(resolved.Problems));
    }

    [Fact]
    public void PuttingAMaterialAgainIsSeenByEverythingBelowIt()
    {
        var library = new MaterialLibrary(Docs(
            ("base.omat", Doc(Values(("roughness", 0.1)))),
            ("child.omat", Doc(Map(("parent", "base.omat"))))));
        Assert.Equal(0.1, library.Resolve("child.omat").Number("roughness"));

        library.Put("base.omat", Doc(Values(("roughness", 0.9))));
        Assert.Equal(0.9, library.Resolve("child.omat").Number("roughness"));

        library.PutGroup("props", Doc(Values(("roughness", 0.5))));
        library.Put("child.omat", Doc(Map(("parent", "base.omat"), ("group", "props"))));
        Assert.Equal(0.5, library.Resolve("child.omat").Number("roughness"));
    }

    [Fact]
    public void ResolvingTwiceGivesTheSameAnswerWithoutWalkingAgain()
    {
        var library = new MaterialLibrary(Docs(("a.omat", Doc(Values(("roughness", 0.3))))));

        Assert.Same(library.Resolve("a.omat"), library.Resolve("a.omat"));
    }

    // The parameter table.
    [Fact]
    public void TheTableHasNoNameTwice()
    {
        Assert.Equal(MaterialFields.All.Count, MaterialFields.ByName.Count);
    }

    [Fact]
    public void TheTableGivesEveryChoiceAtLeastTwoToChooseBetween()
    {
        foreach (var field in MaterialFields.All)
        {
            if (field.Kind == MaterialKind.Choice)
            {
                Assert.True(field.Choices.Count > 1, field.Name);
            }
            else
            {
                Assert.Empty(field.Choices);
            }
        }
    }

    // Looks.
    [Fact]
    public void AnObjectWearsItsOwnMaterialWhenALookSaysNothing()
    {
        var worn = new MaterialComponent("a.omat");

        Assert.Equal("a.omat", worn.Under(null));
        Assert.Equal("a.omat", worn.Under("winter"));
    }

    [Fact]
    public void AndTheLooksWhenItHasOne()
    {
        var worn = new MaterialComponent("a.omat", Looks(("winter", "snow.omat")));

        Assert.Equal("snow.omat", worn.Under("winter"));
        Assert.Equal("a.omat", worn.Under("summer"));
        Assert.Equal(["winter"], worn.LookNames);
    }

    [Fact]
    public void LooksSurviveBeingWrittenOutAndReadBack()
    {
        var worn = new MaterialComponent("a.omat", Looks(("winter", "snow.omat")));

        var again = MaterialComponent.FromJson(worn.ToJson());

        Assert.Equal("a.omat", again.Asset);
        var look = Assert.Single(again.Looks);
        Assert.Equal(("winter", "snow.omat"), (look.Key, look.Value));
    }

    [Fact]
    public void AComponentWithNoLooksWritesNoLooks()
    {
        Same(Map(("asset", "a.omat")), new MaterialComponent("a.omat").ToJson());
    }

    // Values for a shader.
    [Fact]
    public void ShaderValuesSurviveBeingWrittenOutAndReadBack()
    {
        var worn = new MaterialComponent(
            "pulse.oshader",
            values: new OrderedDictionary<string, object>
            {
                ["speed"] = 2.5,
                ["glow"] = true,
                ["tint"] = List(1L, 0.5, 0.25, 1L),
                ["noise"] = "art/noise.png",
            });

        var again = MaterialComponent.FromJson(worn.ToJson());

        Same(worn.Values, again.Values);
    }

    [Fact]
    public void AShaderValueNoShaderCouldTakeIsDroppedOnReading()
    {
        var worn = MaterialComponent.FromJson(Map(
            ("asset", "pulse.oshader"),
            ("values", Map(
                ("speed", 2L),
                ("empty", List()),
                ("long", List(1L, 2L, 3L, 4L, 5L)),
                ("words", List("a")),
                ("blank", ""),
                ("nothing", null)))));

        var kept = Assert.Single(worn.Values);
        Assert.Equal(("speed", 2L), (kept.Key, kept.Value));
    }

    [Fact]
    public void AComponentWithNoShaderValuesWritesNone()
    {
        Same(Map(("asset", "a.omat")), new MaterialComponent("a.omat").ToJson());
    }

    private static OrderedDictionary<string, string> Looks(params (string Look, string Path)[] pairs)
    {
        var looks = new OrderedDictionary<string, string>();
        foreach (var (look, path) in pairs)
        {
            looks[look] = path;
        }

        return looks;
    }
}

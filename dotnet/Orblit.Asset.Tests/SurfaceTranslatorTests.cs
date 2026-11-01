namespace Orblit.Asset.Tests;

public class SurfaceTranslatorTests
{
    private const string Pulse = """
        shader_type spatial;
        render_mode blend_mix, cull_disabled, unshaded;

        group_uniforms look;
        uniform vec4 tint : source_color = vec4(1.0, 0.5, 0.2, 1.0);
        uniform float speed : hint_range(0.0, 10.0, 0.1) = 2.0;
        instance uniform float glow = 1.0;
        uniform sampler2D albedo_map : source_color, filter_nearest;

        void fragment() {
          float pulse = 0.5 + 0.5 * sin(TIME * speed);
          ALBEDO = tint.rgb * texture(albedo_map, UV).rgb;
          EMISSION = tint.rgb * pulse * glow;
          ALPHA = tint.a;
        }

        """;

    private static SurfaceTranslation Translate(string source, Func<string, string?>? read = null) =>
        SurfaceTranslator.Translate("pulse", "materials/pulse.oshader", source, read);

    private static SurfaceException Refused(string source, Func<string, string?>? read = null) =>
        Assert.Throws<SurfaceException>(() => Translate(source, read));

    private static string Head(string modes) => Translate($"render_mode {modes};\nvoid fragment() {{}}").Material;

    [Fact]
    public void KeepsTheAuthorsLinesAndDeclaresTheParameters()
    {
        var output = Translate(Pulse);
        var lines = output.Material.Split('\n');

        Assert.Contains("#define tint materialParams.tint", lines);
        Assert.Contains("#define albedo_map materialParams_albedo_map", lines);
        Assert.Contains("{ type : float4, name : \"tint\" }", output.Material);
        Assert.Contains("{ type : sampler2d, name : \"albedo_map\" }", output.Material);
        Assert.Contains("shadingModel : unlit", output.Material);
        Assert.Contains("blending : transparent", output.Material);
        Assert.Contains("doubleSided : true", output.Material);
        Assert.Contains("material.baseColor = vec4(ALBEDO * ALPHA", output.Material);
    }

    [Fact]
    public void QuotesNamesAndRemovesItsAliasesAfterTheAuthorsText()
    {
        var lines = Translate("uniform float n;\nvoid fragment() {}").Material.Split('\n').ToList();

        Assert.Contains("        { type : float, name : \"n\" }", lines);
        Assert.True(lines.IndexOf("#undef n") > lines.IndexOf("#define n materialParams.n"));
    }

    [Fact]
    public void AUniformNamedLikeAMaterialFieldDoesNotRewriteTheEntry()
    {
        var lines = Translate("uniform vec3 emissive;\nvoid fragment() { EMISSION = emissive; }").Material.Split('\n').ToList();

        Assert.True(lines.IndexOf("#undef emissive") < lines.FindIndex(l => l.Contains("material.emissive", StringComparison.Ordinal)));
    }

    [Fact]
    public void MapsAMaterialLineBackToTheLineTheAuthorWrote()
    {
        var output = Translate(Pulse);
        var lines = output.Material.Split('\n').ToList();
        var pulseAt = lines.FindIndex(l => l.Contains("float pulse", StringComparison.Ordinal)) + 1;
        var authorAt = Pulse.Split('\n').ToList().FindIndex(l => l.Contains("float pulse", StringComparison.Ordinal)) + 1;

        Assert.Equal($"materials/pulse.oshader:{authorAt}", output.OriginOf(pulseAt).ToString());
        Assert.Equal("materials/pulse.oshader", output.OriginOf(lines.Count).File);
    }

    [Fact]
    public void ReadsTypesHintsDefaultsGroupsAndInstance()
    {
        var parameters = Translate(Pulse).Params;

        Assert.Equal(["tint", "speed", "glow", "albedo_map"], parameters.Select(p => p.Name));
        Assert.True(parameters[0].IsColor);
        Assert.Equal<object?>([1.0, 0.5, 0.2, 1.0], (IReadOnlyList<object?>)parameters[0].DefaultValue!);
        Assert.Equal(["0.0", "10.0", "0.1"], parameters[1].Hints["hint_range"]);
        Assert.Equal(2.0, parameters[1].DefaultValue);
        Assert.True(parameters[2].Instance);
        Assert.Equal("look", parameters[2].Group);
        Assert.Equal(SurfaceType.Texture, parameters[3].Type);
        Assert.Contains("source_color", parameters[3].Hints.Keys);
        Assert.Contains("filter_nearest", parameters[3].Hints.Keys);
        Assert.Equal("vec4", parameters[0].ToJson()["type"]);
    }

    [Fact]
    public void BroadcastsOneNumberToAWholeVector()
    {
        var output = Translate("uniform vec3 c = vec3(0.5);\nvoid fragment() {}");

        Assert.Equal<object?>([0.5, 0.5, 0.5], (IReadOnlyList<object?>)output.Params.Single().DefaultValue!);
    }

    [Fact]
    public void WritesTheNormalOnlyWhenTheAuthorSetsIt()
    {
        Assert.DoesNotContain("material.normal", Translate(Pulse).Material, StringComparison.Ordinal);
        var lit = Translate("void fragment() { NORMAL_MAP = vec3(0.0, 0.0, 1.0); }");

        Assert.Contains("material.normal = NORMAL_MAP;", lit.Material);
        Assert.Contains("shadingModel : lit", lit.Material);
    }

    [Fact]
    public void MapsEveryRenderModeItHas()
    {
        Assert.Contains("blending : add", Head("blend_add"));
        Assert.Contains("blending : multiply", Head("blend_mul"));
        Assert.DoesNotContain("ALBEDO * ALPHA", Head("blend_premul_alpha"), StringComparison.Ordinal);
        Assert.Contains("culling : front", Head("cull_front"));
        Assert.Contains("depthWrite : false", Head("depth_draw_never"));
        Assert.Contains("depthCulling : false", Head("depth_test_disabled"));
    }

    [Fact]
    public void TheLastModeOfAFamilyWinsWhateverCameBefore()
    {
        Assert.DoesNotContain("doubleSided", Head("cull_disabled, cull_back"), StringComparison.Ordinal);
        Assert.Contains("culling : back", Head("cull_disabled, cull_back"));
        Assert.DoesNotContain("* ALPHA", Head("blend_add, blend_premul_alpha"), StringComparison.Ordinal);
        Assert.DoesNotContain("depthWrite", Head("depth_draw_never, depth_draw_opaque"), StringComparison.Ordinal);
    }

    [Fact]
    public void IncludesAreExpandedInPlaceWithTheirOwnLinesInTheMap()
    {
        const string include = "float twice(float x) {\n  return x * 2.0;\n}\n";
        var output = Translate(
            "#include \"lib/math.oshaderinc\"\nvoid fragment() { ROUGHNESS = twice(0.1); }",
            path => path == "materials/lib/math.oshaderinc" ? include : null);
        var at = output.Material.Split('\n').ToList().FindIndex(l => l.Contains("return x", StringComparison.Ordinal)) + 1;

        Assert.Equal("materials/lib/math.oshaderinc:2", output.OriginOf(at).ToString());
        Assert.Equal(["materials/lib/math.oshaderinc"], output.Includes);
    }

    [Fact]
    public void IncludesNameBothFilesInACycle()
    {
        var files = new Dictionary<string, string>
        {
            ["materials/a.oshaderinc"] = "#include \"b.oshaderinc\"",
            ["materials/b.oshaderinc"] = "#include \"a.oshaderinc\"",
        };
        var error = Refused("#include \"a.oshaderinc\"\nvoid fragment() {}", path => files.GetValueOrDefault(path));

        Assert.Contains("a.oshaderinc includes materials/b.oshaderinc", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void IncludesMustBeOshaderincFiles()
    {
        var error = Refused("#include \"a.glsl\"\nvoid fragment() {}");

        Assert.Equal(1, error.Origin.Line);
        Assert.Contains(".oshaderinc", error.Reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("void fragment() {}\nrender_mode wireframe;", 2, "wireframe")]
    [InlineData("shader_type particles;\nvoid fragment() {}", 1, "particles")]
    [InlineData("void fragment() {}\nvoid vertex() {}", 2, "vertex()")]
    [InlineData("varying vec3 pos;\nvoid fragment() {}", 1, "varying")]
    [InlineData("uniform vec3 c : hint_wibble;\nvoid fragment() {}", 1, "hint_wibble")]
    [InlineData("uniform float f = vec2(1.0);\nvoid fragment() {}", 1, "vec2(1.0)")]
    [InlineData("instance uniform sampler2D t;\nvoid fragment() {}", 1, "texture")]
    [InlineData("uniform mat4 m;\nvoid fragment() {}", 1, "mat4")]
    public void RefusesSayingWhichLineAndWhy(string source, int line, string reason)
    {
        var error = Refused(source);

        Assert.Equal(line, error.Origin.Line);
        Assert.Contains(reason, error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void WantsAFragmentFunction() =>
        Assert.Contains("fragment()", Refused("uniform float f;").Reason, StringComparison.Ordinal);
}

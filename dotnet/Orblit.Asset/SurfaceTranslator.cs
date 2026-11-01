using System.Text.RegularExpressions;
using Orblit.Json;
using Orblit.Mesh;
using static Orblit.Asset.CharClasses;

namespace Orblit.Asset;

/// <summary>
/// Turns an <c>.oshader</c> into a Filament material file. Each declaration becomes a <c>#define</c> on the line it
/// was written on, and the entry point is added after the author's text, so a line number matc reports in the
/// material file is the author's own plus a fixed offset. Only text from an <c>#include</c> shifts that, and
/// <see cref="SurfaceTranslation.Origins"/> says by how much.
/// </summary>
internal static partial class SurfaceTranslator
{
    private const string FragmentArguments =
        "inout vec3 ALBEDO, inout float ALPHA, inout float METALLIC, "
        + "inout float ROUGHNESS, inout float AO, inout vec3 EMISSION, "
        + "inout vec3 NORMAL_MAP, vec2 UV, float TIME";

    [GeneratedRegex($"(?<!{Word})void{Space}+fragment{Space}*\\({Space}*\\)")]
    private static partial Regex FragmentHead();

    [GeneratedRegex($"^{Space}*(instance{Space}+)?uniform(?!{Word})")]
    private static partial Regex UniformStart();

    [GeneratedRegex($"^{Space}*(instance{Space}+)?uniform{Space}+({Word}+){Space}+({Word}+){Space}*(?::([^=]*))?(?:=({Any}*))?;{Space}*\\z")]
    private static partial Regex UniformShape();

    [GeneratedRegex($"^{Space}*render_mode{Space}+([^;]*);{Space}*\\z")]
    private static partial Regex ModeLine();

    [GeneratedRegex($"^{Space}*shader_type{Space}+({Word}+){Space}*;{Space}*\\z")]
    private static partial Regex TypeLine();

    [GeneratedRegex($"^{Space}*group_uniforms{Space}*([A-Za-z0-9_.]*){Space}*;{Space}*\\z")]
    private static partial Regex GroupLine();

    [GeneratedRegex($"^{Space}*(?:varying(?!{Word})|void{Space}+(vertex|light){Space}*\\()")]
    private static partial Regex Unsupported();

    [GeneratedRegex(NotWord)]
    private static partial Regex NotWordChar();

    /// <summary>Reads one <c>.oshader</c>. <paramref name="read"/> gives the text of an included project path, or null.</summary>
    internal static SurfaceTranslation Translate(
        string name, string path, string source, Func<string, string?>? read = null)
    {
        var includes = new OrderedSet<string>();
        var lines = SurfaceLines.Expand(path, source, read ?? (_ => null), includes);
        var state = new Reading();
        var body = Rewrite(lines, state);
        var last = lines[^1].Origin;
        if (!state.HasFragment)
        {
            throw new SurfaceException(new SurfaceOrigin(path, 1), "there is no fragment() function");
        }

        var head = Head(name, state);
        var tail = Entry(state);
        return new SurfaceTranslation
        {
            Material = string.Join('\n', head.Concat(body.Select(line => line.Text)).Concat(tail)),
            Params = state.Params,
            Origins =
            [
                .. head.Select(_ => new SurfaceOrigin(path, 1)),
                .. body.Select(line => line.Origin),
                .. tail.Select(_ => last),
            ],
            Includes = includes,
        };
    }

    private static List<SurfaceLine> Rewrite(List<SurfaceLine> lines, Reading state)
    {
        var output = new List<SurfaceLine>();
        var depth = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (depth == 0 && Declaration(lines, i, state) is { } used)
            {
                output.Add(used.First);
                for (var k = 1; k < used.Consumed; k++)
                {
                    output.Add(lines[i + k] with { Text = string.Empty });
                }

                i += used.Consumed - 1;
                continue;
            }

            depth += Braces(line.Text);
            output.Add(line with { Text = AsFragment(line.Text, state) });
        }

        return output;
    }

    private static int Braces(string text) => text.Count(c => c == '{') - text.Count(c => c == '}');

    private static string AsFragment(string text, Reading state)
    {
        state.UsesNormal = state.UsesNormal || text.Contains("NORMAL_MAP", StringComparison.Ordinal);
        if (!FragmentHead().IsMatch(text))
        {
            return text;
        }

        state.HasFragment = true;
        return FragmentHead().Replace(text, _ => $"void orblit_fragment({FragmentArguments})", 1);
    }

    // A top level declaration read from lines[at] on, the text to put on its first line, and how many lines it used.
    // Null when the line is not one.
    private static (SurfaceLine First, int Consumed)? Declaration(List<SurfaceLine> lines, int at, Reading state)
    {
        var line = lines[at];
        var unsupported = Unsupported().Match(line.Text);
        if (unsupported.Success)
        {
            var word = unsupported.Groups[1].Success ? $"{unsupported.Groups[1].Value}()" : "varying";
            throw new SurfaceException(line.Origin, $"`{word}` is not supported yet");
        }

        if (UniformStart().IsMatch(line.Text))
        {
            return Uniform(lines, at, state);
        }

        return Directive(line.Text, line.Origin, state) ? (line with { Text = string.Empty }, 1) : null;
    }

    private static bool Directive(string text, SurfaceOrigin at, Reading state)
    {
        var type = TypeLine().Match(text);
        if (type.Success)
        {
            if (type.Groups[1].Value != "spatial")
            {
                throw new SurfaceException(at, $"shader_type {type.Groups[1].Value} is not supported");
            }

            return true;
        }

        var modes = ModeLine().Match(text);
        if (modes.Success)
        {
            foreach (var mode in modes.Groups[1].Value.Split(','))
            {
                state.Modes.Add(PlainText.Trim(mode), at);
            }

            return true;
        }

        var group = GroupLine().Match(text);
        if (group.Success)
        {
            state.Group = group.Groups[1].Value.Length == 0 ? null : group.Groups[1].Value;
        }

        return group.Success;
    }

    private static (SurfaceLine First, int Consumed) Uniform(List<SurfaceLine> lines, int at, Reading state)
    {
        var origin = lines[at].Origin;
        var end = at;
        var joined = lines[at].Text;
        while (!joined.Contains(';'))
        {
            end++;
            if (end >= lines.Count)
            {
                throw new SurfaceException(origin, "a uniform needs a `;`");
            }

            joined = $"{joined} {lines[end].Text}";
        }

        var match = UniformShape().Match(joined);
        if (!match.Success)
        {
            throw new SurfaceException(origin, "cannot read this uniform");
        }

        var param = Param(match, origin, state.Group);
        state.Params.Add(param);
        var alias = param.Type == SurfaceType.Texture
            ? $"materialParams_{param.Name}"
            : $"materialParams.{param.Name}";
        return (lines[at] with { Text = $"#define {param.Name} {alias}" }, end - at + 1);
    }

    private static SurfaceParam Param(Match match, SurfaceOrigin at, string? group)
    {
        var word = match.Groups[2].Value;
        var type = SurfaceTypeTools.OfWord(word) ?? throw new SurfaceException(at, $"unknown type `{word}`");
        var instance = match.Groups[1].Success;
        if (instance && type == SurfaceType.Texture)
        {
            throw new SurfaceException(at, "a texture cannot be an instance uniform");
        }

        // Hints are read before the default, so a line with both wrong is refused for its hint.
        var hints = match.Groups[4].Success
            ? SurfaceLiterals.ParseHints(match.Groups[4].Value, at)
            : new OrderedDictionary<string, IReadOnlyList<string>>();
        return new SurfaceParam
        {
            Name = match.Groups[3].Value,
            Type = type,
            Line = at.Line,
            Instance = instance,
            Group = group,
            Hints = hints,
            DefaultValue = match.Groups[5].Success ? SurfaceLiterals.ParseDefault(type, match.Groups[5].Value, at) : null,
        };
    }

    private static List<string> Head(string name, Reading state)
    {
        var modes = state.Modes;
        List<string> head =
        [
            "material {",
            $"    name : \"{NotWordChar().Replace(name, "_")}\",",
            $"    shadingModel : {(modes.Unshaded ? "unlit" : "lit")},",
            $"    blending : {modes.Blending},",
        ];
        if (modes.Culling is not null)
        {
            head.Add($"    culling : {modes.Culling},");
        }

        if (modes.DoubleSided)
        {
            head.Add("    doubleSided : true,");
        }

        if (modes.DepthWrite is { } write)
        {
            head.Add($"    depthWrite : {Flag(write)},");
        }

        if (modes.DepthCulling is { } culling)
        {
            head.Add($"    depthCulling : {Flag(culling)},");
        }

        head.Add("    requires : [ uv0 ],");
        head.AddRange(ParameterLines(state.Params));
        head.Add("}");
        head.Add("fragment {");
        return head;
    }

    private static string Flag(bool value) => value ? "true" : "false";

    private static List<string> ParameterLines(List<SurfaceParam> parameters)
    {
        if (parameters.Count == 0)
        {
            return [];
        }

        var lines = new List<string> { "    parameters : [" };
        for (var i = 0; i < parameters.Count; i++)
        {
            var comma = i + 1 < parameters.Count ? "," : string.Empty;
            lines.Add($"        {{ type : {parameters[i].Type.MatType()}, name : \"{parameters[i].Name}\" }}{comma}");
        }

        lines.Add("    ],");
        return lines;
    }

    private static List<string> Entry(Reading state)
    {
        var modes = state.Modes;
        var color = modes.Premultiply ? "vec4(ALBEDO * ALPHA, ALPHA)" : "vec4(ALBEDO, ALPHA)";
        // Before the entry point, which writes `material.baseColor` and `material.emissive`: an alias of the same
        // name would rewrite those into fields that do not exist. Filament's own code follows the block and has a
        // `c` and an `n` of its own, so an alias must not outlive the author's text either.
        var lines = state.Params.Select(p => $"#undef {p.Name}").ToList();
        lines.AddRange(
        [
            "    void material(inout MaterialInputs material) {",
            "        vec3 ALBEDO = vec3(1.0);",
            "        float ALPHA = 1.0;",
            "        float METALLIC = 0.0;",
            "        float ROUGHNESS = 0.5;",
            "        float AO = 1.0;",
            "        vec3 EMISSION = vec3(0.0);",
            "        vec3 NORMAL_MAP = vec3(0.0, 0.0, 1.0);",
            "        orblit_fragment(ALBEDO, ALPHA, METALLIC, ROUGHNESS, AO, EMISSION, NORMAL_MAP, getUV0(), getUserTime().x);",
        ]);
        if (state.UsesNormal && !modes.Unshaded)
        {
            lines.Add("        material.normal = NORMAL_MAP;");
        }

        lines.Add("        prepareMaterial(material);");
        lines.Add($"        material.baseColor = {color};");
        if (!modes.Unshaded)
        {
            lines.Add("        material.metallic = METALLIC;");
            lines.Add("        material.roughness = ROUGHNESS;");
            lines.Add("        material.ambientOcclusion = AO;");
        }

        lines.Add("        material.emissive = vec4(EMISSION, 1.0);");
        lines.Add("    }");
        lines.Add("}");
        return lines;
    }

    // What the walk over the author's lines has found so far.
    private sealed class Reading
    {
        public SurfaceModes Modes { get; } = new();

        public List<SurfaceParam> Params { get; } = [];

        public string? Group { get; set; }

        public bool HasFragment { get; set; }

        public bool UsesNormal { get; set; }
    }
}

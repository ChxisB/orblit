using System.Text.Json;
using Orblit.Parity;
using static Orblit.Parity.Dump;

namespace Orblit.Codegen.Tests;

/// <summary>
/// Runs the cases the codegen fixture exporter wrote and compares the manifest the port writes with
/// the one in the fixture for the same components, byte for byte.
/// </summary>
public class CodegenParityTests
{
    // Text arrives as UTF-16 code units, so a lone surrogate survives the fixture.
    private static string Units(JsonElement units) =>
        new([.. units.EnumerateArray().Select(u => (char)u.GetInt32())]);

    private static ComponentDeclaration Declaration(JsonElement c) => new(
        name: Units(c.GetProperty("name")),
        typeName: Units(c.GetProperty("class")),
        kind: Units(c.GetProperty("kind")),
        fields: [.. c.GetProperty("fields").EnumerateArray().Select(Units)],
        replicated: c.GetProperty("replicated").GetBoolean(),
        ownerWritable: c.GetProperty("ownerWritable").GetBoolean(),
        source: Units(c.GetProperty("source")));

    [Fact]
    public void ManifestsAreTheSameBytes() => Fixture.Load("orblit_codegen", "codegen").Check("manifest", s => Attempt(() =>
    {
        var found = s.GetProperty("components").EnumerateArray().Select(Declaration);
        return Text(ComponentEmitter.Manifest(Units(s.GetProperty("package")), new ScanResult(found, []).Components));
    }));
}

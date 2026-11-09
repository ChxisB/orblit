using System.Threading.Tasks;
using Xunit;

namespace Orblit.SourceGenerators.Tests;

public class ExportDiagnosticsTests
{
    [Fact]
    public async Task StaticMembers()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertyDefValGenerator>.Verify(
            "ExportDiagnostics_ORB0101.cs",
            "ExportDiagnostics_ORB0101_ScriptPropertyDefVal.generated.cs"
        );
    }

    [Fact]
    public async Task TypeIsNotSupported()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertyDefValGenerator>.Verify(
            "ExportDiagnostics_ORB0102.cs",
            "ExportDiagnostics_ORB0102_ScriptPropertyDefVal.generated.cs"
        );
    }

    [Fact]
    public async Task ReadOnly()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertyDefValGenerator>.Verify(
            "ExportDiagnostics_ORB0103.cs",
            "ExportDiagnostics_ORB0103_ScriptPropertyDefVal.generated.cs"
        );
    }

    [Fact]
    public async Task WriteOnly()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertyDefValGenerator>.Verify(
            "ExportDiagnostics_ORB0104.cs",
            "ExportDiagnostics_ORB0104_ScriptPropertyDefVal.generated.cs"
        );
    }

    [Fact]
    public async Task Indexer()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertyDefValGenerator>.Verify(
            "ExportDiagnostics_ORB0105.cs",
            "ExportDiagnostics_ORB0105_ScriptPropertyDefVal.generated.cs"
        );
    }

    [Fact]
    public async Task ExplicitInterfaceImplementation()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertyDefValGenerator>.Verify(
            new string[] { "ExportDiagnostics_ORB0106.cs" },
            new string[]
            {
                "ExportDiagnostics_ORB0106_OK_ScriptPropertyDefVal.generated.cs",
                "ExportDiagnostics_ORB0106_KO_ScriptPropertyDefVal.generated.cs",
            }
        );
    }

    [Fact]
    public async Task NodeExports()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertyDefValGenerator>.Verify(
            new string[] { "ExportDiagnostics_ORB0107.cs" },
            new string[]
            {
                "ExportDiagnostics_ORB0107_OK_ScriptPropertyDefVal.generated.cs",
                "ExportDiagnostics_ORB0107_KO_ScriptPropertyDefVal.generated.cs",
            }
        );
    }

    [Fact]
    public async Task ExportToolButtonInNonToolClass()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertiesGenerator>.Verify(
            new string[] { "ExportDiagnostics_ORB0108.cs" },
            new string[] { "ExportDiagnostics_ORB0108_ScriptProperties.generated.cs" }
        );
    }

    [Fact]
    public async Task ExportAndExportToolButtonOnSameMember()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertiesGenerator>.Verify(
            new string[] { "ExportDiagnostics_ORB0109.cs" },
            new string[] { "ExportDiagnostics_ORB0109_ScriptProperties.generated.cs" }
        );
    }

    [Fact]
    public async Task ExportToolButtonOnNonCallable()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertiesGenerator>.Verify(
            new string[] { "ExportDiagnostics_ORB0110.cs" },
            new string[] { "ExportDiagnostics_ORB0110_ScriptProperties.generated.cs" }
        );
    }

    [Fact]
    public async Task ExportToolButtonStoringCallable()
    {
        await CSharpSourceGeneratorVerifier<ScriptPropertiesGenerator>.Verify(
            new string[] { "ExportDiagnostics_ORB0111.cs" },
            new string[] { "ExportDiagnostics_ORB0111_ScriptProperties.generated.cs" }
        );
    }
}

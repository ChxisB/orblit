using System.Threading.Tasks;
using Xunit;

namespace Orblit.SourceGenerators.Tests;

public class ScriptSignalsGeneratorTests
{
    [Fact]
    public async Task EventSignals()
    {
        await CSharpSourceGeneratorVerifier<ScriptSignalsGenerator>.Verify(
            "EventSignals.cs",
            "EventSignals_ScriptSignals.generated.cs"
        );
    }

    [Fact]
    public async Task EventSignalsInGenericClass()
    {
        await CSharpSourceGeneratorVerifier<ScriptSignalsGenerator>.Verify(
            "GenericSignals.cs",
            "GenericSignals(Of T)_ScriptSignals.generated.cs"
        );
    }
}

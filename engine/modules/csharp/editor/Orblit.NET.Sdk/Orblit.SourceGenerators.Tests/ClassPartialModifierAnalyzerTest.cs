using System.Threading.Tasks;
using Xunit;

namespace Orblit.SourceGenerators.Tests;

public class ClassPartialModifierTest
{
    [Fact]
    public async Task ClassPartialModifierCodeFixTest()
    {
        await CSharpCodeFixVerifier<ClassPartialModifierCodeFixProvider, ClassPartialModifierAnalyzer>
            .Verify("ClassPartialModifier.ORB0001.cs", "ClassPartialModifier.ORB0001.fixed.cs");
    }

    [Fact]
    public async Task OuterClassPartialModifierAnalyzerTest()
    {
        await CSharpAnalyzerVerifier<ClassPartialModifierAnalyzer>.Verify("OuterClassPartialModifierAnalyzer.ORB0002.cs");
    }
}

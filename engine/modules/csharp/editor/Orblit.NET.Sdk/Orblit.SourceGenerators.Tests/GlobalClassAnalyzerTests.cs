using System.Threading.Tasks;
using Xunit;

namespace Orblit.SourceGenerators.Tests;

public class GlobalClassAnalyzerTests
{
    [Fact]
    public async Task GlobalClassMustDeriveFromOrblitObjectTest()
    {
        const string GlobalClassORB0401 = "GlobalClass.ORB0401.cs";
        await CSharpAnalyzerVerifier<GlobalClassAnalyzer>.Verify(GlobalClassORB0401);
    }

    [Fact]
    public async Task GlobalClassMustNotBeGenericTest()
    {
        const string GlobalClassORB0402 = "GlobalClass.ORB0402.cs";
        await CSharpAnalyzerVerifier<GlobalClassAnalyzer>.Verify(GlobalClassORB0402);
    }
}

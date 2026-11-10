using System.Threading.Tasks;
using Xunit;

namespace Orblit.SourceGenerators.Tests;

public class MustBeVariantAnalyzerTests
{
    [Fact]
    public async Task GenericTypeArgumentMustBeVariantTest()
    {
        const string MustBeVariantORB0301 = "MustBeVariant.ORB0301.cs";
        await CSharpAnalyzerVerifier<MustBeVariantAnalyzer>.Verify(MustBeVariantORB0301);
    }

    [Fact]
    public async Task GenericTypeParameterMustBeVariantAnnotatedTest()
    {
        const string MustBeVariantORB0302 = "MustBeVariant.ORB0302.cs";
        await CSharpAnalyzerVerifier<MustBeVariantAnalyzer>.Verify(MustBeVariantORB0302);
    }
}

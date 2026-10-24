using System.Reflection;
using System.Runtime.InteropServices;
using Orblit.Interop;

namespace Orblit.Filament.Tests;

/// <summary>
/// The C# declarations against the header that is their contract, and against
/// the built library that answers to it.
/// </summary>
public sealed class NativeContractTests
{
    private static IEnumerable<MethodInfo> Declarations() =>
        typeof(NativeMethods)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.GetCustomAttribute<LibraryImportAttribute>() is not null);

    private static string EntryPoint(MethodInfo method) => method.GetCustomAttribute<LibraryImportAttribute>()!.EntryPoint!;

    // A pointer in C is a pointer, a span, an array, a string or a reference here. A size_t is a nuint.
    private static bool IsPointer(Type type) =>
        type.IsByRef || type.IsArray || type == typeof(string) || type == typeof(nint)
        || (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Span<>) || type.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>)));

    private static string CamelCase(string snake) =>
        string.Concat(snake.Split('_').Select((word, index) => index == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));

    [Fact]
    public void EveryFunctionOfTheHeaderIsDeclared()
    {
        var header = RendererHeader.Functions().Select(function => function.Entry).Order(StringComparer.Ordinal);
        var declared = Declarations().Select(EntryPoint).Order(StringComparer.Ordinal);

        Assert.Equal(49, header.Count());
        Assert.Equal(header, declared);
    }

    [Fact]
    public void EveryDeclarationTakesTheParametersTheHeaderGives()
    {
        var declarations = Declarations().ToDictionary(EntryPoint, StringComparer.Ordinal);

        foreach (var function in RendererHeader.Functions())
        {
            var method = declarations[function.Entry];
            var parameters = method.GetParameters();

            Assert.True(
                function.Parameters.Select(parameter => CamelCase(parameter.Name)).SequenceEqual(parameters.Select(parameter => parameter.Name!)),
                $"{function.Entry} takes {string.Join(", ", function.Parameters.Select(parameter => parameter.Name))}.");
            Assert.Equal(function.Parameters.Select(parameter => parameter.IsPointer), parameters.Select(parameter => IsPointer(parameter.ParameterType)));
            Assert.Equal(function.ReturnsNothing, method.ReturnType == typeof(void));
        }
    }

    [Theory]
    [InlineData("OrblitBackend", typeof(Backend), 0)]
    [InlineData("orblit_result", typeof(RendererResult), 0)]
    [InlineData("orblit_surface_kind", typeof(SurfaceKind), 0)]
    [InlineData("orblit_stride", typeof(NativeMethods.Stride), 0)]
    [InlineData("orblit_capability", typeof(Capability), 1)]
    [InlineData("orblit_format_family", typeof(FormatFamily), 0)]
    public void EveryEnumMemberHasTheValueTheHeaderGives(string cName, Type csType, int membersLeftOut)
    {
        var header = RendererHeader.Enum(cName);
        var members = Enum.GetNames(csType);

        Assert.Equal(header.Count - membersLeftOut, members.Length);
        foreach (var name in members)
        {
            var value = Convert.ToInt64(Enum.Parse(csType, name), System.Globalization.CultureInfo.InvariantCulture);
            var suffix = name.ToLowerInvariant();
            var matches = header.Where(member => member.Name.Replace("_", string.Empty).ToLowerInvariant().EndsWith(suffix, StringComparison.Ordinal)).ToList();

            Assert.Equal(value, Assert.Single(matches).Value);
        }
    }

    [Fact]
    public void AStructHasTheSizeItsCCounterpartHas()
    {
        // A surface is a kind, then a pointer to the window. Stats are two doubles and three whole numbers, padded to a double.
        Assert.Equal(2 * nint.Size, Marshal.SizeOf<Surface>());
        Assert.Equal(32, Marshal.SizeOf<Stats>());
    }

    [RendererFact]
    public void EveryDeclaredFunctionIsExportedByTheLibrary()
    {
        var path = NativeLoader.SearchPaths(NativeMethods.Library, AppContext.BaseDirectory).First(File.Exists);
        var library = NativeLibrary.Load(path);
        try
        {
            Assert.All(Declarations().Select(EntryPoint), entry => Assert.True(NativeLibrary.TryGetExport(library, entry, out _), $"{entry} is not exported."));
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    [RendererFact]
    public void EveryRowHasAWidthAndAnUnknownOneHasNone()
    {
        var strides = Enum.GetValues<NativeMethods.Stride>();

        Assert.Equal(30, strides.Length);
        Assert.All(strides, which => Assert.True(NativeMethods.StrideOf(which) > 0, $"{which} has no width."));
        Assert.Equal(0u, NativeMethods.StrideOf((NativeMethods.Stride)strides.Length));
        Assert.Equal(0u, NativeMethods.StrideOf((NativeMethods.Stride)999));
    }

    [RendererFact]
    public void ADeviceAnswersEveryQuestionItIsAsked()
    {
        using var renderer = new Renderer(64, 48);
        var allFamilies = Enum.GetValues<FormatFamily>().Aggregate(0, (all, family) => all | (int)family);

        Assert.Equal((int)renderer.Backend, renderer.GetCapability(Capability.Backend));
        Assert.InRange(renderer.GetCapability(Capability.FeatureLevel), 0, 3);
        Assert.True(renderer.GetCapability(Capability.MaxTextureSize) > 0);
        Assert.True(renderer.GetCapability(Capability.MaxArrayTextureLayers) > 0);
        Assert.Equal(0, renderer.GetCapability(Capability.CompressedFormats) & ~allFamilies);
        Assert.InRange(renderer.GetCapability(Capability.HalfFloatTextures), 0, 1);
        Assert.True(renderer.GetCapability(Capability.WorkerThreads) >= 1);
        Assert.True(renderer.GetCapability(Capability.SystemMemoryMegabytes) >= 0);
    }

    [RendererFact]
    public void AQuestionTheRendererDoesNotKnowIsMinusOne()
    {
        using var renderer = new Renderer(64, 48);

        Assert.Equal(-1, renderer.GetCapability((Capability)8));
        Assert.Equal(-1, renderer.GetCapability((Capability)999));
    }
}

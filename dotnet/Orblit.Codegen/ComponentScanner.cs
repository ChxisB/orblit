using Microsoft.CodeAnalysis;

namespace Orblit.Codegen;

/// <summary>
/// Reads one attributed type. Roslyn has already resolved the types, so this reads symbols where the Dart
/// scanner parses source: what a declaration says is the same, and no name has to be guessed from text.
/// </summary>
public static class ComponentScanner
{
    public const string AttributeName = "Orblit.OrblitComponentAttribute";

    /// <summary>The manifest records a path from the project folder, with forward slashes on every system.</summary>
    public static string Relative(string projectDir, string path)
    {
        if (projectDir.Length == 0 || !path.StartsWith(projectDir, StringComparison.Ordinal))
        {
            return path.Replace('\\', '/');
        }

        return path.Substring(projectDir.Length).Replace('\\', '/').TrimStart('/');
    }

    public static (ComponentDeclaration? Component, ComponentError? Error) ScanType(
        INamedTypeSymbol type,
        AttributeData annotation,
        string source)
    {
        var fields = type.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic && !f.IsImplicitlyDeclared).ToList();
        if (Mismatch(fields) is { } mismatch)
        {
            return Refuse(source, type.Name, mismatch);
        }

        if (fields.Count == 0)
        {
            return Refuse(source, type.Name, "has no instance fields");
        }

        var kind = ExplicitKind(annotation) ?? InferKind(fields[0].Type);
        if (kind is null)
        {
            return Refuse(source, type.Name, $"fields are {fields[0].Type.ToDisplayString()}, which has no default storage. Give the annotation an explicit kind");
        }

        return (new ComponentDeclaration(
            name: Argument(annotation, "Name").Value as string ?? type.Name,
            typeName: type.Name,
            kind: kind,
            fields: [.. fields.Select(f => f.Name)],
            replicated: Argument(annotation, "Replicated").Value is true,
            ownerWritable: Argument(annotation, "OwnerWritable").Value is true,
            source: source), null);
    }

    private static (ComponentDeclaration?, ComponentError?) Refuse(string source, string typeName, string message) =>
        (null, new ComponentError(source, typeName, message));

    private static string? Mismatch(List<IFieldSymbol> fields)
    {
        ITypeSymbol? first = null;
        foreach (var field in fields)
        {
            if (field.Type.TypeKind == TypeKind.Error)
            {
                return $"field \"{field.Name}\" has no resolved type; the generator reads declarations rather than guessing";
            }

            first ??= field.Type;
            if (!SymbolEqualityComparer.Default.Equals(field.Type, first))
            {
                return $"fields are {first.ToDisplayString()} and {field.Type.ToDisplayString()}; a component is one column, so every field must share a type. Split it into two components instead";
            }
        }

        return null;
    }

    // C# says how wide each numeric type is, so double is float64 here where Dart's double is float32.
    private static string? InferKind(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Single => "float32",
        SpecialType.System_Double => "float64",
        SpecialType.System_Int32 => "int32",
        SpecialType.System_UInt32 => "uint32",
        SpecialType.System_Int64 => "int64",
        SpecialType.System_Byte or SpecialType.System_Boolean => "uint8",
        _ => null,
    };

    private static TypedConstant Argument(AttributeData annotation, string name) =>
        annotation.NamedArguments.FirstOrDefault(pair => pair.Key == name).Value;

    /// <summary>Reads <c>Kind = ComponentKind.Float64</c> as <c>float64</c>. A cast to no member reads as none.</summary>
    private static string? ExplicitKind(AttributeData annotation)
    {
        var kind = Argument(annotation, "Kind");
        return kind.Kind == TypedConstantKind.Enum
            ? kind.Type?.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => Equals(f.ConstantValue, kind.Value))?.Name.ToLowerInvariant()
            : null;
    }
}

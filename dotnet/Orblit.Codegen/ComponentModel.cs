namespace Orblit.Codegen;

/// <summary>What the generator learned about one component type.</summary>
public sealed class ComponentDeclaration : IEquatable<ComponentDeclaration>
{
    // F2: seven fields, always passed by name, as in the Dart constructor.
    public ComponentDeclaration(
        string name,
        string typeName,
        string kind,
        IReadOnlyList<string> fields,
        bool replicated,
        bool ownerWritable,
        string source)
    {
        Name = name;
        TypeName = typeName;
        Kind = kind;
        Fields = [.. fields];
        Replicated = replicated;
        OwnerWritable = ownerWritable;
        Source = source;
    }

    /// <summary>The name that crosses the wire and keys the manifest.</summary>
    public string Name { get; }

    /// <summary>The class or struct it was declared on.</summary>
    public string TypeName { get; }

    /// <summary>Storage kind, as an Orblit component kind name in lower case.</summary>
    public string Kind { get; }

    /// <summary>Field names, in declaration order, which is also their order in memory.</summary>
    public IReadOnlyList<string> Fields { get; }

    public bool Replicated { get; }

    public bool OwnerWritable { get; }

    /// <summary>Where it was declared, relative to the project folder.</summary>
    public string Source { get; }

    public int Arity => Fields.Count;

    public ComponentDeclaration At(string source) => new(Name, TypeName, Kind, Fields, Replicated, OwnerWritable, source);

    public bool Equals(ComponentDeclaration? other) =>
        other is not null
        && (Name, TypeName, Kind, Replicated, OwnerWritable, Source) == (other.Name, other.TypeName, other.Kind, other.Replicated, other.OwnerWritable, other.Source)
        && Fields.SequenceEqual(other.Fields);

    public override bool Equals(object? obj) => Equals(obj as ComponentDeclaration);

    public override int GetHashCode() => (Name, TypeName, Kind, Source).GetHashCode();
}

/// <summary>A problem with a declaration, reported with enough context to fix it.</summary>
public sealed class ComponentError(string source, string typeName, string message) : IEquatable<ComponentError>
{
    public string Source { get; } = source;

    public string TypeName { get; } = typeName;

    public string Message { get; } = message;

    public bool Equals(ComponentError? other) =>
        other is not null && (Source, TypeName, Message) == (other.Source, other.TypeName, other.Message);

    public override bool Equals(object? obj) => Equals(obj as ComponentError);

    public override int GetHashCode() => (Source, TypeName, Message).GetHashCode();

    public override string ToString() => $"{Source}: {TypeName}: {Message}";
}

/// <summary>
/// Everything found in one project. Ordered by name on the way in, so a manifest is reproducible and two
/// scans of the same sources compare equal.
/// </summary>
public sealed class ScanResult
{
    public ScanResult(IEnumerable<ComponentDeclaration> found, IReadOnlyList<ComponentError> errors)
    {
        // OrderBy is stable. Dart's List.sort is stable only up to 32 items (decision 75).
        Components = [.. found.OrderBy(c => c.Name, StringComparer.Ordinal)];
        Errors = errors;
    }

    public IReadOnlyList<ComponentDeclaration> Components { get; }

    public IReadOnlyList<ComponentError> Errors { get; }

    public bool HasErrors => Errors.Count > 0;
}

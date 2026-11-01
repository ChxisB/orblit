namespace Orblit.Motion;

/// <summary>A file that is not a blend, or is one from a newer Orblit.</summary>
public sealed class BlendFormatException(string message) : Exception(message);

/// <summary>A blend read back off disk, with anything that could not be read.</summary>
public sealed class BlendLoad(BlendDocument blend, IReadOnlyList<string>? problems = null)
{
    public BlendDocument Blend { get; } = blend;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];
}

/// <summary>One step between blend formats: decoded JSON at <see cref="From"/> in, at <see cref="To"/> out.</summary>
public abstract class BlendMigration : IFormatStep
{
    // Internal so the steps in BlendDocument.Migrations are the only ones there are.
    internal BlendMigration()
    {
    }

    /// <summary>The version this reads.</summary>
    public abstract int From { get; }

    /// <summary>The version it writes, always <see cref="From"/> plus one.</summary>
    public int To => From + 1;

    public abstract JsonMap Apply(JsonMap json, List<string> notes);
}

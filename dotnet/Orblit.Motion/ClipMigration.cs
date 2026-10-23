namespace Orblit.Motion;

/// <summary>One step between clip formats: decoded JSON at <see cref="From"/> in, at <see cref="To"/> out.</summary>
public abstract class ClipMigration : IFormatStep
{
    // Internal so the steps in ClipDocument.Migrations are the only ones there are.
    internal ClipMigration()
    {
    }

    /// <summary>The version this reads.</summary>
    public abstract int From { get; }

    /// <summary>The version it writes, always <see cref="From"/> plus one.</summary>
    public int To => From + 1;

    public abstract JsonMap Apply(JsonMap json, List<string> notes);
}

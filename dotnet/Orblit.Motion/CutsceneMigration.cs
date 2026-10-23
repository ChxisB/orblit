namespace Orblit.Motion;

/// <summary>One step between cutscene formats: decoded JSON at <see cref="From"/> in, at <see cref="To"/> out.</summary>
public abstract class CutsceneMigration : IFormatStep
{
    // Internal so the steps in CutsceneDocument.Migrations are the only ones there are.
    internal CutsceneMigration()
    {
    }

    /// <summary>The version this reads.</summary>
    public abstract int From { get; }

    /// <summary>The version it writes, always <see cref="From"/> plus one.</summary>
    public int To => From + 1;

    public abstract JsonMap Apply(JsonMap json, List<string> notes);
}

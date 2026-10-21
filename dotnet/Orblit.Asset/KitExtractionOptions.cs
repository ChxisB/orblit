namespace Orblit.Asset;

/// <summary>How <see cref="KitExtraction.ExtractAsync"/> writes a kit into a project.</summary>
public sealed record KitExtractionOptions
{
    /// <summary>Replace a file the project has with other bytes, where the default stops with a conflict.</summary>
    public bool Overwrite { get; init; }

    /// <summary>The folder of the project the files go under. It has to pass <see cref="KitRules.PlaceProblem"/>.</summary>
    public string Into { get; init; } = "";

    /// <summary>Write just these files. A file left out cannot conflict.</summary>
    public IReadOnlySet<AssetId>? Only { get; init; }
}

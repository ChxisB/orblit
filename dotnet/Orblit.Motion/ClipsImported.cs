namespace Orblit.Motion;

/// <summary>The clips a glTF file carried, and what of it could not be carried.</summary>
/// <param name="Clips">One for each of the file's animations, in the file's order.</param>
/// <param name="Problems">What was left out, and why, in words for a person.</param>
public sealed record ClipsImported(IReadOnlyList<ClipDocument> Clips, IReadOnlyList<string> Problems);

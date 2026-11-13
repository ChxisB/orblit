namespace Orblit.Editor.Bridge;

static class ResourcePath
{
    const string Prefix = "res://";

    /// <summary>A file of one kind under res://. The kind reads in the message, as in "a scene file".</summary>
    internal static void RequireFile(string resourcePath, string extension, string kind)
    {
        var inside = resourcePath.StartsWith(Prefix, StringComparison.Ordinal) && resourcePath.EndsWith(extension, StringComparison.Ordinal)
            && resourcePath.Length > Prefix.Length + extension.Length;
        if (!inside || resourcePath.Contains("..") || resourcePath.Contains('\\'))
        {
            throw new ArgumentException($"{resourcePath} is not {kind} under res://.", nameof(resourcePath));
        }
    }
}

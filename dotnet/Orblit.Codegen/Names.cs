namespace Orblit.Codegen;

/// <summary>Turns an assembly name into names the generated code can use.</summary>
public static class Names
{
    /// <summary>The assembly name, with anything a namespace cannot hold swapped for an underscore.</summary>
    public static string Namespace(string assembly) =>
        new([.. assembly.Select(c => char.IsLetterOrDigit(c) || c is '.' or '_' ? c : '_')]);

    /// <summary>
    /// <c>My.Game-Core</c> becomes <c>MyGameCoreComponents</c>, and <c>my_game</c>
    /// becomes <c>MyGameComponents</c>.
    /// </summary>
    public static string Registry(string assembly)
    {
        var parts = assembly.Split([.. assembly.Where(c => !char.IsLetterOrDigit(c)).Distinct()], StringSplitOptions.RemoveEmptyEntries);
        var name = string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        return (name.Length > 0 && char.IsDigit(name[0]) ? "_" : "") + name + "Components";
    }
}

namespace Orblit.Filament;

/// <summary>What a call of the C API came to. Nought is success and the rest are negative.</summary>
public enum RendererResult
{
    Ok = 0,

    /// <summary>A handle, or an array with a count above nought, was NULL.</summary>
    Null = -1,

    /// <summary>An array is shorter than its count and its row say it must be.</summary>
    Length = -2,

    /// <summary>The renderer has stopped: Filament refused something, or it was disposed.</summary>
    Failed = -3,

    /// <summary>An index names something past the end of its array.</summary>
    Range = -4,
}

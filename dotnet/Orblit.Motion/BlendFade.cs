namespace Orblit.Motion;

/// <summary>A fade duration for an ordered pair of dominant clips.</summary>
public sealed class BlendFade
{
    public BlendFade(string from, string to, double seconds)
    {
        if (from.Length == 0 || to.Length == 0 || !double.IsFinite(seconds) || seconds < 0)
        {
            throw new ArgumentException("Two clip names and a finite fade at least zero.");
        }

        From = from;
        To = to;
        Seconds = seconds;
    }

    public string From { get; }

    public string To { get; }

    public double Seconds { get; }

    public OrderedDictionary<string, object?> ToJson() => new() { ["from"] = From, ["to"] = To, ["seconds"] = Seconds };
}

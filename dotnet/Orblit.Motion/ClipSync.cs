namespace Orblit.Motion;

/// <summary>
/// Lines clips up by the marks they share, so two walks of different lengths put their feet down together.
/// </summary>
public static class ClipSync
{
    /// <summary>
    /// Maps a common cycle to evenly spaced named contacts in a clip.
    /// </summary>
    /// <remarks>
    /// The first marker is phase zero. Missing or repeated markers, or markers in a different cyclic order,
    /// leave the clip on its ordinary lap clock.
    /// </remarks>
    public static double SyncedLap(ClipDocument clip, double lap, IReadOnlyList<string> names)
    {
        if (names.Count < 2 || clip.Duration <= 0 || Anchors(clip, names) is not { } anchors)
        {
            return lap;
        }

        var whole = Math.Floor(lap);
        var along = (lap - whole) * names.Count;
        var segment = (int)Math.Clamp(Doubles.Floor(along), 0, names.Count - 1);
        return whole + anchors[segment] + ((anchors[segment + 1] - anchors[segment]) * (along - segment));
    }

    /// <summary>
    /// The phase of each name, one lap long in all with the first repeated at the end, or null when the
    /// clip's marks do not say a single cycle.
    /// </summary>
    private static List<double>? Anchors(ClipDocument clip, IReadOnlyList<string> names)
    {
        var anchors = new List<double>();
        foreach (var name in names)
        {
            if (PhaseOf(clip, name) is not { } phase)
            {
                return null;
            }

            var at = phase;
            if (anchors.Count > 0 && at <= anchors[^1])
            {
                at += 1;
            }

            if (anchors.Count > 0 && at <= anchors[^1])
            {
                return null;
            }

            anchors.Add(at);
        }

        if (anchors[^1] >= anchors[0] + 1)
        {
            return null;
        }

        anchors.Add(anchors[0] + 1);
        return anchors;
    }

    /// <summary>How far through the clip the one mark called <paramref name="name"/> is, or null.</summary>
    private static double? PhaseOf(ClipDocument clip, string name)
    {
        var found = clip.Marks.Where(mark => mark.Name == name).ToList();
        if (found.Count != 1)
        {
            return null;
        }

        var phase = found[0].At / clip.Duration;
        return phase < 0 || phase >= 1 ? null : phase;
    }
}

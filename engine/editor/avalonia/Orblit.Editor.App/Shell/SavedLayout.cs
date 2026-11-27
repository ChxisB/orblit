using System.Globalization;
using Avalonia;

namespace Orblit.Editor.App.Shell;

/// <summary>Where a floating window sits, in screen pixels, and its size.</summary>
internal readonly record struct FloatSpot(PixelPoint At, Size Size);

/// <summary>A group of panels: the place it sits in or comes back to, its tabs in order, and its window if it floats.</summary>
internal sealed record SavedGroup(DockSlot Slot, IReadOnlyList<string> Titles, FloatSpot? Window = null);

/// <summary>
/// The layout of the panels as the settings file keeps it. Reading drops what it does not know and places what it
/// does not find, so every panel ends up exactly once.
/// </summary>
internal sealed record SavedLayout
{
    // The key of each place's docked group, by place.
    static readonly string[] s_places = ["upper_left", "lower_left", "upper_right", "lower_right"];

    public required IReadOnlyList<SavedGroup> Groups { get; init; }

    /// <summary>The chosen tab of each group.</summary>
    public IReadOnlyList<string> Chosen { get; init; } = [];

    public IReadOnlyList<string> Hidden { get; init; } = [];

    public bool Locked { get; init; }

    public double? LeftWidth { get; init; }

    public double? RightWidth { get; init; }

    /// <summary>The upper place's share of the left side's height.</summary>
    public double? LeftSplit { get; init; }

    public double? RightSplit { get; init; }

    /// <summary>The layout a section holds, or none where there is no section.</summary>
    /// <param name="section">The section's keys and values.</param>
    /// <param name="panels">Every panel there is, with the place it starts in.</param>
    public static SavedLayout? Read(IReadOnlyDictionary<string, string> section, IReadOnlyList<(string Title, DockSlot Home)> panels)
    {
        if (section.Count == 0)
        {
            return null;
        }

        var known = panels.Select(panel => panel.Title).ToHashSet();
        string[] Names(string key) => [.. (section.GetValueOrDefault(key) ?? "").Split(',').Where(known.Contains).Distinct()];

        return new SavedLayout
        {
            Groups = ReadGroups(section, panels),
            Chosen = Names("chosen"),
            Hidden = Names("hidden"),
            Locked = section.GetValueOrDefault("locked") == "true",
            LeftWidth = Number(section, "left_width", (220, 4000)),
            RightWidth = Number(section, "right_width", (260, 4000)),
            LeftSplit = Number(section, "left_split", (0.1, 0.9)),
            RightSplit = Number(section, "right_split", (0.1, 0.9)),
        };
    }

    /// <summary>The section's keys and values, as <see cref="Read"/> takes them.</summary>
    public IEnumerable<KeyValuePair<string, string>> Values()
    {
        foreach (var group in Groups.Where(group => group.Window is null).OrderBy(group => group.Slot))
        {
            yield return new(s_places[(int)group.Slot], string.Join(',', group.Titles));
        }

        foreach (var (group, at) in Groups.Where(group => group.Window is not null).Select((group, at) => (group, at + 1)))
        {
            var spot = group.Window!.Value;
            yield return new($"float_{at}", string.Create(CultureInfo.InvariantCulture, $"{string.Join(',', group.Titles)}|{group.Slot}|{spot.At.X} {spot.At.Y} {spot.Size.Width} {spot.Size.Height}"));
        }

        yield return new("chosen", string.Join(',', Chosen));
        yield return new("hidden", string.Join(',', Hidden));
        yield return new("locked", Locked ? "true" : "false");
        (string Key, double? Value)[] numbers = [("left_width", LeftWidth), ("right_width", RightWidth), ("left_split", LeftSplit), ("right_split", RightSplit)];
        foreach (var (key, value) in numbers.Where(number => number.Value is not null))
        {
            yield return new(key, value!.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The point, or the fallback where no screen shows enough of a window's title bar there to drag it by.</summary>
    public static PixelPoint OnScreen(PixelPoint at, IReadOnlyList<PixelRect> screens, PixelPoint fallback)
    {
        var grab = at + new PixelVector(40, 10);
        return screens.Count == 0 || screens.Any(screen => screen.Contains(grab)) ? at : fallback;
    }

    // The docked groups claim their panels first, then the windows. A panel no group claims joins its home place.
    static List<SavedGroup> ReadGroups(IReadOnlyDictionary<string, string> section, IReadOnlyList<(string Title, DockSlot Home)> panels)
    {
        var free = panels.Select(panel => panel.Title).ToHashSet();
        List<string> Claim(string titles) => [.. titles.Split(',').Where(free.Remove)];

        var docked = Enum.GetValues<DockSlot>().ToDictionary(slot => slot, slot => Claim(section.GetValueOrDefault(s_places[(int)slot]) ?? ""));
        var floating = new List<SavedGroup>();
        foreach (var key in section.Keys.Where(key => key.StartsWith("float_", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            if (ReadWindow(section[key]) is { } window && Claim(window.Titles) is { Count: > 0 } titles)
            {
                floating.Add(new SavedGroup(window.Slot, titles, window.Spot));
            }
        }

        foreach (var (title, home) in panels.Where(panel => free.Contains(panel.Title)))
        {
            docked[home].Add(title);
        }

        return [.. docked.Where(place => place.Value.Count > 0).Select(place => new SavedGroup(place.Key, place.Value)), .. floating];
    }

    // "Titles|Place|x y width height", or none where any part cannot be read.
    static (string Titles, DockSlot Slot, FloatSpot Spot)? ReadWindow(string value)
    {
        if (value.Split('|') is not [var titles, var place, var box]
            || !Enum.GetValues<DockSlot>().Any(slot => slot.ToString() == place)
            || box.Split(' ') is not [var x, var y, var width, var height]
            || !int.TryParse(x, CultureInfo.InvariantCulture, out var left)
            || !int.TryParse(y, CultureInfo.InvariantCulture, out var top)
            || Number(width, (120, 4000)) is not { } across
            || Number(height, (120, 4000)) is not { } down)
        {
            return null;
        }

        return (titles, Enum.Parse<DockSlot>(place), new FloatSpot(new PixelPoint(left, top), new Size(across, down)));
    }

    static double? Number(IReadOnlyDictionary<string, string> section, string key, (double Least, double Most) bounds) =>
        Number(section.GetValueOrDefault(key), bounds);

    static double? Number(string? text, (double Least, double Most) bounds) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? Math.Clamp(number, bounds.Least, bounds.Most)
            : null;
}

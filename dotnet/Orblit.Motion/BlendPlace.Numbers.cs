using System.Text;
using Orblit.Json;
using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion;

// The place as numbers and as decoded JSON, read back against the blend that owns its states.
public sealed partial class BlendPlace
{
    private static readonly UTF8Encoding Strict = new(false, true);

    private static readonly JsonMap NoMetadata = new Dictionary<string, object?>();

    /// <summary>
    /// A place out of decoded JSON, or null when it is not one.
    /// </summary>
    /// <remarks>
    /// Read against <paramref name="blend"/>, because a save file outlives the blend it was saved with. A
    /// state the blend no longer has is dropped with whatever it was fading from, so a character saved
    /// mid-fade into a state that has since been taken out comes back in the state it was leaving; one saved
    /// in such a state does not come back at all, and the caller starts it afresh.
    /// </remarks>
    public static BlendPlace? FromJson(object? raw, BlendDocument blend) => Read(raw, blend, Deepest);

    /// <summary>
    /// The place as numbers, starting with the <see cref="Width"/> number prefix.
    /// </summary>
    /// <remarks>
    /// A state goes by its place in the blend's list rather than by name, so both ends need the same blend.
    /// For a save file, which may be read by a later build with its states moved about,
    /// <see cref="ToJson"/> names them.
    /// <para>
    /// The first number is how many places there are; then five for each, newest first: the state, the lap,
    /// the fade gone, the fade in all, and the easing. Routes and frozen poses append a length and UTF-8
    /// JSON bytes as numbers. Use JSON or a variable-length transport for these extended places.
    /// </para>
    /// </remarks>
    public double[] ToNumbers(BlendDocument blend)
    {
        var places = new List<BlendPlace>();
        for (var at = this; at is not null && places.Count < Deepest; at = at.From)
        {
            places.Add(at);
        }

        var extra = places.Any(static place => place.Frozen is not null || place.Route.Count > 0)
            ? Encoding.UTF8.GetBytes(JsonText.Encode(places.Select(static place => place.Metadata()).ToList()))
            : [];
        var numbers = new double[Width + (extra.Length > 0 ? 1 + extra.Length : 0)];
        if (extra.Length > 0)
        {
            numbers[Width] = extra.Length;
            for (var i = 0; i < extra.Length; i++)
            {
                numbers[Width + 1 + i] = extra[i];
            }
        }

        for (var i = 0; i < places.Count; i++)
        {
            var at = places[i];
            var start = 1 + (i * Each);
            numbers[start] = blend.IndexOf(at.State);
            numbers[start + 1] = at.Lap;
            numbers[start + 2] = at.Faded;
            numbers[start + 3] = at.Fade;
            numbers[start + 4] = (int)at.Shape;
        }

        numbers[0] = places.Count;
        return numbers;
    }

    /// <summary>
    /// A place out of <paramref name="numbers"/> as <see cref="ToNumbers"/> wrote them, or null when they are
    /// not one <paramref name="blend"/> can play.
    /// </summary>
    public static BlendPlace? FromNumbers(BlendDocument blend, IReadOnlyList<double> numbers)
    {
        if (numbers.Count < Width || !double.IsFinite(numbers[0]))
        {
            return null;
        }

        var count = (int)Doubles.Clamp(Math.Round(numbers[0], MidpointRounding.AwayFromZero), 0, Deepest);
        if (ReadExtra(numbers, count) is not { } extra)
        {
            return null;
        }

        BlendPlace? built = null;
        for (var i = count - 1; i >= 0; i--)
        {
            var slot = numbers.Skip(1 + (i * Each)).Take(Each).ToList();
            built = Rebuilt(blend, slot, built, extra.Count == 0 ? NoMetadata : extra[i]);
        }

        return built;
    }

    private static BlendPlace? Read(object? raw, BlendDocument blend, int places)
    {
        if (Head(raw, blend, places) is not var (map, state))
        {
            return null;
        }

        var lap = LapOf(Values.MaybeNumber(map, "lap"));
        var route = ReadRoute(map.GetValueOrDefault("route"), blend, state);
        var from = Read(map.GetValueOrDefault("from"), blend, places - 1);
        var fade = Values.MaybeNumber(map, "fade") ?? 0;
        if (from is null || !(fade > 0) || !double.IsFinite(fade))
        {
            return new BlendPlace(blend.ResolveState(state), lap, journey: route);
        }

        var faded = Values.MaybeNumber(map, "faded") ?? 0;
        return new BlendPlace(
            blend.ResolveState(state),
            lap,
            from,
            double.IsFinite(faded) ? Doubles.Clamp(faded, 0, fade) : 0,
            fade,
            FileNames.Named<Easing>(map.GetValueOrDefault("shape")) ?? Easing.Smooth,
            PoseSnapshot.FromJson(map.GetValueOrDefault("frozen")),
            route);
    }

    // The map and the state of a written place, when it is one at all and the blend has its state.
    private static (JsonMap Map, string State)? Head(object? raw, BlendDocument blend, int places) =>
        raw is JsonMap map && places >= 1 && Values.Text(map, "state") is { } state && blend.StateNamed(state) is not null
            ? (map, state)
            : null;

    // The place written in slot (state, lap, faded, fade, shape) on top of what is older, or null when its
    // state is not one the blend has.
    private static BlendPlace? Rebuilt(BlendDocument blend, List<double> slot, BlendPlace? older, JsonMap metadata)
    {
        var index = slot[0];

        // Nothing older than a state this blend has not got can be kept.
        if (!double.IsFinite(index) || index != Math.Round(index) || index < 0 || index >= blend.States.Count)
        {
            return null;
        }

        var state = blend.States[(int)index].Name;
        var lap = LapOf(slot[1]);
        var route = ReadRoute(metadata.GetValueOrDefault("route"), blend, state);
        var fade = slot[3];
        if (older is null || !(fade > 0) || !double.IsFinite(fade))
        {
            return new BlendPlace(state, lap, journey: route);
        }

        return new BlendPlace(
            state,
            lap,
            older,
            double.IsFinite(slot[2]) ? Doubles.Clamp(slot[2], 0, fade) : 0,
            fade,
            ShapeOf(slot[4]),
            PoseSnapshot.FromJson(metadata.GetValueOrDefault("frozen")),
            route);
    }

    private static Easing ShapeOf(double number)
    {
        var shapes = Enum.GetValues<Easing>();
        return double.IsFinite(number)
            ? shapes[(int)Doubles.Clamp(Math.Round(number, MidpointRounding.AwayFromZero), 0, shapes.Length - 1)]
            : Easing.Smooth;
    }

    private static BlendRoute? ReadRoute(object? raw, BlendDocument blend, string state)
    {
        if (raw is not JsonList { Count: > 0 } list)
        {
            return null;
        }

        var route = new List<string>();
        var from = blend.ResolveState(state);
        foreach (var next in list)
        {
            if (next is not string name || blend.StateNamed(name) is null)
            {
                return null;
            }

            var to = blend.ResolveState(name);
            var origin = from;
            if (!blend.Changes.Any(change => (change.From is null || change.From == origin) && change.To == to))
            {
                return null;
            }

            route.Add(to);
            from = to;
        }

        return new BlendRoute(route);
    }

    // The metadata of each of count places, or null when what follows the fixed prefix is not that.
    private static List<JsonMap>? ReadExtra(IReadOnlyList<double> numbers, int count)
    {
        if (numbers.Count == Width)
        {
            return [];
        }

        return ExtraBytes(numbers) is { } bytes ? ExtraMetadata(bytes, count) : null;
    }

    private static byte[]? ExtraBytes(IReadOnlyList<double> numbers)
    {
        var length = numbers[Width];
        if (!double.IsFinite(length) || length < 0 || length != Math.Round(length) || length != numbers.Count - Width - 1)
        {
            return null;
        }

        var bytes = new byte[numbers.Count - Width - 1];
        for (var i = 0; i < bytes.Length; i++)
        {
            var one = numbers[Width + 1 + i];
            if (!double.IsFinite(one) || one < 0 || one > 255 || one != Math.Round(one))
            {
                return null;
            }

            bytes[i] = (byte)one;
        }

        return bytes;
    }

    private static List<JsonMap>? ExtraMetadata(byte[] bytes, int count)
    {
        try
        {
            return JsonText.Decode(Strict.GetString(bytes)) is JsonList list && list.Count == count && list.All(static one => one is JsonMap)
                ? list.Cast<JsonMap>().ToList()
                : null;
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException)
        {
            return null;
        }
    }

    private static double LapOf(double? raw) => raw is { } lap && double.IsFinite(lap) && lap >= 0 ? lap : 0;

    private OrderedDictionary<string, object?> Metadata()
    {
        var written = new OrderedDictionary<string, object?>();
        if (Frozen is { } frozen)
        {
            written["frozen"] = frozen.ToJson();
        }

        if (Route.Count > 0)
        {
            written["route"] = Route;
        }

        return written;
    }
}

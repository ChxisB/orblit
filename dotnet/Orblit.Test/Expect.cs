using Orblit.Audio;
using Orblit.Scene;
using Orblit.VectorMath;

namespace Orblit.Test;

/// <summary>
/// Expectations about an entity and about recorded sound. Each returns when it holds and throws
/// <see cref="ExpectationException"/> when it does not.
/// </summary>
public static class Expect
{
    /// <summary>
    /// <paramref name="entity"/> holds a component of type <typeparamref name="T"/>. Name the type,
    /// because the base type would match any entity at all.
    /// </summary>
    public static void HasComponent<T>(this SceneEntity entity)
        where T : SceneComponent
    {
        if (typeof(T) == typeof(SceneComponent))
        {
            throw new ArgumentException("HasComponent needs the type of component to find.");
        }

        if (!entity.Components.Values.OfType<T>().Any())
        {
            var held = string.Join(", ", entity.Components.Values.Select(component => component.GetType().Name));
            throw new ExpectationException($"Expected {entity.Name} to have a {typeof(T).Name}, but its components are [{held}].");
        }
    }

    /// <summary>
    /// The position of <paramref name="entity"/> itself, not counting its parents, is within
    /// <paramref name="within"/> of <paramref name="point"/>. Fails for an entity with no transform.
    /// </summary>
    public static void IsPositionedNear(this SceneEntity entity, Vector3 point, double within = 0.001)
    {
        if (entity[SceneComponents.Transform] is not TransformComponent transform)
        {
            throw new ExpectationException($"{entity.Name} has no transform.");
        }

        var distance = transform.Position.DistanceTo(point);
        if (!(distance <= within))
        {
            throw new ExpectationException($"Expected {entity.Name} to be within {within} of {point}, but its distance is {distance}.");
        }
    }

    /// <summary>The sound started <paramref name="clip"/> at some point, finished or not.</summary>
    public static void HasPlayed(this RecordedAudio audio, string clip)
    {
        var started = audio.Calls.OfType<AudioStarted>().Select(call => call.Clip).ToList();
        if (!started.Contains(clip))
        {
            throw new ExpectationException($"Expected sound that started \"{clip}\", but the clips started are {Quoted(started)}.");
        }
    }

    /// <summary>A voice of <paramref name="clip"/> is playing now.</summary>
    public static void IsSounding(this RecordedAudio audio, string clip)
    {
        var playing = audio.Calls.OfType<AudioStarted>().Where(call => audio.IsPlaying(call.Voice)).Select(call => call.Clip).ToList();
        if (!playing.Contains(clip))
        {
            throw new ExpectationException($"Expected sound with \"{clip}\" playing, but the clips playing are {Quoted(playing)}.");
        }
    }

    private static string Quoted(IEnumerable<string> clips) => $"[{string.Join(", ", clips.Select(clip => $"\"{clip}\""))}]";
}

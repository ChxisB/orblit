using Orblit.Audio;
using Orblit.Scene;
using Orblit.VectorMath;

namespace Orblit.Test.Tests;

public sealed class ExpectTests
{
    private static readonly IReadOnlyDictionary<string, SceneComponent> Lit =
        new Dictionary<string, SceneComponent> { [SceneComponents.Light] = new LightComponent() };

    [Fact]
    public void AnEntityHasTheKindOfComponentItHolds()
    {
        Things.Make("lamp", components: Lit).HasComponent<LightComponent>();
    }

    [Fact]
    public void AnEntityWithoutThatKindFailsAndNamesTheEntityAndTheKind()
    {
        var error = Assert.Throws<ExpectationException>(() => Things.Make("floor").HasComponent<LightComponent>());

        Assert.Contains("floor", error.Message);
        Assert.Contains(nameof(LightComponent), error.Message);
    }

    [Fact]
    public void AskingForTheBaseKindIsRefusedBecauseEveryComponentWouldPass()
    {
        var error = Assert.Throws<ArgumentException>(() => Things.Make("lamp", components: Lit).HasComponent<SceneComponent>());

        Assert.Contains("type of component", error.Message);
    }

    [Fact]
    public void AnEntityIsNearAPointWithinTheDistance()
    {
        Things.Make("crate", at: new Vector3(1, 2, 3)).IsPositionedNear(new Vector3(1, 2, 3.0005));
        Things.Make("crate", at: new Vector3(0, 0, 0)).IsPositionedNear(new Vector3(0, 3, 4), within: 5);
    }

    [Fact]
    public void AnEntityFarFromThePointFailsAndSaysHowFar()
    {
        var error = Assert.Throws<ExpectationException>(
            () => Things.Make("crate", at: Vector3.Zero()).IsPositionedNear(new Vector3(0, 3, 4)));

        Assert.Contains("crate", error.Message);
        Assert.Contains("distance", error.Message);
        Assert.Contains("5", error.Message);
    }

    [Fact]
    public void AnEntityWithNoTransformIsNowhere()
    {
        var error = Assert.Throws<ExpectationException>(
            () => Things.Make("crate").IsPositionedNear(Vector3.Zero()));

        Assert.Contains("crate has no transform", error.Message);
    }

    [Fact]
    public void ANotANumberPositionIsNeverNear()
    {
        Assert.Throws<ExpectationException>(
            () => Things.Make("crate", at: new Vector3(double.NaN, 0, 0)).IsPositionedNear(Vector3.Zero(), within: 1e9));
    }

    [Fact]
    public void RecordedSoundHasPlayedAClipOnceItWasStarted()
    {
        var audio = new RecordedAudio();
        var player = new AudioPlayer(audio);
        player.Play("door");

        audio.HasPlayed("door");
        var error = Assert.Throws<ExpectationException>(() => audio.HasPlayed("alarm"));
        Assert.Contains("alarm", error.Message);
    }

    [Fact]
    public void ASoundIsSoundingUntilItsVoiceStops()
    {
        var audio = new RecordedAudio();
        var player = new AudioPlayer(audio);
        var voice = player.Play("door");

        audio.IsSounding("door");

        voice.Stop();
        var error = Assert.Throws<ExpectationException>(() => audio.IsSounding("door"));
        Assert.Contains("door", error.Message);
    }
}

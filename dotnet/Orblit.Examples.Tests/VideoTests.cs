using Orblit.Filament;

namespace Orblit.Examples.Tests;

/// <summary>
/// Where the video example gets its film. The fixture ran with
/// <c>ORBLIT_VIDEO</c> unset and nothing typed, so it never had a film.
/// </summary>
public sealed class VideoTests
{
    private const string Typed = "Or a path";

    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    private static readonly HashSet<string> OnDisk = ["/films/a.mp4"];

    [Fact]
    public void TheEnvironmentNamesTheFilmOnlyWhenItIsThere()
    {
        var found = new VideoExample(name => name == "ORBLIT_VIDEO" ? "/films/a.mp4" : null, OnDisk.Contains);
        var lost = new VideoExample(name => name == "ORBLIT_VIDEO" ? "/films/b.mp4" : null, OnDisk.Contains);

        Assert.Equal("/films/a.mp4", Assert.Single(found.Scene(Camera, 0).Videos).Path);
        Assert.Equal("/films/a.mp4", found.Control<TextEntry>(Typed).Value);
        Assert.Empty(lost.Scene(Camera, 0).Videos);
        Assert.Equal("", lost.Control<TextEntry>(Typed).Value);
    }

    /// <summary>
    /// One decoder, four screens. The fixture had no film, and a screen with no
    /// film to find draws the same whatever key it names, so this is checked here.
    /// </summary>
    [Fact]
    public void FourScreensShowTheOneFilm()
    {
        var scene = new VideoExample(name => name == "ORBLIT_VIDEO" ? "/films/a.mp4" : null, OnDisk.Contains).Scene(Camera, 0);

        var film = Assert.Single(scene.Videos).Key;
        Assert.Equal([film, film, film, film], scene.Materials.Where(one => one.Shading == MaterialShading.Video).Select(one => one.Video));
    }

    /// <summary>A missing file says so and leaves the film that was playing.</summary>
    [Fact]
    public void AMissingPathSaysSoAndKeepsTheFilm()
    {
        var example = new VideoExample(name => name == "ORBLIT_VIDEO" ? "/films/a.mp4" : null, OnDisk.Contains);

        example.Control<TextEntry>(Typed).Change(" /films/b.mp4 ");

        Assert.Equal("There is no file at that path.", example.Note);
        Assert.Equal("/films/a.mp4", Assert.Single(example.Scene(Camera, 0).Videos).Path);
        Assert.Equal(" /films/b.mp4 ", example.Control<TextEntry>(Typed).Value);
    }

    [Fact]
    public void AFoundPathPlaysAndAnEmptyOneStops()
    {
        var example = new VideoExample(_ => null, OnDisk.Contains) { Note = "There is no file at that path." };

        example.Control<TextEntry>(Typed).Change(" /films/a.mp4 ");
        Assert.Equal("/films/a.mp4", Assert.Single(example.Scene(Camera, 0).Videos).Path);
        Assert.Null(example.Note);

        example.Note = "stale";
        example.Control<TextEntry>(Typed).Change("  ");
        Assert.Empty(example.Scene(Camera, 0).Videos);
        Assert.Null(example.Note);
    }

    /// <summary>The renderer jumps when the token moves, so a second press of the same button still jumps.</summary>
    [Fact]
    public void EachPressMovesTheToken()
    {
        var example = new VideoExample(name => name == "ORBLIT_VIDEO" ? "/films/a.mp4" : null, OnDisk.Contains);
        var jumps = example.Control<Buttons>("");
        Assert.Equal(["0s", "2s", "4s"], jumps.Options);

        jumps.Press("2s");
        jumps.Press("2s");

        var video = Assert.Single(example.Scene(Camera, 0).Videos);
        Assert.Equal((2.0, 2L), (video.SeekTo, video.SeekToken));
    }

    /// <summary>The Dart rate ran from -2 to 3, so a film can play backwards, and the field showed an example path.</summary>
    [Fact]
    public void TheControlsReadAsTheDartOnesDid()
    {
        var example = new VideoExample(_ => null, OnDisk.Contains);
        var rate = example.Control<Setting>("Rate");
        var fade = example.Control<Setting>("Fade");

        Assert.Equal((-2.0, 3.0, 0.0, 1.0), (rate.Min, rate.Max, fade.Min, fade.Max));
        Assert.Equal("/path/to/a.mp4", example.Control<TextEntry>(Typed).Hint);
    }

    [Fact]
    public void TheControlsReachTheFilm()
    {
        var example = new VideoExample(name => name == "ORBLIT_VIDEO" ? "/films/a.mp4" : null, OnDisk.Contains);

        example.Control<Toggle>("Playing").Change(false);
        example.Control<Toggle>("Loop").Change(false);
        example.Control<Setting>("Rate").Change(-1.5);

        var video = Assert.Single(example.Scene(Camera, 0).Videos);
        Assert.Equal((false, false, -1.5, 0.0, null, 0L), (video.Playing, video.Loop, video.Rate, video.Volume, video.SeekTo, video.SeekToken));
    }
}

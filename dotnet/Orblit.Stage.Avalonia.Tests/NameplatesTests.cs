using Orblit.Filament;
using Orblit.Ui.Avalonia;
using Orblit.VectorMath;

namespace Orblit.Stage.Avalonia.Tests;

public class NameplatesTests
{
    private static readonly RenderCamera Front = new() { Position = new Vector3(0, 0, 10), Target = Vector3.Zero() };

    private static Nameplate Plate(int id, string text, Vector3 at) => new(id, text, at);

    private static Nameplates Make(RecordedResources? resources = null, int maxLabels = 256)
    {
        resources ??= new RecordedResources();
        return new Nameplates(
            new LabelAtlas(new LabelAtlasOptions { Width = 256 }),
            new NameplatesOptions
            {
                FadeStart = 10,
                FadeEnd = 30,
                MaxLabels = maxLabels,
                Provide = resources.Provide,
                Release = resources.Release,
            });
    }

    // The plate with this id among what was built, as its object and its material.
    private static (RenderObject Object, RenderMaterial Material) Find(NameplateBatch built, Nameplates plates, int id)
    {
        var key = plates.Options.KeyBase + id;
        return (built.Objects.Single(o => o.Key == key), built.Materials.Single(m => m.Key == key));
    }

    private static Vector3 Axis(RenderObject plate, int column)
    {
        var axis = plate.Transform.GetColumn(column);
        return new Vector3(axis.X, axis.Y, axis.Z).Normalized();
    }

    [Fact]
    public Task ItsFaceIsSquareToTheView() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada"]);

        var built = plates.Build(Front, [Plate(1, "Ada", new Vector3(1, 2, 0))]);

        var (plate, _) = Find(built, plates, 1);
        Assert.Equal(0, Axis(plate, 2).X, 1e-9);
        Assert.Equal(1, Axis(plate, 2).Z, 1e-9);
        Assert.Equal(1, Axis(plate, 0).X, 1e-9);
        Assert.Equal(new Vector3(1, 2, 0), plate.Transform.GetTranslation());
    });

    [Fact]
    public Task ItFollowsTheCameraRound() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada"]);
        var side = Front with { Position = new Vector3(10, 0, 0) };

        var built = plates.Build(side, [Plate(1, "Ada", Vector3.Zero())]);

        var (plate, _) = Find(built, plates, 1);
        Assert.Equal(1, Axis(plate, 2).X, 1e-9);
        Assert.Equal(0, Axis(plate, 2).Z, 1e-9);
    });

    [Fact]
    public Task LookingStraightDownStillGivesAUsableTurn() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada"]);
        var down = Front with { Position = new Vector3(0, 10, 0) };

        var built = plates.Build(down, [Plate(1, "Ada", Vector3.Zero())]);

        var (plate, _) = Find(built, plates, 1);
        Assert.All(Enumerable.Range(0, 16), at => Assert.True(double.IsFinite(plate.Transform[at])));
        Assert.Equal(1, Axis(plate, 2).Y, 1e-9);
    });

    [Fact]
    public Task APlateFadesOutBetweenTheTwoRangesAndIsThenGone() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada"]);

        // The camera is ten metres from the origin, so z = 0 is at ten and moving the plate back from
        // it adds to the distance.
        var built = plates.Build(Front,
        [
            Plate(1, "Ada", new Vector3(0, 0, 5)),
            Plate(2, "Ada", new Vector3(0, 0, -10)),
            Plate(3, "Ada", new Vector3(0, 0, -21)),
        ]);

        Assert.Equal(1, Find(built, plates, 1).Material.BaseColour.W);
        Assert.Equal(0.5, Find(built, plates, 2).Material.BaseColour.W, 1e-9);
        Assert.DoesNotContain(built.Objects, o => o.Key == plates.Options.KeyBase + 3);
    });

    [Fact]
    public Task PastTheCapTheNearestAreKept() => Headless.Run(() =>
    {
        var plates = Make(maxLabels: 2);
        plates.Sync(["Ada"]);

        var built = plates.Build(Front,
        [
            Plate(1, "Ada", new Vector3(0, 0, -5)),
            Plate(2, "Ada", new Vector3(0, 0, 5)),
            Plate(3, "Ada", Vector3.Zero()),
        ]);

        Assert.Equal(new long[] { 2, 3 }, built.Objects.Select(o => o.Key - plates.Options.KeyBase).Order());
    });

    [Fact]
    public Task PlatesAtTheSameDistanceKeepTheOrderTheyWereGivenIn() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada"]);

        var built = plates.Build(Front,
        [
            Plate(5, "Ada", new Vector3(1, 0, 0)),
            Plate(2, "Ada", new Vector3(-1, 0, 0)),
            Plate(9, "Ada", new Vector3(0, 1, 0)),
        ]);

        Assert.Equal(new long[] { 5, 2, 9 }, built.Objects.Select(o => o.Key - plates.Options.KeyBase));
    });

    [Fact]
    public Task TheSameTextSharesOneRectangle() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada", "Brannock"]);

        var built = plates.Build(Front,
        [
            Plate(1, "Ada", Vector3.Zero()),
            Plate(2, "Ada", new Vector3(2, 0, 0)),
            Plate(3, "Brannock", new Vector3(4, 0, 0)),
        ]);

        var one = Find(built, plates, 1).Material;
        var two = Find(built, plates, 2).Material;
        var three = Find(built, plates, 3).Material;
        Assert.Equal(one.Tiling, two.Tiling);
        Assert.Equal(one.Offset, two.Offset);
        Assert.NotEqual(one.Offset, three.Offset);
        Assert.Equal(one.BaseColourMap!.Path, three.BaseColourMap!.Path);
    });

    [Fact]
    public Task ARectangleBecomesTheRightPartOfThePicture() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada"]);
        var rect = plates.Atlas.RectOf("Ada")!.Value;
        var size = plates.Atlas.Size;

        var built = plates.Build(Front, [Plate(1, "Ada", Vector3.Zero())]);

        var material = Find(built, plates, 1).Material;
        Assert.Equal(rect.Width / size.Width, material.Tiling.X, 1e-9);
        Assert.Equal(-rect.Height / size.Height, material.Tiling.Y, 1e-9);
        Assert.Equal(rect.Left / size.Width, material.Offset.X, 1e-9);
        Assert.Equal(rect.Bottom / size.Height, material.Offset.Y, 1e-9);
    });

    [Fact]
    public Task APlateIsAsBigAsItsTextAtThePixelSize() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada"]);
        var rect = plates.Atlas.RectOf("Ada")!.Value;

        var built = plates.Build(Front, [Plate(1, "Ada", Vector3.Zero())]);

        // The cube spans -1 to 1, so its width is twice the x axis' length.
        var (plate, _) = Find(built, plates, 1);
        var x = plate.Transform.GetColumn(0);
        var across = new Vector3(x.X, x.Y, x.Z).Length * 2;
        Assert.Equal(rect.Width * plates.Options.MetresPerPixel, across, 1e-9);
    });

    [Fact]
    public Task AStringThePictureLacksIsLeftOutUntilItIsAdded() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Sync(["Ada"]);
        var stranger = Plate(2, "Zed", Vector3.Zero());

        Assert.Empty(plates.Build(Front, [stranger]).Objects);

        plates.Sync(["Zed"]);
        Assert.Single(plates.Build(Front, [stranger]).Objects);
    });

    [Fact]
    public Task NothingIsBuiltBeforeTheFirstSync() => Headless.Run(() =>
    {
        var plates = Make();
        plates.Atlas.Add("Ada");

        var built = plates.Build(Front, [Plate(1, "Ada", Vector3.Zero())]);

        Assert.Empty(built.Objects);
        Assert.Empty(built.Materials);
    });

    [Fact]
    public Task ANewStringIsANewNameAndTheOldIsReleased() => Headless.Run(() =>
    {
        var resources = new RecordedResources();
        var plates = Make(resources);
        plates.Sync(["Ada"]);
        var first = resources.Provided.Single();
        Assert.StartsWith("orblit:resource/panel/nameplates/", first);

        plates.Sync(["Ada", "Brannock"]);

        Assert.Equal(2, resources.Provided.Count());
        Assert.NotEqual(first, resources.Provided.Last());
        Assert.Equal(first, resources.Released.Single());
    });

    [Fact]
    public Task DisposeReleasesThePicture() => Headless.Run(() =>
    {
        var resources = new RecordedResources();
        var plates = Make(resources);
        plates.Sync(["Ada"]);
        var name = resources.Provided.Single();

        plates.Dispose();

        Assert.Equal(("release", name), resources.Calls.Last());
        Assert.Empty(plates.Build(Front, [Plate(1, "Ada", Vector3.Zero())]).Objects);
    });

    [Fact]
    public Task StringsAlreadyInThePictureSendNothing() => Headless.Run(() =>
    {
        var resources = new RecordedResources();
        var plates = Make(resources);
        plates.Sync(["Ada"]);
        resources.Calls.Clear();

        plates.Sync(["Ada"]);

        Assert.Empty(resources.Calls);
    });

    [Fact]
    public Task AFadeThatEndsWhereItStartsIsRefused() => Headless.Run(() =>
    {
        var options = new NameplatesOptions { FadeStart = 10, FadeEnd = 10 };

        Assert.Throws<ArgumentException>(() => new Nameplates(new LabelAtlas(), options));
    });

    [Fact]
    public Task ANegativeCapIsRefused() => Headless.Run(() =>
    {
        var options = new NameplatesOptions { FadeStart = 1, FadeEnd = 2, MaxLabels = -1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => new Nameplates(new LabelAtlas(), options));
    });
}

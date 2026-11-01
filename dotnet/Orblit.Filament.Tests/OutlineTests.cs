using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// The outline as it leaves the packer: which keys, in what
/// order, and the row of settings the renderer reads by position.
/// </summary>
public sealed class OutlineTests
{
    private static RenderScene SceneWith(Outline? outline = null)
    {
        var scene = SceneBuilders.Of(
            [SceneBuilders.Plain(1), SceneBuilders.Plain(2)],
            SceneBuilders.CameraAt(0, 0, 5));
        return outline is null ? scene : scene with { Outline = outline };
    }

    [Fact]
    public void NothingOutlinedIsWhatASceneHasUntilSomebodySaysOtherwise()
    {
        var scene = SceneWith();

        Assert.True(scene.Outline.IsEmpty);
        Assert.Empty(SceneBuilders.Read<long[]>(scene.ToMessage(), "outlineKeys"));
    }

    [Fact]
    public void NothingOutlinedStillSendsAWholeRowOfSettingsSoThePluginNeverReadsOneShort()
    {
        var packed = SceneBuilders.Read<float[]>(SceneWith().ToMessage(), "outlineParams");

        Assert.Equal(Outline.Stride, packed.Length);
        // No active object.
        Assert.Equal(0f, packed[12]);
    }

    [Fact]
    public void TheKeysPutTheActiveObjectFirstAndOnlyOnce()
    {
        var outline = new Outline { Keys = [3, 7, 9], Primary = 7 };

        Assert.Equal([7L, 3L, 9L], outline.PackedKeys);
        Assert.Equal(1f, outline.Packed[12]);
    }

    [Fact]
    public void TheKeysOutlineAnActiveObjectThatIsNotInTheSet()
    {
        var outline = new Outline { Primary = 4 };

        Assert.False(outline.IsEmpty);
        Assert.Equal([4L], outline.PackedKeys);
        Assert.Equal(1f, outline.Packed[12]);
    }

    [Fact]
    public void TheKeysHaveNoActiveObjectUnlessOneIsNamed()
    {
        var outline = new Outline { Keys = [5, 6] };

        Assert.Equal([5L, 6L], outline.PackedKeys);
        Assert.Equal(0f, outline.Packed[12]);
    }

    [Fact]
    public void TheSettingsArePackedInTheOrderTheRendererReadsThem()
    {
        var packed = new Outline
        {
            Keys = [1],
            Colour = DisplayColour.FromArgb(0xFF00FF00),
            PrimaryColour = DisplayColour.FromArgb(0x80FF0000),
            Width = 3,
            Occluded = OutlineOccluded.Faint,
            OccludedOpacity = 0.25,
            Dash = 5,
        }.Packed;

        Assert.Equal([0f, 1f, 0f, 1f], packed[0..4]);
        Assert.Equal(1f, packed[4]);
        Assert.Equal(0f, packed[5]);
        Assert.Equal(0f, packed[6]);
        SceneBuilders.Near(128 / 255.0, packed[7]);
        Assert.Equal(3f, packed[8]);
        Assert.Equal((float)OutlineOccluded.Faint, packed[9]);
        Assert.Equal(0.25f, packed[10]);
        Assert.Equal(5f, packed[11]);
    }

    [Fact]
    public void TheSettingsHoldTheWidthToWhatTheRendererCanDraw()
    {
        Assert.Equal((float)Outline.MaxWidth, new Outline { Width = 400 }.Packed[8]);
        Assert.Equal(0f, new Outline { Width = -2 }.Packed[8]);
    }

    [Fact]
    public void TheSettingsNumberTheHiddenStylesAsTheRendererDoes()
    {
        // Shown, faint, dashed, hidden: 0 to 3 in the renderer header. A reordering here would
        // draw a hidden edge in the wrong style with no error.
        Assert.Equal(["Shown", "Faint", "Dashed", "Hidden"], Enum.GetNames<OutlineOccluded>());
        Assert.Equal([0, 1, 2, 3], Enum.GetValues<OutlineOccluded>().Select(one => (int)one));
    }

    [Fact]
    public void SurvivesWithOnTheSceneAndCanBeReplacedByIt()
    {
        var scene = SceneWith(new Outline { Keys = [1] });

        Assert.Equal([1L], (scene with { Sky = new Sky() }).Outline.Keys);
        var replaced = scene with { Outline = new Outline { Primary = 2 } };
        Assert.Equal([2L], replaced.Outline.PackedKeys);
    }

    [Fact]
    public void WithCanTakeTheActiveObjectAway()
    {
        var outline = new Outline { Keys = [1, 2], Primary = 2 };

        Assert.Null((outline with { Primary = null }).Primary);
        Assert.Equal(2, (outline with { Width = 4 }).Primary);
    }
}

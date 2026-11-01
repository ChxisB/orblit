using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>The image work as the renderer receives it.</summary>
public sealed class PostTests
{
    [Fact]
    public void WhatArrivesIsAsLongAsTheRendererExpects()
    {
        Assert.Equal(PostProcess.Stride, new PostProcess().Packed.Length);
    }

    [Fact]
    public void StartsWithEverythingOffButThePictureStillMade()
    {
        var post = new PostProcess();

        // A renderer that arrives with bloom on is one where the first question is how to turn
        // it off.
        Assert.False(post.Bloom.Enabled);
        Assert.False(post.DepthOfField.Enabled);
        Assert.False(post.Vignette.Enabled);
        Assert.False(post.Occlusion.Enabled);
        Assert.False(post.Reflections.Enabled);
        Assert.False(post.Grading.Enabled);

        // But tone mapping and anti aliasing happen anyway: something has to decide how light
        // becomes pixels.
        Assert.True(post.Enabled);
        Assert.Equal(ToneMapping.Filmic, post.Grading.ToneMapping);
        Assert.Equal(AntiAliasing.Fxaa, post.AntiAliasing);
        Assert.True(post.Dithering);
    }

    [Fact]
    public void CarriesEveryNumberItWasGiven()
    {
        var post = new PostProcess
        {
            Bloom = new Bloom { Enabled = true, Strength = 0.4, Levels = 8 },
            Vignette = new Vignette { Enabled = true, MidPoint = 0.3 },
            Grading = new Grading
            {
                Enabled = true,
                ToneMapping = ToneMapping.Aces,
                Exposure = 1.5,
                Saturation = 1.2,
            },
        };

        var packed = post.Packed;

        // Compared loosely because the array is float32: 0.4 as a double is not 0.4 as a float,
        // and an exact match would be testing the width of the array and not the value in it.
        bool Carries(double wanted) => packed.Any(value => Math.Abs(value - wanted) < 1e-6);

        Assert.True(Carries(0.4), "bloom strength");
        Assert.True(Carries(8), "bloom levels");
        Assert.True(Carries(1.5), "exposure");
        Assert.True(Carries(1.2), "saturation");
        Assert.Equal((float)(int)ToneMapping.Aces, packed[35]);
    }

    [Fact]
    public void FlagsAreOneAndNoughtAndNothingElse()
    {
        var packed = new PostProcess { Bloom = new Bloom { Enabled = true } }.Packed;

        Assert.All(packed, value => Assert.True(float.IsFinite(value)));
        Assert.Equal(1f, packed[0]);
        Assert.Equal(1f, packed[3]);
    }

    [Fact]
    public void AColourTripleIsThreeNumbersInOrder()
    {
        var post = new PostProcess
        {
            Grading = new Grading
            {
                Shadows = new Vector3(0.1, 0.2, 0.3),
                Midtones = new Vector3(0.4, 0.5, 0.6),
                Highlights = new Vector3(0.7, 0.8, 0.9),
            },
        };

        SceneBuilders.NearAll(post.Packed[(PostProcess.Stride - 9)..], 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9);
    }

    [Fact]
    public void TheSceneHasOneAndSendsIt()
    {
        var message = SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 0)).ToMessage();

        Assert.Equal(PostProcess.Stride, SceneBuilders.Read<float[]>(message, "postParams").Length);
    }

    [Fact]
    public void ALookBelongsToTheSceneAndNotToACamera()
    {
        // Four views of one world should not each grade it differently.
        var scene = SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 0)) with
        {
            Post = new PostProcess { Bloom = new Bloom { Enabled = true } },
        };

        Assert.True(scene.Post.Bloom.Enabled);
    }
}

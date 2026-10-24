using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// What a sprite draws is the colour it was given, and layers draw in order. Measured through the
/// whole path a 2D scene takes: an orthographic camera, straight-through tone mapping, an image
/// provided as bytes, and a camera exposure that is not one, which a sprite's colour must ignore
/// because an artist's colours are not a photograph of anything.
/// </summary>
public sealed class SpriteDrawTests
{
    private const uint Width = 64;
    private const uint Height = 48;

    private static string White => RenderResources.NameFor("test/white.png");

    private static string RedOverGreen => RenderResources.NameFor("test/redgreen.png");

    private static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "orblit_filament", name));

    /// <summary>
    /// The renderer's own placeholder cube stays in the scene, so this is also a sprite drawn in
    /// front of something solid under an orthographic camera.
    /// </summary>
    private static RenderScene Flat(params SpriteLayer[] layers) =>
        new()
        {
            Objects = [SceneBuilders.Plain(1)],
            Camera = new RenderCamera
            {
                Position = new Vector3(0, 0, 10),
                Target = Vector3.Zero(),
                Orthographic = true,
                ViewHeight = 10,
                Aperture = 1,
                ShutterSpeed = 1,
                Sensitivity = 100,
            },
            Sky = new Sky { Colour = Vector3.Zero(), Ambient = 0, ShowBody = false, Drawn = false },
            Post = new PostProcess
            {
                AntiAliasing = AntiAliasing.Off,
                Dithering = false,
                Grading = new Grading { ToneMapping = ToneMapping.Linear },
            },
            Sprites = layers,
        };

    // One sprite covering the whole view, at a depth in front of anything the renderer puts there.
    private static SpriteLayer Layer(int key, int order, string? image, double red, double green, double blue) =>
        new(
            key,
            SpriteLayer.Pack(
                [new LayerSprite { X = 0, Y = 0, Depth = 5, Width = 40, Height = 40, Red = red, Green = green, Blue = blue }]))
        {
            Order = order,
            Image = image is null ? null : new Texture(image),
        };

    private static SpriteLayer Grey(int order) => Layer(1, order, White, 0.2, 0.2, 0.2);

    private static SpriteLayer Green(int order) => Layer(2, order, null, 0, 0.2, 0);

    [RendererFact]
    public void ASpriteDrawsInTheColourItWasGivenWhateverTheExposure()
    {
        try
        {
            RenderResources.Provide(White, Fixture("white.png"));
            using var renderer = new Renderer(Width, Height);
            renderer.Apply(Flat(Grey(0)));

            var grey = Pictures.Middle(renderer, 1.0);

            // Linear 0.2 is 124 in sRGB. Had the exposure reached it, it would read 114.
            Assert.All(grey, channel => Assert.InRange(channel, 120, 128));
        }
        finally
        {
            RenderResources.Release(White);
        }
    }

    [RendererFact]
    public void TheLayerWithTheHigherOrderDrawsOnTopAndSwappingTheOrdersSwapsWhatIsOnTop()
    {
        try
        {
            RenderResources.Provide(White, Fixture("white.png"));
            using var renderer = new Renderer(Width, Height);

            renderer.Apply(Flat(Grey(0), Green(1)));
            var greenOnTop = Pictures.Middle(renderer, 2.0);
            renderer.Apply(Flat(Grey(2), Green(1)), at: 2.0);
            var greyOnTop = Pictures.Middle(renderer, 3.0);

            Assert.True(greenOnTop[1] > greenOnTop[0] + 60, "the layer with the higher order draws on top");
            Assert.True(Math.Abs(greyOnTop[0] - greyOnTop[1]) <= 4 && greyOnTop[0] > 60, "and swapping the orders swaps what is on top");
        }
        finally
        {
            RenderResources.Release(White);
        }
    }

    [RendererFact]
    public void APictureIsTheRightWayUp()
    {
        // A rectangle's V0 is the top of the picture, so red over green cut whole is red at the top
        // of the sprite. A white picture cannot tell a flipped sprite from a correct one.
        try
        {
            RenderResources.Provide(RedOverGreen, Fixture("redgreen.png"));
            using var renderer = new Renderer(Width, Height);
            renderer.Apply(Flat(Layer(1, 0, RedOverGreen, 1, 1, 1)));

            var top = Pictures.RowColour(renderer, 4.0, lower: false);
            var bottom = Pictures.RowColour(renderer, 5.0, lower: true);

            Assert.True(top[0] > top[1] + 100, "the top of the picture is at the top");
            Assert.True(bottom[1] > bottom[0] + 100, "and the bottom of it at the bottom");
        }
        finally
        {
            RenderResources.Release(RedOverGreen);
        }
    }
}

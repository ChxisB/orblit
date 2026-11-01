using System.Text;
using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// A capture's spherical harmonics, in the pixels. View-dependent colour is a claim about what
/// reaches the screen, and a frame is the only place such a claim can be settled. The same five
/// splats are drawn from two sides, and then again with their bands left out, so one run says both
/// that the colour moves with the view and that nothing moves without the bands.
/// </summary>
public sealed class SplatDrawTests
{
    // Large enough that a three sigma ellipse is more than the shader's own low-pass widening.
    private const uint Width = 512;
    private const uint Height = 384;

    private static readonly string[] PlyProperties =
    [
        "x", "y", "z",
        "f_dc_0", "f_dc_1", "f_dc_2",
        "f_rest_0", "f_rest_1", "f_rest_2",
        "f_rest_3", "f_rest_4", "f_rest_5",
        "f_rest_6", "f_rest_7", "f_rest_8",
        "opacity", "scale_0", "scale_1", "scale_2",
        "rot_0", "rot_1", "rot_2", "rot_3",
    ];

    // One in the middle and four around it, all in the plane the x axis runs through, so the two
    // views are mirror images and the middle pixel is the same in both.
    private static readonly float[][] PlyPlaces =
    [
        [0f, 0f, 0f], [0f, 0.35f, 0f], [0f, -0.35f, 0f], [0f, 0f, 0.35f], [0f, 0f, -0.35f],
    ];

    /// <summary>
    /// Every f_dc is nought, so the flat colour is the mid grey from everywhere. Red's third
    /// coefficient is -1 and blue's is +1, and the third basis function is -sqrt(3/4pi) x, so a
    /// splat seen from -x, where the direction towards it is +x, is red, and the same splat seen
    /// from +x is blue. The file is the binary little-endian PLY the trainers write.
    /// </summary>
    private static byte[] FivePly()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            var header = new StringBuilder("ply\nformat binary_little_endian 1.0\nelement vertex 5\n");
            foreach (var name in PlyProperties)
            {
                header.Append("property float ").Append(name).Append('\n');
            }

            writer.Write(Encoding.ASCII.GetBytes(header.Append("end_header\n").ToString()));
            foreach (var place in PlyPlaces)
            {
                var row = new float[PlyProperties.Length];
                place.CopyTo(row, 0);
                row[8] = -1; // f_rest_2: red's third coefficient
                row[14] = 1; // f_rest_8: blue's
                row[15] = 6; // opacity as a logit, so all but opaque
                row[16] = row[17] = row[18] = -1.9f; // log scales, about 15 cm
                row[19] = 1; // the identity rotation, (w, x, y, z)
                foreach (var value in row)
                {
                    writer.Write(value);
                }
            }
        }

        return stream.ToArray();
    }

    // A wall behind each viewpoint, so that a frame with no cloud in it is not the same picture as
    // a frame with a black cloud in it. They are black: the sun has no light and the sky has none,
    // because 1% of a sunlit surface is tens of thousands of times brighter than an unlit cloud and
    // would wash the middle of the frame white.
    private static RenderObject Wall(long key, double x) =>
        SceneBuilders.Plain(key) with
        {
            Transform = Matrix4.Compose(new Vector3(x, 0, 0), Quaternion.Identity(), new Vector3(0.1, 2, 2)),
            Colour = new Vector3(0.10, 0.35, 0.12),
        };

    private static RenderScene Seen(SplatCloud cloud, double eyeX) =>
        new()
        {
            Objects = [Wall(200, 3), Wall(201, -3)],
            Camera = new RenderCamera
            {
                Position = new Vector3(eyeX, 0, 0),
                Target = Vector3.Zero(),
                FieldOfView = 42,
                // Splats are unlit, but the camera's exposure still multiplies them, and the daylight
                // exposure a scene with a sun in it wants takes a mid grey down to a single level.
                Aperture = 1,
                ShutterSpeed = 1,
                Sensitivity = 100,
            },
            Lights = [new RenderLight { Key = 1, Kind = RenderLightKind.Directional, Intensity = 0 }],
            Sky = new Sky { Colour = Vector3.Zero(), Ambient = 0, ShowBody = false, Drawn = false },
            Post = new PostProcess
            {
                AntiAliasing = AntiAliasing.Off,
                Dithering = false,
                Grading = new Grading { ToneMapping = ToneMapping.Linear },
            },
            Splats = [cloud],
        };

    /// <summary>The middle of the frame seen from -x and then from +x, at <paramref name="degree"/> bands.</summary>
    private static (double[] Left, double[] Right) BothWays(Renderer renderer, string path, int degree)
    {
        var cloud = new SplatCloud(1, path) { Harmonics = degree };
        renderer.Apply(Seen(cloud, -1.5));
        var left = Pictures.Middle(renderer, 1.0);
        renderer.Apply(Seen(cloud, 1.5));
        var right = Pictures.Middle(renderer, 2.0);
        return (left, right);
    }

    private static void WithTheCaptureOnDisk(Action<string> body)
    {
        var directory = Directory.CreateTempSubdirectory("orblit_splat_harmonics_");
        try
        {
            var path = Path.Combine(directory.FullName, "five.ply");
            File.WriteAllBytes(path, FivePly());
            body(path);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [RendererFact]
    public void ACaptureWithHarmonicsIsRedFromOneSideAndBlueFromTheOtherAndFlatWithoutThem()
    {
        WithTheCaptureOnDisk(path =>
        {
            using var renderer = new Renderer(Width, Height);

            var (left, right) = BothWays(renderer, path, degree: 1);
            var (flatLeft, flatRight) = BothWays(renderer, path, degree: 0);

            Assert.True(left[0] > left[2] + 24, "from one side the cloud is red");
            Assert.True(right[2] > right[0] + 24, "from the other side the same cloud is blue");
            // The two views are mirror images and not one image, so two levels and not none.
            for (var channel = 0; channel < 3; channel++)
            {
                Assert.InRange(flatLeft[channel] - flatRight[channel], -2, 2);
            }

            Assert.True(left[0] > flatLeft[0] + 24 && right[0] + 24 < flatRight[0], "the bands move the colour away from the flat one, each way");
        });
    }

    [RendererFact]
    public void ACaptureProvidedAsBytesAndNamedWithADetourDrawsWhatTheFileDrew()
    {
        var name = RenderResources.NameFor("captures/five.ply");
        WithTheCaptureOnDisk(path =>
        {
            using var renderer = new Renderer(Width, Height);
            var (left, right) = BothWays(renderer, path, degree: 1);
            try
            {
                RenderResources.Provide(name, File.ReadAllBytes(path));

                var (namedLeft, namedRight) = BothWays(renderer, RenderResources.NameFor("captures/../captures/five.ply"), degree: 1);

                foreach (var channel in new[] { 0, 2 })
                {
                    Assert.InRange(namedLeft[channel] - left[channel], -2, 2);
                    Assert.InRange(namedRight[channel] - right[channel], -2, 2);
                }
            }
            finally
            {
                Assert.True(RenderResources.Release(name), "and can be let go of afterwards");
            }
        });
    }
}

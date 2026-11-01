using System.Text;

namespace Orblit.Mesh.Tests;

public class ObjReadTests
{
    private const string Square = "v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\n";

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Repeat(string text, int times) => string.Concat(Enumerable.Repeat(text, times));

    private static ObjModel Read(
        string text,
        Dictionary<string, string>? files = null,
        ReadLimits? limits = null) => ObjFiles.ReadObj(
            Bytes(text),
            (files ?? []).ToDictionary(entry => entry.Key, entry => Bytes(entry.Value)),
            limits ?? new ReadLimits());

    // faces

    [Fact]
    public void AQuadAndAPentagonKeepTheirCorners()
    {
        var model = Read("v 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nv -1 0.5 0\nf 1 2 3 4\nf 1 4 5\nf 1 2 3 4 5\n");
        Assert.Equal(new[] { 4, 3, 5 }, model.Mesh.Faces.Select(f => f.Length));
        Assert.Equal(5, model.Mesh.VertexCount);
    }

    [Fact]
    public void ANegativeIndexCountsBackFromTheLastPointRead()
    {
        var model = Read($"{Square}\nf -4 -3 -2 -1\n");
        Assert.Equal(new[] { 0, 1, 2, 3 }, model.Mesh.Faces.Single().Vertices);
    }

    [Fact]
    public void ACornerMayCarryACoordinateAndANormalOrOnlyANormal()
    {
        var model = Read($"{Square}\nvt 0 0\nvt 1 0\nvt 1 1\nvt 0 1\nvn 0 0 1\nf 1/1/1 2/2/1 3/3/1 4/4/1\nf 1//1 2//1 3//1\n");
        var first = model.Mesh.Faces[0].Uv;
        Assert.True(first.IsManual);
        Assert.Equal(new[] { 0.0, 0.0, 1.0, 1.0 }, first.Manual!.Select(uv => uv.Y));
        Assert.False(model.Mesh.Faces[^1].Uv.IsManual);
    }

    [Fact]
    public void AFaceWithACornerThatIsNotThereIsDroppedWithANote()
    {
        var model = Read($"{Square}\nf 1 2 3 4\nf 1 2 9\nf 1 2\n");
        Assert.Equal(1, model.Mesh.FaceCount);
        Assert.Equal(2, model.Problems.Count);
        Assert.Contains("Line 7", model.Problems[0]);
    }

    [Fact]
    public void ALineEndingInABackslashGoesOnToTheNext()
    {
        var model = Read($"{Square}\nf 1 2 \\\n 3 4\n");
        Assert.Equal(4, model.Mesh.Faces.Single().Length);
    }

    [Fact]
    public void ASmoothingGroupTurnsSmoothingOnAndOffTurnsItOff()
    {
        var model = Read($"{Square}\ns 1\nf 1 2 3\ns off\nf 1 3 4\n");
        Assert.Equal(new[] { true, false }, model.Mesh.Faces.Select(f => f.Smooth));
    }

    // groups and materials

    private const string Mtl =
        "newmtl red\nKd 1 0 0\nPr 0.25\nPm 0.75\nnewmtl glass\nKd 0.2 0.4 0.6\nd 0.5\nKe 0.1 0.2 0.3\nmap_Kd glass.png\n";

    [Fact]
    public void EachObjectOrGroupIsItsOwnGroupOfFaces()
    {
        var model = Read($"{Square}\no left\nf 1 2 3\ng right extra\nf 1 3 4\no left\nf 1 2 4\n");
        Assert.Equal(new[] { "left", "right extra" }, model.Groups.Select(g => g.Name));
        Assert.Equal(new[] { 0, 2 }, model.Groups[0].Faces);
        Assert.Equal(2, model.MeshOf(model.Groups[0]).FaceCount);
    }

    [Fact]
    public void FacesBeforeAnyNameAreInAGroupCalledDefault()
    {
        var model = Read($"{Square}\nf 1 2 3\n");
        Assert.Equal("default", model.Groups.Single().Name);
    }

    [Fact]
    public void AGroupCarriesOnlyThePointsItsFacesUse()
    {
        var model = Read($"{Square}\nv 5 5 5\no far\nf 1 2 3\no near\nf 1 3 4\n");
        var near = model.MeshOf(model.Groups[^1]);
        Assert.Equal(3, near.VertexCount);
        Assert.All(near.Faces.Single().Vertices, i => Assert.True(i < 3));
    }

    [Fact]
    public void UsemtlPicksAMaterialFromTheLibraryMtllibNames()
    {
        var model = Read(
            $"mtllib one.mtl\n{Square}\nusemtl glass\nf 1 2 3\nusemtl red\nf 1 3 4\n",
            files: new Dictionary<string, string> { ["one.mtl"] = Mtl });
        Assert.Equal(new[] { "red", "glass" }, model.Materials.Select(m => m.Name));
        Assert.Equal(new[] { 1, 0 }, model.Mesh.Faces.Select(f => f.Material));
        var red = model.Materials[0];
        Assert.Equal(new[] { 1.0, 0.0, 0.0 }, red.Colour.Take(3));
        Assert.Equal(0.25, red.Roughness);
        Assert.Equal(0.75, red.Metallic);
        var glass = model.Materials[^1];
        Assert.Equal(0.5, glass.Colour[3]);
        Assert.Equal(new[] { 0.1, 0.2, 0.3 }, glass.Emissive);
        Assert.Contains("glass", model.Problems.Single());
        Assert.Contains("pictures", model.Problems.Single());
    }

    [Fact]
    public void ALibraryThatWasNotGivenIsANoteAndTheNameIsKept()
    {
        var model = Read($"mtllib gone.mtl\n{Square}\nusemtl wood\nf 1 2 3\n");
        Assert.Equal("wood", model.Materials.Single().Name);
        Assert.Contains("gone.mtl", model.Problems.Single());
    }

    [Fact]
    public void ALibraryIsFoundByItsFileNameWhateverFolderItNames()
    {
        var model = Read(
            $"mtllib models/one.mtl\n{Square}\nusemtl red\nf 1 2 3\n",
            files: new Dictionary<string, string> { ["one.mtl"] = "newmtl red\nKd 1 0 0\n" });
        Assert.Equal("red", model.Materials[0].Name);
        Assert.Empty(model.Problems);
    }

    [Fact]
    public void OneNumberForKdIsAGrey()
    {
        var model = Read(
            $"mtllib a.mtl\n{Square}\nusemtl a\nf 1 2 3\n",
            files: new Dictionary<string, string> { ["a.mtl"] = "newmtl a\nKd 0.5\n" });
        Assert.Equal(new[] { 0.5, 0.5, 0.5 }, model.Materials.Single().Colour.Take(3));
        Assert.Empty(model.Problems);
    }

    [Fact]
    public void RoughnessComesFromTheExponentWhenThereIsNoPr()
    {
        var model = Read(
            $"mtllib a.mtl\n{Square}\nusemtl a\nf 1 2 3\n",
            files: new Dictionary<string, string> { ["a.mtl"] = "newmtl a\nNs 901\n" });
        Assert.Equal(0, model.Materials.Single().Roughness, 1e-9);
    }

    // limits

    [Fact]
    public void AFileOverTheByteLimitIsRefusedBeforeItIsRead()
    {
        Assert.ThrowsAny<FormatException>(() => Read(Square, limits: new ReadLimits { Bytes = 10 }));
    }

    [Fact]
    public void TooManyPointsOrTooManyFacesStopTheRead()
    {
        Assert.ThrowsAny<FormatException>(() => Read($"{Square}\nf 1 2 3", limits: new ReadLimits { Vertices = 3 }));
        Assert.ThrowsAny<FormatException>(
            () => Read($"{Square}\nf 1 2 3\nf 1 3 4", limits: new ReadLimits { Faces = 1 }));
    }

    [Fact]
    public void AFaceWithTooManyCornersIsDroppedNotTheFile()
    {
        var model = Read($"{Square}\nf 1 2 3 4 1\nf 1 2 3", limits: new ReadLimits { Corners = 4 });
        Assert.Equal(1, model.Mesh.FaceCount);
        Assert.Contains("corners", model.Problems.Single());
    }

    [Fact]
    public void AThousandBadLinesMakeAShortListOfNotes()
    {
        var model = Read($"{Square}\nf 1 2 3\n{Repeat("f 1 2 99\n", 1000)}");
        Assert.True(model.Problems.Count < 60);
        Assert.Contains("more", model.Problems[^1]);
    }

    [Fact]
    public void NumbersThatAreNotNumbersOrNotFiniteAreNotes()
    {
        var model = Read($"v 1e999 0 0\nv a b c\nv 1 2\n{Square}\nf 4 5 6");
        Assert.Equal(7, model.Mesh.VertexCount);
        Assert.Equal(3, model.Problems.Count);
    }

    [Fact]
    public void APointThatCannotBeReadKeepsTheNumbersAfterIt()
    {
        var model = Read("v 0 0 0\nv 1 oops 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt nope\nvt 1 1\nf 1 3 4\nf 1 2 3\nf 1/1 3/3 4/3\n");
        Assert.Equal(2, model.Mesh.Faces.Count);
        Assert.All(model.Mesh.Faces, f => Assert.Equal(new[] { 0, 2, 3 }, f.Vertices));
        Assert.Equal(1, model.Mesh.Faces[^1].Uv.Manual![^1].X);
        Assert.Equal(3, model.Problems.Count);
    }

    [Fact]
    public void OnlyPlainDecimalNumbersNameAPoint()
    {
        var model = Read($"{Square}\nf 1 2 3\nf 0x1 2 3\nf +1 2 3\n");
        Assert.Equal(1, model.Mesh.FaceCount);
        Assert.Equal(2, model.Problems.Count);
    }

    [Fact]
    public void TooManyNamesCornersOrCharactersStopTheRead()
    {
        Assert.ThrowsAny<FormatException>(() => Read(
            $"{Square}\no a\nf 1 2 3\no b\nf 1 2 3\no c\nf 1 2 3",
            limits: new ReadLimits { Names = 2 }));
        Assert.ThrowsAny<FormatException>(() => Read(
            $"{Square}\nusemtl a\nusemtl b\nusemtl c\nf 1 2 3",
            limits: new ReadLimits { Names = 2 }));
        Assert.ThrowsAny<FormatException>(() => Read(
            $"{Square}\nf 1 2 3\nf 1 3 4 2",
            limits: new ReadLimits { Indices = 6 }));
        Assert.ThrowsAny<FormatException>(() => Read(
            $"{Square}\nf 1 2 3 # {new string('x', 40)}\n",
            limits: new ReadLimits { Line = 30 }));
        Assert.ThrowsAny<FormatException>(() => Read(
            $"{Square}\nf 1 2 \\\n{Repeat("3 ", 20)}\n",
            limits: new ReadLimits { Line = 30 }));
    }

    [Fact]
    public void ALibraryNamedAgainAndAgainIsReadOnce()
    {
        var library = string.Join("\n", Enumerable.Range(0, 20000).Select(i => $"newmtl m{i}\nKd 1 0 0"));
        var model = Read(
            $"{Repeat("mtllib big.mtl\n", 2000)}{Square}\nf 1 2 3",
            files: new Dictionary<string, string> { ["big.mtl"] = library });
        Assert.Equal(20000, model.Materials.Count);
    }

    [Fact]
    public void NoFacesAtAllIsNotAModel()
    {
        Assert.ThrowsAny<FormatException>(() => Read(Square));
        Assert.ThrowsAny<FormatException>(() => Read(""));
        Assert.ThrowsAny<FormatException>(() => ObjFiles.ReadObj([7, 7, 7]));
    }

    // written by this package

    [Fact]
    public void AMeshWrittenAsObjIsReadBackAsTheSameGeometry()
    {
        var cube = Shape.Of(ShapeKind.Cube).Build();
        var files = cube.ToObj(name: "box", materials: [new GlbMaterial { Name = "paint", Metallic = 0.5 }]);
        var model = ObjFiles.ReadObj(files[0].Bytes, files.ToDictionary(file => file.Name, file => file.Bytes));

        Assert.Empty(model.Problems);
        Assert.Equal(cube.VertexCount, model.Mesh.VertexCount);
        Assert.Equal(new[] { 4, 4, 4, 4, 4, 4 }, model.Mesh.Faces.Select(f => f.Length));
        Assert.Equal("box", model.Groups.Single().Name);
        Assert.Equal("paint", model.Materials.Single().Name);
        Assert.Equal(0.5, model.Materials.Single().Metallic, 1e-6);
        for (var i = 0; i < cube.FaceCount; i++)
        {
            Assert.Equal(cube.Faces[i].Vertices, model.Mesh.Faces[i].Vertices);
            Assert.Equal(4, model.Mesh.Faces[i].Uv.Manual!.Count);
        }

        var a = cube.Bounds;
        var b = model.Mesh.Bounds;
        Assert.Equal(0, (b.Min - a.Min).Length, 1e-6);
        Assert.Equal(0, (b.Max - a.Max).Length, 1e-6);
    }

    // bounds

    [Fact]
    public void BoundsOfObjGivesTheBoxOfThePoints()
    {
        var box = ObjFiles.BoundsOfObj(Bytes("v -1 0 2\nv 3 4 5\nv 0 0 0\nf 1 2 3\n"))!.Value;
        Assert.Equal(new[] { -1.0, 0.0, 0.0 }, new[] { box.Min.X, box.Min.Y, box.Min.Z });
        Assert.Equal(new[] { 3.0, 4.0, 5.0 }, new[] { box.Max.X, box.Max.Y, box.Max.Z });
    }

    [Fact]
    public void AnythingThatIsNotAModelHasNone()
    {
        Assert.Null(ObjFiles.BoundsOfObj([]));
        Assert.Null(ObjFiles.BoundsOfObj(Bytes("v 1 2 3\n")));
        Assert.Null(ObjFiles.BoundsOfObj([0, 255, 3]));
    }

    // fuzz

    private const string FuzzCube =
        "mtllib a.mtl\nv -1 -1 -1\nv 1 -1 -1\nv 1 1 -1\nv -1 1 -1\nvt 0 0\nvt 1 0\nvt 1 1\no box\nusemtl a\ns 1\nf 1/1 2/2 3/3 4/1\nf -1 -2 -3\n";

    private static void Survives(byte[] bytes)
    {
        try
        {
            ObjFiles.ReadObj(bytes, new Dictionary<string, byte[]> { ["a.mtl"] = Bytes("newmtl a\nKd 1 1 1\n") });
        }
        catch (FormatException)
        {
            // A refusal is an answer. Anything else is the bug.
        }
    }

    [Fact]
    public void RandomBytesAreReadOrRefusedNeverACrash()
    {
        // Seeded, but a different stream from the fixture's, so the bytes differ. The parity fixture covers the
        // shared cases.
        var random = new Random(19);
        for (var round = 0; round < 400; round++)
        {
            var bytes = new List<byte>();
            for (var i = 0; i < random.Next(2000); i++)
            {
                bytes.Add((byte)random.Next(256));
            }

            Survives([.. bytes]);
        }
    }

    [Fact]
    public void AValidFileWithBytesChangedIsReadOrRefused()
    {
        var random = new Random(20);
        var source = Bytes(FuzzCube);
        for (var round = 0; round < 600; round++)
        {
            var changed = (byte[])source.Clone();
            for (var n = 0; n < 1 + random.Next(8); n++)
            {
                changed[random.Next(changed.Length)] = (byte)random.Next(256);
            }

            Survives(changed);
        }
    }

    [Fact]
    public void NumbersAtTheEdgeOfWhatAnIntOrADoubleHolds()
    {
        string[] edges =
        [
            "99999999999999999999",
            "-99999999999999999999",
            "9223372036854775807",
            "-9223372036854775808",
            "0",
            "-0",
            "1e308",
            "-1e308",
            "nan",
            "inf",
        ];
        foreach (var edge in edges)
        {
            Survives(Bytes($"v {edge} {edge} {edge}\n{Square}\nf {edge} 1 2 3\nf 1 2 3"));
        }
    }
}

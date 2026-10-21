namespace Orblit.Asset.Tests;

public class AssetIdTests
{
    private readonly AssetId _robot = AssetId.Parse("models/robots/robot.gltf");

    // an asset id

    [Fact]
    public void AWellFormedIdIsKeptExactlyAsItWasWritten()
    {
        Assert.Equal("models/robot.glb", AssetId.Parse("models/robot.glb").ToString());
        Assert.Equal("robot.glb", AssetId.Parse("robot.glb").ToString());
        Assert.Equal("textures/metal plate.png", AssetId.Parse("textures/metal plate.png").ToString());
    }

    [Fact]
    public void TwoIdsSpeltTheSameAreTheSameId()
    {
        var a = AssetId.Parse("models/robot.glb");
        var b = AssetId.Parse("models/robot.glb");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Single(new HashSet<AssetId> { a, b });
        Assert.NotEqual(a, AssetId.Parse("models/Robot.glb"));
    }

    [Fact]
    public void ItsNameAndDirectoryAreTheTwoHalvesOfItsPath()
    {
        var id = AssetId.Parse("models/robots/robot.glb");
        Assert.Equal("robot.glb", id.Name);
        Assert.Equal("models/robots", id.Directory);
    }

    [Fact]
    public void AnIdAtTheRootHasAnEmptyDirectoryNotAMissingOne()
    {
        var id = AssetId.Parse("robot.glb");
        Assert.Equal("robot.glb", id.Name);
        Assert.Equal("", id.Directory);
    }

    [Fact]
    public void TheExtensionIsLowerCaseAndHasNoDot()
    {
        Assert.Equal("glb", AssetId.Parse("models/ROBOT.GLB").Extension);
        Assert.Equal("gz", AssetId.Parse("scene.tar.gz").Extension);
    }

    [Fact]
    public void ANameWithNoDotOrOnlyALeadingOneHasNoExtension()
    {
        Assert.Equal("", AssetId.Parse("README").Extension);
        Assert.Equal("", AssetId.Parse("config/.gitignore").Extension);
        Assert.Equal("", AssetId.Parse("notes.").Extension);
        // A dot in a folder name is not the file's extension.
        Assert.Equal("", AssetId.Parse("v1.2/README").Extension);
    }

    [Fact]
    public void TryParseGivesNullWhereverParseWouldThrow()
    {
        Assert.NotNull(AssetId.TryParse("models/robot.glb"));
        Assert.Null(AssetId.TryParse("models//robot.glb"));
    }

    // an asset id is refused when

    [Fact]
    public void ItIsEmpty() => Refused("", "empty");

    [Fact]
    public void ItUsesABackslash() => Refused(@"models\robot.glb", "backslash");

    [Fact]
    public void ItHasASchemeOrADriveLetterInIt()
    {
        Refused("https://example.com/robot.glb", "\":\"");
        Refused("C:/models/robot.glb", "\":\"");
        Refused("asset:robot.glb", "\":\"");
    }

    [Fact]
    public void ItStartsWithASlash() => Refused("/models/robot.glb", "starts with \"/\"");

    [Fact]
    public void ItHasAnEmptyFolderNameFromADoubledOrTrailingSlash()
    {
        Refused("models//robot.glb", "empty folder name");
        Refused("models/", "empty folder name");
    }

    // The two Dart names differ only in their quotes, which are words here.
    [Fact]
    public void ItHasADotInPlaceOfAFolder()
    {
        Refused("./models/robot.glb", "\".\" folder");
        Refused("models/./robot.glb", "\".\" folder");
        Refused(".", "\".\" folder");
    }

    [Fact]
    public void ItHasADotDotInPlaceOfAFolder()
    {
        Refused("models/../robot.glb", "\"..\"");
        Refused("../robot.glb", "\"..\"");
        Refused("..", "\"..\"");
    }

    [Fact]
    public void TheExceptionPointsAtWhereTheProblemIs()
    {
        var error = Assert.Throws<AssetFormatException>(() => AssetId.Parse("models/../robot.glb"));
        Assert.Equal("models/../robot.glb", error.Text);
        Assert.Equal(7, error.Offset);
    }

    [Fact]
    public void DotsInsideANameAreNotMistakenForOr()
    {
        Assert.NotNull(AssetId.TryParse("models/...glb"));
        Assert.NotNull(AssetId.TryParse("models/.hidden/robot.glb"));
    }

    // resolving a reference from inside an asset

    [Fact]
    public void APlainNameIsASibling() =>
        Assert.Equal(AssetId.Parse("models/robots/robot.bin"), _robot.Resolve("robot.bin"));

    [Fact]
    public void ARelativePathGoesDownFromTheDirectory() =>
        Assert.Equal(AssetId.Parse("models/robots/textures/metal.png"), _robot.Resolve("textures/metal.png"));

    [Fact]
    public void PercentEscapesAreDecoded() =>
        Assert.Equal(AssetId.Parse("models/robots/metal plate.png"), _robot.Resolve("metal%20plate.png"));

    [Fact]
    public void DotAndDotDotAreCollapsed()
    {
        Assert.Equal(AssetId.Parse("models/robots/metal.png"), _robot.Resolve("./textures/../metal.png"));
        Assert.Equal(AssetId.Parse("shared/metal.png"), _robot.Resolve("../../shared/metal.png"));
    }

    [Fact]
    public void ADotDotThatClimbsPastTheRootIsRefused()
    {
        Assert.Null(_robot.Resolve("../../../secret.png"));
        Assert.Null(AssetId.Parse("robot.gltf").Resolve("../robot.bin"));
    }

    [Fact]
    public void AnEscapedDotDotIsRefusedJustTheSame() =>
        Assert.Null(_robot.Resolve("%2E%2E/%2E%2E/%2E%2E/secret.png"));

    [Fact]
    public void SomethingResolvingToTheRootItselfIsRefused() => Assert.Null(_robot.Resolve("../.."));

    [Fact]
    public void AbsolutePathsAndUrisWithASchemeAreRefused()
    {
        Assert.Null(_robot.Resolve("/etc/passwd"));
        Assert.Null(_robot.Resolve("https://example.com/metal.png"));
        Assert.Null(_robot.Resolve("data:application/octet-stream;base64,AAAA"));
    }

    [Fact]
    public void ReferencesThatDecodeToSomethingThatIsNotAnIdAreRefused()
    {
        Assert.Null(_robot.Resolve(""));
        Assert.Null(_robot.Resolve("metal%zz.png"));
        Assert.Null(_robot.Resolve("metal%2"));
        Assert.Null(_robot.Resolve("textures%5Cmetal.png"));
        Assert.Null(_robot.Resolve("textures//metal.png"));
    }

    private static void Refused(string text, string saying)
    {
        var error = Assert.Throws<AssetFormatException>(() => AssetId.Parse(text));
        Assert.Contains(saying, error.Message);
        Assert.Null(AssetId.TryParse(text));
    }
}

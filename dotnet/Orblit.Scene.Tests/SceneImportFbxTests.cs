using Orblit.Light;
using static Orblit.Scene.Tests.ImportDocs;

namespace Orblit.Scene.Tests;

public class SceneImportFbxTests
{
    /// <summary>
    /// What the importer makes of an ASCII FBX holding a triangle, a camera at 0, 2, 5 cm, a spot
    /// light of 200 W and a sun of 3 W/m2 turned a quarter about Z. It is the converter's output, so
    /// the test reads what a cooked FBX gives and the native check reads how it was made.
    /// </summary>
    private static readonly byte[] CookedRig = Convert.FromBase64String(
        "Z2xURgIAAADcBQAAcAUAAEpTT057ImFzc2V0Ijp7ImdlbmVyYXRvciI6Ik9yYmxpdCBpbXBv"
        + "cnQiLCJ2ZXJzaW9uIjoiMi4wIn0sImV4dGVuc2lvbnNVc2VkIjpbIktIUl9saWdodHNfcHVu"
        + "Y3R1YWwiXSwic2NlbmUiOjAsInNjZW5lcyI6W3sibm9kZXMiOlswLDEsMiwzXX1dLCJub2Rl"
        + "cyI6W3sibmFtZSI6IlRyaSIsIm1lc2giOjB9LHsibmFtZSI6IkNhbSIsInRyYW5zbGF0aW9u"
        + "IjpbMCwwLjAxOTk5OTk5OTYsMC4wNTAwMDAwMDA3XSwicm90YXRpb24iOlswLC0wLjcwNzEw"
        + "Njc2OSwwLDAuNzA3MTA2NzY5XSwiY2FtZXJhIjowfSx7Im5hbWUiOiJMYW1wIiwicm90YXRp"
        + "b24iOlstMC43MDcxMDY3NjksMCwwLDAuNzA3MTA2NzY5XSwiZXh0ZW5zaW9ucyI6eyJLSFJf"
        + "bGlnaHRzX3B1bmN0dWFsIjp7ImxpZ2h0IjowfX19LHsibmFtZSI6IlN1biIsInJvdGF0aW9u"
        + "IjpbLTAuNSwtMC41LDAuNSwwLjVdLCJleHRlbnNpb25zIjp7IktIUl9saWdodHNfcHVuY3R1"
        + "YWwiOnsibGlnaHQiOjF9fX1dLCJtZXNoZXMiOlt7Im5hbWUiOiJUcmkiLCJwcmltaXRpdmVz"
        + "IjpbeyJhdHRyaWJ1dGVzIjp7IlBPU0lUSU9OIjowLCJOT1JNQUwiOjF9LCJpbmRpY2VzIjoy"
        + "fV19XSwiY2FtZXJhcyI6W3sidHlwZSI6InBlcnNwZWN0aXZlIiwicGVyc3BlY3RpdmUiOnsi"
        + "eWZvdiI6MS4wNDcxOTc1OCwiem5lYXIiOjAuMDA0OTk5OTk5ODksInpmYXIiOjJ9fV0sImFj"
        + "Y2Vzc29ycyI6W3siYnVmZmVyVmlldyI6MCwiY29tcG9uZW50VHlwZSI6NTEyNiwiY291bnQi"
        + "OjMsInR5cGUiOiJWRUMzIiwibWluIjpbMCwwLDBdLCJtYXgiOlswLjAwOTk5OTk5OTc4LDAu"
        + "MDA5OTk5OTk5NzgsMF19LHsiYnVmZmVyVmlldyI6MSwiY29tcG9uZW50VHlwZSI6NTEyNiwi"
        + "Y291bnQiOjMsInR5cGUiOiJWRUMzIn0seyJidWZmZXJWaWV3IjoyLCJjb21wb25lbnRUeXBl"
        + "Ijo1MTIzLCJjb3VudCI6MywidHlwZSI6IlNDQUxBUiJ9XSwiYnVmZmVyVmlld3MiOlt7ImJ1"
        + "ZmZlciI6MCwiYnl0ZU9mZnNldCI6MCwiYnl0ZUxlbmd0aCI6MzYsInRhcmdldCI6MzQ5NjJ9"
        + "LHsiYnVmZmVyIjowLCJieXRlT2Zmc2V0IjozNiwiYnl0ZUxlbmd0aCI6MzYsInRhcmdldCI6"
        + "MzQ5NjJ9LHsiYnVmZmVyIjowLCJieXRlT2Zmc2V0Ijo3MiwiYnl0ZUxlbmd0aCI6NiwidGFy"
        + "Z2V0IjozNDk2M31dLCJleHRlbnNpb25zIjp7IktIUl9saWdodHNfcHVuY3R1YWwiOnsibGln"
        + "aHRzIjpbeyJ0eXBlIjoic3BvdCIsImNvbG9yIjpbMSwwLjUsMC4yNV0sImludGVuc2l0eSI6"
        + "MTA4NzAuMjgyMiwic3BvdCI6eyJpbm5lckNvbmVBbmdsZSI6MC4yNjE3OTkzOTUsIm91dGVy"
        + "Q29uZUFuZ2xlIjowLjUyMzU5ODc5fX0seyJ0eXBlIjoiZGlyZWN0aW9uYWwiLCJjb2xvciI6"
        + "WzEsMSwxXSwiaW50ZW5zaXR5IjoyMDQ5fV19fSwiYnVmZmVycyI6W3siYnl0ZUxlbmd0aCI6"
        + "Nzh9XX0gICBQAAAAQklOAAAAAAAAAAAAAAAAAArXIzwAAAAAAAAAAAAAAAAK1yM8AAAAAAAA"
        + "AAAAAAAAAACAPwAAAAAAAAAAAACAPwAAAAAAAAAAAACAPwAAAQACAAAA");

    private static readonly SceneImported Read = SceneImport.ReadSceneFrom(CookedRig, "Rig");

    private static SceneEntity Named(string name) => Assert.Single(Read.Document.Entities, e => e.Name == name);

    [Fact]
    public void ArrivesWithAnEntityForEachOfItsNodes() =>
        Assert.Equal(["Tri", "Cam", "Lamp", "Sun"], Read.Document.Entities.Select(e => e.Name));

    [Fact]
    public void TheCameraKeepsItsAngleAndPlanesInMetres()
    {
        var camera = Assert.IsType<CameraComponent>(Named("Cam")[SceneComponents.Camera]);

        Assert.Equal(60, camera.FieldOfView, 1e-4);
        Assert.Equal(0.005, camera.Near, 1e-6);
        Assert.Equal(2, camera.Far, 1e-6);
    }

    [Fact]
    public void TheLightsComeBackAtTheWattsTheFileStated()
    {
        var spot = Assert.IsType<LightComponent>(Named("Lamp")[SceneComponents.Light]);
        Assert.Equal(LightType.Spot, spot.Kind);
        Assert.Equal(200, spot.Power, 1e-3);
        Assert.Equal(60, spot.SpotSize, 1e-3);
        Assert.Equal(0.5, spot.SpotBlend, 1e-4);

        var sun = Assert.IsType<LightComponent>(Named("Sun")[SceneComponents.Light]);
        Assert.Equal(LightType.Sun, sun.Kind);
        Assert.Equal(3, sun.Power, 1e-3);
    }
}

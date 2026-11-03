using Orblit.Scene;
using static Orblit.Stage.Tests.Documents;

namespace Orblit.Stage.Tests;

public class CameraPlanesTests
{
    [Fact]
    public void ASceneCamerasPlanesReachTheRenderCamera()
    {
        var view = new DocumentView(
            Of(
                Thing(
                    "cam",
                    components: Parts(
                        (SceneComponents.Transform, new TransformComponent()),
                        (SceneComponents.Camera, new CameraComponent(near: 0.5, far: 250))))));

        var camera = view.CameraOf("cam")!;

        Assert.Equal(0.5, camera.Near);
        Assert.Equal(250, camera.Far);
    }
}

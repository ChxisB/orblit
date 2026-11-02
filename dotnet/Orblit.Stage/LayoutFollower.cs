using Orblit.Scene;

namespace Orblit.Stage;

/// <summary>
/// Picks the layout that suits a window and hands what changes to whatever shows the scene.
/// </summary>
/// <remarks>
/// Hands over the difference between the layout last shown and the one that now suits, and not the
/// whole scene. Pass <see cref="DocumentView.Apply"/> as <c>show</c>: a turn then changes only what
/// the two layouts set, so an object the game added, or one a cutscene is posing, is left as it is.
/// Build the view from the base scene, because the first call starts from there.
/// <para>
/// A resize that keeps the shape calls nothing, which is what makes it fine to call <see cref="Fit"/>
/// on every size change.
/// </para>
/// </remarks>
public sealed class LayoutFollower(SceneDocument scene, Action<SceneDiff> show)
{
    /// <summary>The layout last shown, or null until <see cref="Fit"/> has been called.</summary>
    public LayoutShape? Shape { get; private set; }

    /// <summary>Shows the layout for a window of this size. Call it at start and on every resize.</summary>
    public void Fit(double width, double height)
    {
        var shape = LayoutShapes.Of(width, height);
        if (shape == Shape)
        {
            return;
        }

        var shown = Shape is { } last ? scene.InLayout(last) : scene;
        show(SceneDiff.Between(shown, scene.InLayout(shape)));

        // After the call, so a view that was not ready is asked again by the next Fit.
        Shape = shape;
    }
}

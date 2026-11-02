using Orblit.Scene;

namespace Orblit.Stage;

/// <summary>
/// Picks the layout that suits a window and hands it to whatever shows the scene.
/// </summary>
/// <remarks>
/// Holds the scene as it is stored, layouts and all, because what shows it only holds the layout it
/// is showing. Pass <see cref="DocumentView.Replace"/> as <c>show</c>: a turn then swaps one layout
/// for the other as a diff, so what both share is not rebuilt.
/// <para>
/// A resize that keeps the shape calls nothing, which is what makes it fine to call <see cref="Fit"/>
/// on every size change.
/// </para>
/// </remarks>
public sealed class LayoutFollower(SceneDocument scene, Action<SceneDocument> show)
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

        Shape = shape;
        show(scene.InLayout(shape));
    }
}

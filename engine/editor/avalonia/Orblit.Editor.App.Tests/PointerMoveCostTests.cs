using System.Diagnostics;
using Orblit.Editor.Bridge;
using static Orblit.Editor.App.Tests.ReferenceWindow;

namespace Orblit.Editor.App.Tests;

public sealed class PointerMoveCostTests
{
    // The inspector reads the dragged entity only, never the tree, so its cost does not grow with the scene. The
    // engine side of a step is timed in the windowed checks. The median is about 0.05 ms headless, so the bound of 1 ms
    // has 20 times headroom.
    // shortcut: the bound only catches a large regression, tighten it once a machine runs it steadily.
    [Fact]
    public void The_inspector_after_a_drag_step_costs_under_the_bound()
    {
        UiThread.Run(() =>
        {
            var scene = new FakeScene();
            var window = OpenAtReferenceSize(scene);
            var lamp = scene.Outline()!.Find(1)!;
            window.Outline.SelectedItem = lamp;
            window.UpdateLayout();

            var ms = new double[50];
            for (var i = 0; i < ms.Length; i++)
            {
                var start = Stopwatch.GetTimestamp();
                window.InspectorPane.Show(scene, lamp);
                window.UpdateLayout();
                ms[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }

            Array.Sort(ms);
            Assert.True(ms[25] < 1, $"median {ms[25]:F3} ms, p95 {ms[47]:F3} ms");
        });
    }
}

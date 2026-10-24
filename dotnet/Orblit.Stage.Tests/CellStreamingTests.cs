using System.Text;
using Orblit.Cells;
using Orblit.Scene;
using Orblit.VectorMath;
using static Orblit.Stage.Tests.Documents;

namespace Orblit.Stage.Tests;

public class CellStreamingTests
{
    // A cell of ten rocks laid out inside it, in world coordinates.
    private static byte[] Rocks(Grid grid, Cell cell)
    {
        var centre = grid.CenterOf(cell);
        var rocks = Enumerable.Range(0, 10).Select(i => new SceneEntity(
            $"rock{i}",
            "rock",
            components: Parts(
                (SceneComponents.Transform, new TransformComponent(new Vector3(centre.X + (i * 0.1), 0, centre.Z))),
                (SceneComponents.Mesh, new MeshComponent("rock.glb")))));
        return Encoding.UTF8.GetBytes(new SceneDocument(entities: [.. rocks]).Encode());
    }

    [Fact]
    public async Task AViewFedCellsDrawsWhatTheStreamedDocumentHolds()
    {
        var grid = new Grid(10);
        var document = new SceneDocument();
        var view = new DocumentView(document);
        var streamer = new CellStreamer(
            new CellWindow(grid, 1),
            cell => Task.FromResult<byte[]?>(Rocks(grid, cell)),
            diff =>
            {
                document = diff.ApplyTo(document);
                view.Apply(diff);
            },
            budget: TimeSpan.FromSeconds(30),
            inFlight: 4);

        // Walks east across the world, a cell at a time, waiting for the fetches to land.
        for (var x = 5.0; x < 65; x += 10)
        {
            var at = x;
            await Waits.Until(
                () =>
                {
                    streamer.Step(at, 5);
                    return streamer.Pending == 0;
                },
                $"the cells around {at}");
            Assert.True(view.Scene.Objects.Count == document.Length, $"at {at}");
            Assert.True(ViewShape.Positions(view) == ViewShape.Positions(new DocumentView(document)), $"at {at}");
        }

        // A cell stays until it is two away, so it does not flicker on a border.
        // At 55 that keeps cells 3 to 6, and nothing from the cells before it.
        Assert.All(streamer.Loaded, cell => Assert.InRange(cell.X, 3, 6));
        Assert.Equal(streamer.Loaded.Count * 10, view.Scene.Objects.Count);
        Assert.All(view.Scene.Objects, one => Assert.True(one.Transform.GetTranslation().X > 30));
    }
}

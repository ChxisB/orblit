using System.Numerics;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests;

public sealed class SelectionBoxTests
{
    static readonly Transform3D Identity = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero);

    [Fact]
    public void A_box_round_an_unmoved_entity_is_the_bounds_themselves()
    {
        var box = SelectionBox.Around(Identity, new Aabb(new Vector3(1, 2, 3), new Vector3(2, 4, 6)));

        Assert.Equal(new Transform3D(new Vector3(2, 0, 0), new Vector3(0, 4, 0), new Vector3(0, 0, 6), new Vector3(2, 4, 6)), box);
    }

    [Fact]
    public void The_origin_follows_the_entity()
    {
        var place = Identity with { Origin = new Vector3(10, 20, 30) };

        var box = SelectionBox.Around(place, new Aabb(new Vector3(-0.5f), Vector3.One));

        Assert.Equal(new Vector3(10, 20, 30), box.Origin);
    }

    // A quarter turn about Y sends the entity's X axis to -Z and its Z axis to X. The size stays on the
    // entity's own axes, so a box twice as long in X is twice as long along -Z in the scene.
    [Fact]
    public void The_box_turns_with_the_entity_and_keeps_its_size_on_the_entity_axes()
    {
        var turned = new Transform3D(new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(-1, 0, 0), Vector3.Zero);

        var box = SelectionBox.Around(turned, new Aabb(new Vector3(-1, -0.5f, -0.5f), new Vector3(2, 1, 1)));

        Assert.Equal(new Transform3D(new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(-2, 0, 0), Vector3.Zero), box);
    }

    [Fact]
    public void The_centre_is_moved_into_the_scene_by_the_entity_turn()
    {
        var turned = new Transform3D(new Vector3(0, 0, 1), new Vector3(0, 1, 0), new Vector3(-1, 0, 0), Vector3.Zero);

        var box = SelectionBox.Around(turned, new Aabb(new Vector3(1, 0, 0), Vector3.One));

        Assert.Equal(new Vector3(0.5f, 0.5f, -1.5f), box.Origin);
    }
}

using Orblit.Filament;
using Orblit.Scene;
using static Orblit.Stage.Tests.Documents;

namespace Orblit.Stage.Tests;

public class ShaderWearingTests
{
    private const string Shader = "shaders/pulse.oshader";

    private static SceneEntity Wearing(string id, params (string Name, object Value)[] values)
    {
        var given = new Dictionary<string, object>();
        foreach (var (name, value) in values)
        {
            given[name] = value;
        }

        return Thing(
            id,
            components: Parts(
                (SceneComponents.Mesh, new MeshComponent()),
                (SceneComponents.Material, new MaterialComponent(Shader, values: given))));
    }

    private static DocumentView Uncooked(params SceneEntity[] entities) => new(Of(entities), projectRoot: "/p");

    private static DocumentView ViewOf(params SceneEntity[] entities)
    {
        var view = Uncooked(entities);
        view.SetSurfaces(new Dictionary<string, CookedSurface> { [Shader] = PulseShader.Cooked });
        return view;
    }

    private static CustomSurface SurfaceOf(DocumentView view, long? key) =>
        view.Scene.Materials.Single(one => one.Key == key).Surface!;

    [Fact]
    public void ObjectsThatStateNothingShareOneMaterialWithTheDefaults()
    {
        var view = ViewOf(Wearing("a"), Wearing("b"));
        var keys = view.Scene.Objects.Select(one => one.Material).Distinct().ToList();

        var key = Assert.Single(keys);
        Assert.Single(view.Scene.Materials);
        var surface = SurfaceOf(view, key);
        Assert.Equal("/p/cook/pulse.filamat", surface.Package);
        Assert.Equal([1.5], surface.Values["speed"]);
        Assert.Equal([1.0, 0.5, 0.25, 1.0], surface.Values["tint"]);
    }

    [Fact]
    public void TwoObjectsDifferByWhatEachStates()
    {
        var view = ViewOf(
            Wearing("a", ("tint", new List<object?> { 0L, 1L, 0L, 1L })),
            Wearing("b", ("speed", 4L)));
        var keys = view.Scene.Objects.Select(one => one.Material).ToList();

        Assert.Equal(2, keys.Distinct().Count());
        var first = SurfaceOf(view, keys[0]);
        var second = SurfaceOf(view, keys[1]);
        Assert.Equal([0.0, 1.0, 0.0, 1.0], first.Values["tint"]);
        Assert.Equal([1.5], first.Values["speed"]);
        Assert.Equal([1.0, 0.5, 0.25, 1.0], second.Values["tint"]);
        Assert.Equal([4.0], second.Values["speed"]);
    }

    [Fact]
    public void AValueForANameNothingDeclaresOrTheWrongWidthIsDropped()
    {
        var view = ViewOf(Wearing("a", ("speed", new List<object?> { 1L, 2L }), ("missing", 3L)));

        var surface = SurfaceOf(view, Assert.Single(view.Scene.Objects).Material);

        Assert.Equal(["speed", "tint"], surface.Values.Keys.Order());
        Assert.Equal([1.5], surface.Values["speed"]);
    }

    [Fact]
    public void AFlagAndAPictureAreSentAsWhatTheShaderDeclares()
    {
        var view = ViewOf(Wearing("a", ("glow", true), ("noise", "art/noise.png")));

        var surface = SurfaceOf(view, Assert.Single(view.Scene.Objects).Material);

        Assert.Equal([1.0], surface.Values["glow"]);
        Assert.Equal("/p/art/noise.png", surface.Textures["noise"].Path);
        Assert.False(surface.Textures["noise"].Srgb);
    }

    [Fact]
    public void AnObjectKeepsItsKeyWhileAValueChanges()
    {
        var view = ViewOf(Wearing("a", ("speed", 2L)));
        var before = Assert.Single(view.Scene.Objects).Material;

        view.Replace(Of(Wearing("a", ("speed", 3L))));

        Assert.Equal(before, Assert.Single(view.Scene.Objects).Material);
        Assert.Equal([3.0], SurfaceOf(view, before).Values["speed"]);
        Assert.Single(view.Scene.Materials);
    }

    [Fact]
    public void AShaderNobodyHasCookedLeavesTheObjectAsItsMeshWas()
    {
        var view = Uncooked(Wearing("a"));

        Assert.Null(Assert.Single(view.Scene.Objects).Material);
        Assert.Empty(view.Scene.Materials);
    }

    [Fact]
    public void CookingItLaterPutsTheSurfaceOnTheObject()
    {
        var view = Uncooked(Wearing("a"));

        view.SetSurfaces(new Dictionary<string, CookedSurface> { [Shader] = PulseShader.Cooked });

        Assert.NotNull(Assert.Single(view.Scene.Objects).Material);
    }
}

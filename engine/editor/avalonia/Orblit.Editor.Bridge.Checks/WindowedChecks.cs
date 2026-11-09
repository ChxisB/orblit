using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Orblit.Editor.Bridge.Checks;

/// <summary>
/// The bridge against a windowed engine, which is the only kind that draws. It opens a small window
/// for a few seconds. The fixture project clears to orange and holds a box, so a frame has a known
/// background and a known subject. The order matters: the checks that look at pixels come before the
/// ones that change the scene.
/// </summary>
internal sealed class WindowedChecks : IChecks
{
    const int FramesToDraw = 20;
    const int FramesToSettle = 5;
    const int Tolerance = 4;

    readonly string _folder = EngineProject.CopyProject("view_demo");
    readonly OutputLog _log = new();
    readonly EngineHost _host;
    readonly EditorSession _editor;
    EditorCamera? _camera;
    PlanarCamera? _planar;

    public WindowedChecks()
    {
        _host = EngineHost.Create(EngineLibrary.Find(), ["--path", _folder, "--resolution", "320x240", "--log-file", _log.NewFile()]);
        _host.Start();
        _host.Pump(FramesToDraw);
        _editor = new EditorSession(_host, new RunningScene(_host));
    }

    public IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("the viewport frame reads back in the project's clear colour", FrameHasTheClearColour),
        ("the running scene opens and is read as a tree", OpenSceneIsATree),
        ("the frame shows the box in front of the clear colour", FrameShowsTheBox),
        ("the editor camera changes the frame and stays out of the outline", EditorCameraIsOutsideTheScene),
        ("an orbit of the editor camera changes the frame again", OrbitChangesTheFrame),
        ("an added entity is saved and the editor camera is not", AddedEntityIsSaved),
        ("a saved scene opens again with the added entity", SavedSceneReopens),
        ("another scene of the project opens and the editor camera still draws it", AnotherSceneOpens),
        ("a click picks the entity whose pivot lands nearest, and never one behind the camera", PickChoosesTheNearestPivot),
        ("a locked entity is never picked, and a pick under a group gives the topmost group unless it is locked", LockedAndGroupedPicks),
        ("the move handles reach out from the pivot and a drag along one moves the entity along its axis only", MoveHandlesDragAlongTheirAxis),
        ("the local handles turn with the entity and a drag along one moves it along that local axis only", LocalHandlesTurnWithTheEntity),
        ("a scale drag along a local handle stretches that axis only, and a pivot behind the camera stays put", ScaleDragStretchesOneAxis),
        ("a rotate drag turns the entity about one axis through its pivot, global or local, and a pivot behind the camera has no rings", RotateDragTurnsAboutOneAxis),
        ("the rotate gizmo has an outer ring, and a spin drag turns the entity about the view direction", OuterRingSpinsAboutTheView),
        ("a trackball drag turns the entity about the axis a drag across the view makes", TrackballTurnsLikeABall),
        ("a uniform scale drag multiplies all three scale values, with the zero escape, and a pivot behind the camera stays put", UniformScaleStretchesAllThree),
        ("a snapped move and a snapped turn land on whole steps and the next drag starts afresh", SnappedMoveAndTurnLandOnSteps),
        ("a snapped stretch of one axis or of all three lands on whole steps", SnappedStretchLandsOnSteps),
        ("a stretch that collapses an axis keeps it at zero for the rest of the drag", CollapsedStretchStaysAtZero),
        ("framing an entity puts its pivot in the middle of the frame", FrameCentresThePivot),
        ("the editor world fills the frame with sky, ground and axes and stays out of the scene", EditorWorldIsOutsideTheScene),
        ("a scene opened after the editor world is drawn before its sky", OpenedSceneComesBeforeTheSky),
        ("the selection box draws orange edges round an entity, none when nothing is selected, and stays out of the scene", SelectionBoxDrawsTheEdges),
        ("an uploaded UI frame covers the window with premultiplied blending and goes with its surface", UiSurfaceDrawsOverTheScene),
        ("the sample game UI draws its menu over the scene and leaves the rest of the frame alone", GameUiDrawsItsMenuOverTheScene),
        ("a drag step with the inspector's reads stays under 1 ms from 1,000 to 100,000 entities, and the numbers are printed", StepCostOnALargeScene),
        ("a new scene file, an instance of it saved as a reference, and a closed scene all work against the running scene", SceneFilesWorkInTheRunningScene),
        ("an orthogonal camera drops the perspective, keeps the scene's size at the target, follows a zoom and goes back", OrthogonalCameraDropsThePerspective),
        ("a collision shape is made under a mesh, owned by the scene so a save keeps it, and a mesh with no mesh or a plain entity is refused", CollisionShapesAreSaved),
        ("a collision shape under a mesh that is the scene root is saved too", CollisionUnderTheRootIsSaved),
        ("the planar camera draws the level over a flat backdrop, pans and zooms it, and goes off again", PlanarCameraDrawsTheLevelOverABackdrop),
        ("a planar pick chooses the nearest pivot of an Entity2D, and honours locks and groups", PlanarPickChoosesTheNearestPivot),
        ("a planar move drag lands the pivot where the pointer ends, at any zoom", PlanarMoveFollowsThePointerAtAnyZoom),
        ("the planar camera and its backdrop stay out of the outline and the saved scene", PlanarCameraStaysOutOfTheScene),
        ("a line the engine logs reaches the output log, and a clear drops the lines read so far", EngineLogReachesTheOutputLog),
    ];

    void FrameHasTheClearColour()
    {
        var frame = Frame();

        Assert.True(frame.Width > 0 && frame.Height > 0, $"empty frame {frame.Width}x{frame.Height}");
        var (r, g, b, a) = frame.At(0, 0);
        Assert.InRange(r, 255 - Tolerance, 255);
        Assert.InRange(g, 128 - Tolerance, 128 + Tolerance);
        Assert.InRange(b, 0, Tolerance);
        Assert.Equal(255, a);
    }

    void OpenSceneIsATree()
    {
        _editor.OpenScene("res://main.oscn");

        var root = _editor.Outline();

        Assert.NotNull(root);
        Assert.Equal("ViewDemo", root.Name);
        Assert.Equal(["Camera", "Light", "Box"], root.Children.Select(c => c.Name));
        Assert.Equal(["Camera3D", "DirectionalLight3D", "MeshInstance3D"], root.Children.Select(c => c.ClassName));
    }

    void FrameShowsTheBox()
    {
        _host.Pump(FramesToSettle);
        var frame = Frame();

        var (cr, cg, cb, _) = frame.At(frame.Width / 2, frame.Height / 2);

        Assert.False(IsClearColour(cr, cg, cb), $"the centre is still the clear colour: {cr}, {cg}, {cb}");
        Assert.True(IsClearColour(frame.At(0, 0)), "the corner lost the clear colour");
    }

    void EditorCameraIsOutsideTheScene()
    {
        var before = Frame();

        _camera = new EditorCamera(_host);
        _host.Pump(FramesToSettle);
        var after = Frame();

        Assert.NotEqual(before.Pixels, after.Pixels);
        Assert.Equal(["Camera", "Light", "Box"], _editor.Outline()!.Children.Select(c => c.Name));
    }

    void OrbitChangesTheFrame()
    {
        var before = Frame();

        _camera!.Orbit(1.0f, 0.0f);
        _host.Pump(FramesToSettle);
        var after = Frame();

        Assert.NotEqual(before.Pixels, after.Pixels);
    }

    void AddedEntityIsSaved()
    {
        var root = _editor.Outline()!;

        _editor.AddChild(root.Handle, "Marker3D", "Spawn");
        _editor.Save();

        Assert.Contains("[node name=\"Spawn\" type=\"Marker3D\" parent=\".\"", SavedScene());
        Assert.DoesNotContain("EditorCamera", SavedScene());
    }

    void SavedSceneReopens()
    {
        _editor.OpenScene("res://main.oscn");

        var names = _editor.Outline()!.Children.Select(c => c.Name);

        Assert.Equal(["Camera", "Light", "Box", "Spawn"], names);
    }

    // The second scene has no camera of its own, so a cube in the frame came through the editor camera.
    void AnotherSceneOpens()
    {
        _editor.OpenScene("res://second.oscn");
        _host.Pump(FramesToSettle);

        var root = _editor.Outline()!;
        var frame = Frame();
        var (r, g, b, _) = frame.At(frame.Width / 2, frame.Height / 2);

        Assert.Equal("Second", root.Name);
        Assert.Equal(["Light", "Cube"], root.Children.Select(c => c.Name));
        Assert.False(IsClearColour(r, g, b), $"the centre is the clear colour: {r}, {g}, {b}");
    }

    // A fresh camera starts at OrbitView.Start, so the check can ask where a point lands. The root is lifted
    // by two, so a child's world position differs from its own. Its entities sit at the root, except Spawn,
    // three to the side, and Behind, which stands on the far side of the camera.
    void PickChoosesTheNearestPivot()
    {
        _editor.OpenScene("res://main.oscn");
        _host.Pump(FramesToSettle);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        var behind = _editor.AddChild(root.Handle, "Marker3D", "Behind");
        var lift = new Vector3(0, 2, 0);
        var spawnAt = lift + new Vector3(3, 0, 0);
        var behindAt = 3 * OrbitView.Start.Eye + new Vector3(0, 6, 0);
        _editor.MoveTo(root.Handle, lift);
        _editor.MoveTo(spawn, spawnAt - lift);
        _editor.MoveTo(behind, behindAt - lift);
        _host.Pump(1);
        var spawnPoint = camera.Project(spawnAt);

        Assert.Equal(spawn, _editor.Pick(camera, spawnPoint));
        Assert.Equal(spawn, _editor.Pick(camera, spawnPoint + new Vector2(20, 0)));
        Assert.Equal(0, _editor.Pick(camera, spawnPoint + new Vector2(30, 0)));
        // The root, the light and the box are equally near the centre, so the first in outline order wins.
        Assert.Equal(root.Handle, _editor.Pick(camera, camera.Project(lift)));
        Assert.Equal(0, _editor.Pick(camera, Vector2.Zero));
        Assert.True(camera.IsBehind(behindAt));
        Assert.Equal(0, _editor.Pick(camera, camera.Project(behindAt)));
    }

    // Spawn stands three to the side of the lifted root, with a child one above it and a grandchild one above that.
    void LockedAndGroupedPicks()
    {
        _editor.OpenScene("res://main.oscn");
        _host.Pump(FramesToSettle);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        var child = _editor.AddChild(spawn, "Marker3D", "SpawnChild");
        var tip = _editor.AddChild(child, "Marker3D", "SpawnTip");
        var lift = new Vector3(0, 2, 0);
        var up = new Vector3(0, 1, 0);
        var spawnAt = lift + new Vector3(3, 0, 0);
        _editor.MoveTo(root.Handle, lift);
        _editor.MoveTo(spawn, spawnAt - lift);
        _editor.MoveTo(child, up);
        _editor.MoveTo(tip, up);
        _host.Pump(1);
        var spawnPoint = camera.Project(spawnAt);
        var childPoint = camera.Project(spawnAt + up);
        var tipPoint = camera.Project(spawnAt + 2 * up);
        Assert.Equal([spawn, child, tip], [_editor.Pick(camera, spawnPoint), _editor.Pick(camera, childPoint), _editor.Pick(camera, tipPoint)]);

        _editor.SetLocked(child, true);
        Assert.NotEqual(child, _editor.Pick(camera, childPoint));
        Assert.Equal(tip, _editor.Pick(camera, tipPoint));
        _editor.SetLocked(child, false);
        Assert.Equal(child, _editor.Pick(camera, childPoint));

        _editor.SetGrouped(spawn, true);
        Assert.Equal([spawn, spawn, spawn], [_editor.Pick(camera, spawnPoint), _editor.Pick(camera, childPoint), _editor.Pick(camera, tipPoint)]);
        _editor.SetGrouped(child, true);
        Assert.Equal(spawn, _editor.Pick(camera, tipPoint));

        _editor.SetLocked(spawn, true);
        Assert.Equal([0, 0, 0], [_editor.Pick(camera, spawnPoint), _editor.Pick(camera, childPoint), _editor.Pick(camera, tipPoint)]);
        _editor.SetLocked(spawn, false);
        _editor.SetGrouped(spawn, false);
        Assert.Equal(child, _editor.Pick(camera, childPoint));
        _editor.SetGrouped(child, false);
    }

    // Spawn is three to the side of the lifted root. Its handles start at its pivot and run a fixed length on
    // screen. A drag of one handle's whole length moves the entity along that axis and no other, and a pivot behind
    // the camera has no handles and does not move.
    void MoveHandlesDragAlongTheirAxis()
    {
        _editor.OpenScene("res://main.oscn");
        _host.Pump(FramesToSettle);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        var behind = _editor.AddChild(root.Handle, "Marker3D", "Behind");
        var lift = new Vector3(0, 2, 0);
        var spawnAt = lift + new Vector3(3, 0, 0);
        var behindAt = 3 * OrbitView.Start.Eye + new Vector3(0, 6, 0);
        _editor.MoveTo(root.Handle, lift);
        _editor.MoveTo(spawn, spawnAt - lift);
        _editor.MoveTo(behind, behindAt - lift);
        _host.Pump(1);

        var handles = _editor.HandlesOf(camera, spawn, GizmoSpace.Global)!.Value;

        Assert.True(Vector2.Distance(camera.Project(spawnAt), handles.Origin) < 0.01f, $"the handles start at {handles.Origin}");
        foreach (var axis in Enum.GetValues<GizmoAxis>())
        {
            var reach = Vector2.Distance(handles.Origin, handles.Tip(axis));
            Assert.True(MathF.Abs(reach - GizmoMath.HandleLength) < 0.01f, $"the {axis} handle is {reach} pixels long");
        }

        // Far from the camera the probe is still long enough on screen to have a direction.
        _editor.MoveTo(spawn, -10 * OrbitView.Start.Eye - lift);
        _host.Pump(1);
        var far = _editor.HandlesOf(camera, spawn, GizmoSpace.Global)!.Value;
        Assert.True(MathF.Abs(Vector2.Distance(far.Origin, far.X) - GizmoMath.HandleLength) < 0.01f, "the X handle has no length far from the camera");
        _editor.MoveTo(spawn, spawnAt - lift);
        _host.Pump(1);

        // The viewport sends each pointer move on its own, a few pixels at a time, so the drag goes the same way.
        // The pointer ends on the tip, so the pivot must end under it, and one jump must land where ten steps do.
        var x = new GizmoDirection(GizmoAxis.X, GizmoSpace.Global);
        for (var step = 0; step < 10; step++)
        {
            _editor.Drag(camera, spawn, x, Step(handles.Origin, handles.X, step, 10));
        }
        var moved = _host.Entity3DGetGlobalPosition(spawn) - spawnAt;

        Assert.True(moved.X > 0.1f, $"the drag moved Spawn {moved.X} along X");
        Assert.True(MathF.Abs(moved.Y) < 0.001f && MathF.Abs(moved.Z) < 0.001f, $"the drag moved Spawn off its axis by {moved}");
        var landed = Vector2.Distance(camera.Project(spawnAt + moved), handles.Origin);
        Assert.True(MathF.Abs(landed - GizmoMath.HandleLength) < 0.5f, $"Spawn landed {landed} pixels from its start for a drag of {GizmoMath.HandleLength}");

        _editor.MoveTo(spawn, spawnAt - lift);
        _editor.Drag(camera, spawn, x, new FrameMove(handles.Origin, handles.X));
        var jumped = _host.Entity3DGetGlobalPosition(spawn) - spawnAt;
        Assert.True(Vector3.Distance(moved, jumped) < 0.001f, $"one jump moved Spawn {jumped} and ten steps moved it {moved}");

        Assert.Null(_editor.HandlesOf(camera, behind, GizmoSpace.Global));
        _editor.Drag(camera, behind, x, new FrameMove(handles.Origin, handles.X));
        Assert.True(Vector3.Distance(behindAt, _host.Entity3DGetGlobalPosition(behind)) < 0.001f, "a pivot behind the camera moved");
    }

    // A yaw of 90 degrees turns Spawn's local X onto the global minus Z. Its local handles then start where the
    // global ones do and point elsewhere, and a drag along the local X moves it along minus Z only.
    void LocalHandlesTurnWithTheEntity()
    {
        _editor.OpenScene("res://main.oscn");
        _host.Pump(FramesToSettle);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        var lift = new Vector3(0, 2, 0);
        var spawnAt = lift + new Vector3(3, 0, 0);
        _editor.MoveTo(root.Handle, lift);
        _editor.MoveTo(spawn, spawnAt - lift);
        _editor.RotateTo(spawn, new Vector3(0, 90, 0));
        _host.Pump(1);

        var global = _editor.HandlesOf(camera, spawn, GizmoSpace.Global)!.Value;
        var local = _editor.HandlesOf(camera, spawn, GizmoSpace.Local)!.Value;

        Assert.True(Vector2.Distance(global.Origin, local.Origin) < 0.01f, "the local handles start somewhere else");
        Assert.True(Vector2.Distance(global.X, local.X) > 10f, $"the local X handle did not turn: {global.X} and {local.X}");
        Assert.True(MathF.Abs(Vector2.Distance(local.Origin, local.X) - GizmoMath.HandleLength) < 0.01f, "the local X handle is not full length");

        for (var step = 0; step < 10; step++)
        {
            _editor.Drag(camera, spawn, new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), Step(local.Origin, local.X, step, 10));
        }
        var moved = _host.Entity3DGetGlobalPosition(spawn) - spawnAt;

        Assert.True(moved.Z < -0.1f, $"the drag moved Spawn {moved.Z} along Z");
        Assert.True(MathF.Abs(moved.X) < 0.001f && MathF.Abs(moved.Y) < 0.001f, $"the drag moved Spawn off its axis by {moved}");
    }

    // The scale handles are the local ones. A drag of a handle's whole length doubles that scale value, and
    // dragging back undoes it. The place, the turn and the other two values stay. A pivot behind the camera has
    // no handles and does not change.
    void ScaleDragStretchesOneAxis()
    {
        _editor.OpenScene("res://main.oscn");
        _host.Pump(FramesToSettle);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        var behind = _editor.AddChild(root.Handle, "Marker3D", "Behind");
        var lift = new Vector3(0, 2, 0);
        var spawnAt = lift + new Vector3(3, 0, 0);
        _editor.MoveTo(root.Handle, lift);
        _editor.MoveTo(spawn, spawnAt - lift);
        _editor.MoveTo(behind, 3 * OrbitView.Start.Eye + new Vector3(0, 6, 0) - lift);
        _editor.RotateTo(spawn, new Vector3(0, 90, 0));
        _editor.ScaleTo(spawn, new Vector3(1, 2, 3));
        _host.Pump(1);
        var local = _editor.HandlesOf(camera, spawn, GizmoSpace.Local)!.Value;
        var localX = GizmoMath.Axis(new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), _host.Entity3DGetGlobalTransform(spawn));

        // The pointer goes from one unit to two units out along the axis in ten steps, so the value doubles.
        for (var step = 0; step < 10; step++)
        {
            _editor.ScaleDrag(camera, spawn, GizmoAxis.X, Slide(camera, spawnAt, localX, 1f + step * 0.1f, 1f + (step + 1) * 0.1f));
        }
        var scale = _editor.ScaleOf(spawn);

        Assert.True(MathF.Abs(scale.X - 2f) < 0.001f, $"the X scale is {scale.X} after the pointer went twice as far out");
        Assert.True(MathF.Abs(scale.Y - 2f) < 0.001f && MathF.Abs(scale.Z - 3f) < 0.001f, $"the drag changed the other axes: {scale}");
        Assert.True(Vector3.Distance(spawnAt, _host.Entity3DGetGlobalPosition(spawn)) < 0.001f, "the drag moved Spawn");
        Assert.True(Vector3.Distance(new Vector3(0, 90, 0), _editor.RotationOf(spawn)) < 0.01f, $"the drag turned Spawn to {_editor.RotationOf(spawn)}");

        _editor.ScaleDrag(camera, spawn, GizmoAxis.X, Slide(camera, spawnAt, localX, 2f, 1f));
        Assert.True(MathF.Abs(_editor.ScaleOf(spawn).X - 1f) < 0.001f, $"the X scale is {_editor.ScaleOf(spawn).X} after the drag back");

        // One jump gives what ten steps give, and the engine's own handle tip scales too.
        _editor.ScaleDrag(camera, spawn, GizmoAxis.X, Slide(camera, spawnAt, localX, 1f, 3f));
        Assert.True(MathF.Abs(_editor.ScaleOf(spawn).X - 3f) < 0.001f, $"the X scale is {_editor.ScaleOf(spawn).X} after one jump to three times");
        var jumpedTo = _editor.ScaleOf(spawn).X;
        _editor.ScaleDrag(camera, spawn, GizmoAxis.X, new FrameMove(local.Origin, local.X));
        Assert.True(_editor.ScaleOf(spawn).X == jumpedTo, $"a drag from the pivot changed the X scale from {jumpedTo} to {_editor.ScaleOf(spawn).X}");

        // Each axis follows its own handle, so a drag along the Y handle stretches Y and nothing else.
        var localY = GizmoMath.Axis(new GizmoDirection(GizmoAxis.Y, GizmoSpace.Local), _host.Entity3DGetGlobalTransform(spawn));
        _editor.ScaleTo(spawn, new Vector3(1, 2, 3));
        for (var step = 0; step < 10; step++)
        {
            _editor.ScaleDrag(camera, spawn, GizmoAxis.Y, Slide(camera, spawnAt, localY, 1f + step * 0.1f, 1f + (step + 1) * 0.1f));
        }
        var stretched = _editor.ScaleOf(spawn);
        Assert.True(MathF.Abs(stretched.Y - 4f) < 0.001f, $"the Y scale is {stretched.Y} after the pointer went twice as far out");
        Assert.True(MathF.Abs(stretched.X - 1f) < 0.001f && MathF.Abs(stretched.Z - 3f) < 0.001f, $"the Y drag changed the other axes: {stretched}");

        // An axis already at zero leaves it by a step. It has no direction of its own, so its handle runs along the global axis.
        _editor.ScaleTo(spawn, new Vector3(0, 2, 3));
        var flatX = GizmoMath.Axis(new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), _host.Entity3DGetGlobalTransform(spawn));
        Assert.True(Vector3.Distance(flatX, Vector3.UnitX) < 0.001f, $"the zero X axis points along {flatX}");
        _editor.ScaleDrag(camera, spawn, GizmoAxis.X, Slide(camera, spawnAt, flatX, 1f, 2f));
        var grown = _editor.ScaleOf(spawn);
        Assert.True(MathF.Abs(grown.X - 0.1f) < 0.001f && MathF.Abs(grown.Y - 2f) < 0.001f, $"the zero X scale grew to {grown}");

        var before = _editor.ScaleOf(behind);
        Assert.Null(_editor.HandlesOf(camera, behind, GizmoSpace.Local));
        _editor.ScaleDrag(camera, behind, GizmoAxis.X, new FrameMove(local.Origin, local.X));
        Assert.Equal(before, _editor.ScaleOf(behind));
    }

    // The rings are about one handle length wide, and more on the near side with the pivot off the middle of the view. A turn about the global Y axis takes the basis round it, and a
    // turn about a local axis takes the other two columns round the entity's own column. The place and the
    // lengths of the columns stay, so the scale does. A pivot behind the camera has no rings.
    void RotateDragTurnsAboutOneAxis()
    {
        _editor.OpenScene("res://main.oscn");
        _host.Pump(FramesToSettle);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        var behind = _editor.AddChild(root.Handle, "Marker3D", "Behind");
        var lift = new Vector3(0, 2, 0);
        var spawnAt = lift + new Vector3(3, 0, 0);
        _editor.MoveTo(root.Handle, lift);
        _editor.MoveTo(spawn, spawnAt - lift);
        _editor.MoveTo(behind, 3 * OrbitView.Start.Eye + new Vector3(0, 6, 0) - lift);
        _editor.RotateTo(spawn, Vector3.Zero);
        _editor.ScaleTo(spawn, new Vector3(1, 2, 3));
        _host.Pump(1);
        var global = _editor.RingsOf(camera, spawn, GizmoSpace.Global)!.Value;

        foreach (var axis in new[] { GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z })
        {
            Assert.True(global.Of(axis).Length == GizmoMath.RingPoints, $"the {axis} ring has {global.Of(axis).Length} points");
        }
        var reach = new[] { GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z }
            .SelectMany(axis => global.Of(axis).Where((_, i) => i % 3 == 0))
            .Average(point => Vector2.Distance(point, global.Origin));
        Assert.True(MathF.Abs(reach - GizmoMath.HandleLength) < 1f, $"the rings reach {reach} pixels on average");

        // The pointer moves along the ring in the plane across Y. Ten steps of 9 degrees turn as far as one of 90.
        var y = new GizmoDirection(GizmoAxis.Y, GizmoSpace.Global);
        Vector2 OnRing(float degrees) => camera.Project(spawnAt + new Vector3(MathF.Cos(degrees * MathF.PI / 180f), 0, -MathF.Sin(degrees * MathF.PI / 180f)));
        for (var step = 0; step < 10; step++)
        {
            _editor.RotateDrag(camera, spawn, y, new FrameMove(OnRing(step * 9f), OnRing((step + 1) * 9f)));
        }
        var stepped = _host.Entity3DGetGlobalTransform(spawn);
        Assert.True(Vector3.Distance(new Vector3(0, 0, -1), Column(stepped, 0)) < 0.01f, $"ten 9 degree drags gave column 0 {Column(stepped, 0)}");
        _editor.RotateTo(spawn, Vector3.Zero);
        _editor.RotateDrag(camera, spawn, y, new FrameMove(OnRing(0f), OnRing(90f)));
        Assert.True(Vector3.Distance(Column(stepped, 0), Column(_host.Entity3DGetGlobalTransform(spawn), 0)) < 0.01f, "one 90 degree drag turned differently");
        _editor.RotateTo(spawn, Vector3.Zero);

        _editor.RotateBy(spawn, y, MathF.PI / 3);
        var turned = _host.Entity3DGetGlobalTransform(spawn);
        Assert.True(Vector3.Distance(new Vector3(0.5f, 0, -0.866f), Column(turned, 0)) < 0.01f, $"the global Y turn gave column 0 {Column(turned, 0)}");
        Assert.True(Vector3.Distance(new Vector3(0, 2, 0), Column(turned, 1)) < 0.01f, $"the global Y turn gave column 1 {Column(turned, 1)}");
        Assert.True(Vector3.Distance(new Vector3(2.598f, 0, 1.5f), Column(turned, 2)) < 0.01f, $"the global Y turn gave column 2 {Column(turned, 2)}");
        Assert.True(Vector3.Distance(spawnAt, _host.Entity3DGetGlobalPosition(spawn)) < 0.001f, "the turn moved Spawn");
        Assert.True(Vector3.Distance(new Vector3(1, 2, 3), _editor.ScaleOf(spawn)) < 0.001f, $"the turn changed the scale to {_editor.ScaleOf(spawn)}");

        // The local X axis is column 0 now, so column 1 turns about it and column 0 stays.
        _editor.RotateBy(spawn, new GizmoDirection(GizmoAxis.X, GizmoSpace.Local), MathF.PI / 2);
        var local = _host.Entity3DGetGlobalTransform(spawn);
        Assert.True(Vector3.Distance(Column(turned, 0), Column(local, 0)) < 0.01f, $"the local X turn moved column 0 to {Column(local, 0)}");
        Assert.True(Vector3.Distance(new Vector3(1.732f, 0, 1f), Column(local, 1)) < 0.01f, $"the local X turn gave column 1 {Column(local, 1)}");
        Assert.True(Vector3.Distance(spawnAt, _host.Entity3DGetGlobalPosition(spawn)) < 0.001f, "the local turn moved Spawn");

        var around = _editor.RingsOf(camera, spawn, GizmoSpace.Local)!.Value;
        Assert.True(around.X.Zip(_editor.RingsOf(camera, spawn, GizmoSpace.Global)!.Value.X).Any(pair => Vector2.Distance(pair.First, pair.Second) > 1f), "the local ring is the global one");

        Assert.Null(_editor.RingsOf(camera, behind, GizmoSpace.Global));
    }

    // The outer ring lies across the view and reaches a little past the three rings. A spin of a quarter turn
    // clockwise on screen keeps the view direction where it is and takes a direction across it to the cross of the two.
    void OuterRingSpinsAboutTheView()
    {
        var (camera, spawn, at, behind) = PlaceSpawn(Vector3.One);
        var rings = _editor.RingsOf(camera, spawn, GizmoSpace.Global)!.Value;

        Assert.True(rings.Outer.Length == GizmoMath.RingPoints, $"the outer ring has {rings.Outer.Length} points");
        var inner = Reach(rings.X.Concat(rings.Y).Concat(rings.Z), rings.Origin);
        var outer = Reach(rings.Outer, rings.Origin);
        // The outer ring faces the view and sits 1.15 times out, so it reaches about 1.28 times the mean reach of the foreshortened axis rings.
        Assert.True(outer > inner * 1.2f && outer < inner * 1.35f, $"the outer ring reaches {outer} pixels and the rings {inner}");
        var distances = rings.Outer.Select(point => Vector2.Distance(point, rings.Origin)).ToArray();
        Assert.True(distances.Max() < distances.Min() * 1.15f, $"the outer ring is not a circle: it runs from {distances.Min()} to {distances.Max()} pixels out");

        var origin = camera.Project(at);
        _editor.SpinDrag(camera, spawn, new FrameMove(origin + new Vector2(80, 0), origin + new Vector2(0, 80)));
        var view = camera.ViewAxis;
        var across = Vector3.Normalize(Vector3.Cross(view, Vector3.UnitY));
        var turned = _host.Entity3DGetGlobalTransform(spawn);
        Assert.True(Vector3.Distance(view, Apply(turned, view)) < 0.01f, $"the spin moved the view direction to {Apply(turned, view)}");
        Assert.True(Vector3.Distance(Vector3.Cross(view, across), Apply(turned, across)) < 0.01f, $"the spin took a direction across the view to {Apply(turned, across)}");
        Assert.True(Vector3.Distance(at, _host.Entity3DGetGlobalPosition(spawn)) < 0.001f, "the spin moved Spawn");

        var before = _editor.RotationOf(behind);
        Assert.Null(_editor.RingsOf(camera, behind, GizmoSpace.Global));
        _editor.SpinDrag(camera, behind, new FrameMove(origin + new Vector2(80, 0), origin + new Vector2(0, 80)));
        Assert.True(Vector3.Distance(before, _editor.RotationOf(behind)) < 0.001f, "a spin turned an entity behind the camera");
    }

    // A drag of 40 pixels turns half a radian and takes the front of the ball the way the pointer went. A drag along the
    // screen turns about the up direction of the view and a drag down it about the right one. The axis stays put.
    void TrackballTurnsLikeABall()
    {
        var (camera, spawn, _, _) = PlaceSpawn(Vector3.One);
        var view = camera.ViewAxis;
        var right = Vector3.Normalize(Vector3.Cross(view, Vector3.UnitY));
        var up = Vector3.Cross(right, view);
        var from = new Vector2(100, 100);

        _editor.TrackballDrag(camera, spawn, new FrameMove(from, from + new Vector2(40, 0)));
        var along = _host.Entity3DGetGlobalTransform(spawn);
        Assert.True(Vector3.Distance(up, Apply(along, up)) < 0.01f, $"a drag along the screen moved the up direction to {Apply(along, up)}");
        Assert.True(MathF.Abs(Vector3.Dot(right, Apply(along, right)) - MathF.Cos(0.5f)) < 0.01f, $"a drag of 40 pixels turned the right direction to {Apply(along, right)}");
        Assert.True(MathF.Abs(Vector3.Dot(right, Apply(along, -view)) - MathF.Sin(0.5f)) < 0.01f, $"a drag along the screen took the front of the ball to {Apply(along, -view)}");

        _editor.RotateTo(spawn, Vector3.Zero);
        _editor.TrackballDrag(camera, spawn, new FrameMove(from, from + new Vector2(0, 40)));
        var down = _host.Entity3DGetGlobalTransform(spawn);
        Assert.True(Vector3.Distance(right, Apply(down, right)) < 0.01f, $"a drag down the screen moved the right direction to {Apply(down, right)}");
        Assert.True(MathF.Abs(Vector3.Dot(up, Apply(down, up)) - MathF.Cos(0.5f)) < 0.01f, $"a drag of 40 pixels turned the up direction to {Apply(down, up)}");
        Assert.True(MathF.Abs(Vector3.Dot(up, Apply(down, -view)) + MathF.Sin(0.5f)) < 0.01f, $"a drag down the screen took the front of the ball to {Apply(down, -view)}");

        _editor.RotateTo(spawn, Vector3.Zero);
        _editor.TrackballDrag(camera, spawn, new FrameMove(from, from));
        Assert.True(Vector3.Distance(Vector3.Zero, _editor.RotationOf(spawn)) < 0.001f, "a drag of no length turned Spawn");
    }

    // The factor is the pointer's distance from the pivot plus one handle length, over the same before the move. So 20
    // pixels out to 120 pixels out doubles. An axis at zero leaves it by a step, and a pivot behind the camera stays put.
    void UniformScaleStretchesAllThree()
    {
        var (camera, spawn, at, behind) = PlaceSpawn(new Vector3(1, 2, 3));
        var origin = camera.Project(at);
        var doubled = new FrameMove(origin + new Vector2(20, 0), origin + new Vector2(120, 0));

        _editor.ScaleAllDrag(camera, spawn, doubled);
        Assert.True(Vector3.Distance(new Vector3(2, 4, 6), _editor.ScaleOf(spawn)) < 0.001f, $"the scale is {_editor.ScaleOf(spawn)} after the pointer went out");
        Assert.True(Vector3.Distance(at, _host.Entity3DGetGlobalPosition(spawn)) < 0.001f, "the stretch moved Spawn");
        Assert.True(Vector3.Distance(Vector3.Zero, _editor.RotationOf(spawn)) < 0.001f, $"the stretch turned Spawn to {_editor.RotationOf(spawn)}");

        _editor.EndDrag();
        _editor.ScaleAllDrag(camera, spawn, new FrameMove(doubled.To, doubled.From));
        Assert.True(Vector3.Distance(new Vector3(1, 2, 3), _editor.ScaleOf(spawn)) < 0.001f, $"the scale is {_editor.ScaleOf(spawn)} after the pointer came back");

        _editor.EndDrag();
        _editor.ScaleTo(spawn, new Vector3(0, 2, 3));
        _editor.ScaleAllDrag(camera, spawn, doubled);
        Assert.True(Vector3.Distance(new Vector3(0.1f, 4, 6), _editor.ScaleOf(spawn)) < 0.001f, $"the zero X scale grew to {_editor.ScaleOf(spawn)}");

        var before = _editor.ScaleOf(behind);
        _editor.ScaleAllDrag(camera, behind, doubled);
        Assert.Equal(before, _editor.ScaleOf(behind));
    }

    // A pointer 1.3 units out lands on 1.5 with a step of 0.5, whether it comes in one move or ten, and a drag with no
    // snap lands on 1.3. A turn or a spin of 40 degrees lands on 45 with a step of 15. A drag that ends starts the next from nothing.
    void SnappedMoveAndTurnLandOnSteps()
    {
        var (camera, spawn, at, _) = PlaceSpawn(Vector3.One);
        var x = new GizmoDirection(GizmoAxis.X, GizmoSpace.Global);
        void Out(float units)
        {
            var pivot = _host.Entity3DGetGlobalPosition(spawn);
            for (var step = 0; step < 10; step++)
            {
                _editor.Drag(camera, spawn, x, Slide(camera, pivot, Vector3.UnitX, step * units / 10, (step + 1) * units / 10));
            }
        }

        Out(1.3f);
        Assert.True(Vector3.Distance(at + new Vector3(1.3f, 0, 0), _host.Entity3DGetGlobalPosition(spawn)) < 0.001f, $"an unsnapped drag landed on {_host.Entity3DGetGlobalPosition(spawn)}");
        _editor.EndDrag();
        _editor.MoveTo(spawn, new Vector3(3, 0, 0));
        _editor.Snap = new GizmoSnap(0.5f, 15f, 0.5f);

        Out(1.3f);
        Assert.True(Vector3.Distance(at + new Vector3(1.5f, 0, 0), _host.Entity3DGetGlobalPosition(spawn)) < 0.001f, $"a snapped drag landed on {_host.Entity3DGetGlobalPosition(spawn)}");
        _editor.EndDrag();
        Out(0.3f);
        Assert.True(Vector3.Distance(at + new Vector3(2f, 0, 0), _host.Entity3DGetGlobalPosition(spawn)) < 0.001f, $"the drag after a release landed on {_host.Entity3DGetGlobalPosition(spawn)}");
        _editor.EndDrag();

        var pivotNow = _host.Entity3DGetGlobalPosition(spawn);
        Vector2 OnRing(float degrees) => camera.Project(pivotNow + new Vector3(MathF.Cos(degrees * MathF.PI / 180f), 0, -MathF.Sin(degrees * MathF.PI / 180f)));
        for (var step = 0; step < 10; step++)
        {
            _editor.RotateDrag(camera, spawn, new GizmoDirection(GizmoAxis.Y, GizmoSpace.Global), new FrameMove(OnRing(step * 4f), OnRing((step + 1) * 4f)));
        }
        var turned = Vector3.Normalize(Column(_host.Entity3DGetGlobalTransform(spawn), 0));
        Assert.True(Vector3.Distance(new Vector3(0.7071f, 0, -0.7071f), turned) < 0.01f, $"a snapped turn of 40 degrees gave column 0 {turned}");
        _editor.EndDrag();

        // A spin of 40 degrees clockwise on screen lands on 45 too.
        _editor.RotateTo(spawn, Vector3.Zero);
        var origin = camera.Project(pivotNow);
        Vector2 Round(float degrees) => origin + 80 * new Vector2(MathF.Cos(degrees * MathF.PI / 180f), MathF.Sin(degrees * MathF.PI / 180f));
        for (var step = 0; step < 10; step++)
        {
            _editor.SpinDrag(camera, spawn, new FrameMove(Round(step * 4f), Round((step + 1) * 4f)));
        }
        var across = Vector3.Normalize(Vector3.Cross(camera.ViewAxis, Vector3.UnitY));
        var spun = Vector3.Dot(across, Apply(_host.Entity3DGetGlobalTransform(spawn), across));
        Assert.True(MathF.Abs(spun - MathF.Cos(MathF.PI / 4)) < 0.01f, $"a snapped spin of 40 degrees turned a direction across the view to {spun} of itself");
        _editor.Snap = null;
    }

    // A pointer 1.37 times as far out stretches an axis by 1.5 with a step of 0.5. The same pointer move from the pivot
    // stretches all three by 1.5. Only the stretch is held to the step, so the scale of 2 goes to 3 and not to a whole step.
    void SnappedStretchLandsOnSteps()
    {
        var (camera, spawn, at, _) = PlaceSpawn(new Vector3(2, 2, 2));
        _editor.Snap = new GizmoSnap(0.25f, 15f, 0.5f);

        for (var step = 0; step < 10; step++)
        {
            _editor.ScaleDrag(camera, spawn, GizmoAxis.X, Slide(camera, at, Vector3.UnitX, 1f + step * 0.037f, 1f + (step + 1) * 0.037f));
        }
        Assert.True(Vector3.Distance(new Vector3(3, 2, 2), _editor.ScaleOf(spawn)) < 0.001f, $"a snapped stretch of one axis gave {_editor.ScaleOf(spawn)}");
        _editor.EndDrag();

        _editor.ScaleTo(spawn, new Vector3(1, 2, 3));
        var origin = camera.Project(at);
        _editor.ScaleAllDrag(camera, spawn, new FrameMove(origin + new Vector2(20, 0), origin + new Vector2(57, 0)));
        Assert.True(Vector3.Distance(new Vector3(1.5f, 3, 4.5f), _editor.ScaleOf(spawn)) < 0.001f, $"a snapped stretch of all three gave {_editor.ScaleOf(spawn)}");
        _editor.Snap = null;
    }

    void CollapsedStretchStaysAtZero()
    {
        var (camera, spawn, at, _) = PlaceSpawn(new Vector3(2, 2, 2));
        _editor.Snap = new GizmoSnap(0.25f, 15f, 1f);

        _editor.ScaleDrag(camera, spawn, GizmoAxis.X, Slide(camera, at, Vector3.UnitX, 1f, 0f));
        Assert.True(_editor.ScaleOf(spawn).X == 0f, $"a stretch to the pivot gave {_editor.ScaleOf(spawn)}");
        _editor.ScaleDrag(camera, spawn, GizmoAxis.X, Slide(camera, at, Vector3.UnitX, 1f, 1.5f));
        var held = _editor.ScaleOf(spawn);
        Assert.True(held.X == 0f && held.Y == 2f && held.Z == 2f, $"a drag on from a collapsed axis gave {held}");

        _editor.EndDrag();
        _editor.ScaleDrag(camera, spawn, GizmoAxis.X, Slide(camera, at, Vector3.UnitX, 1f, 2f));
        Assert.True(_editor.ScaleOf(spawn).X > 0f, "the next drag did not grow the collapsed axis");
        _editor.Snap = null;
    }

    // A fresh Spawn three units out and two up, not turned, at a scale, with no snap. Back is behind the camera.
    (EditorCamera Camera, nint Spawn, Vector3 At, nint Behind) PlaceSpawn(Vector3 scale)
    {
        _editor.Snap = null;
        _editor.EndDrag();
        _editor.OpenScene("res://main.oscn");
        _host.Pump(FramesToSettle);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        var behind = _editor.AddChild(root.Handle, "Marker3D", "Back");
        var lift = new Vector3(0, 2, 0);
        var at = lift + new Vector3(3, 0, 0);
        _editor.MoveTo(root.Handle, lift);
        _editor.MoveTo(spawn, at - lift);
        _editor.MoveTo(behind, 3 * OrbitView.Start.Eye + new Vector3(0, 6, 0) - lift);
        _editor.RotateTo(spawn, Vector3.Zero);
        _editor.ScaleTo(spawn, scale);
        _host.Pump(1);
        return (camera, spawn, at, behind);
    }

    static float Reach(IEnumerable<Vector2> points, Vector2 origin) => points.Average(point => Vector2.Distance(point, origin));

    // Where a basis takes a direction.
    static Vector3 Apply(Transform3D place, Vector3 direction) =>
        Column(place, 0) * direction.X + Column(place, 1) * direction.Y + Column(place, 2) * direction.Z;

    // The pointer's move from fraction step of a segment to the next one of n.
    static FrameMove Step(Vector2 from, Vector2 to, int step, int steps) =>
        new(Vector2.Lerp(from, to, step / (float)steps), Vector2.Lerp(from, to, (step + 1) / (float)steps));

    // The pointer's move from one distance along an axis through a pivot to another, as the camera sees it.
    static FrameMove Slide(EditorCamera camera, Vector3 pivot, Vector3 axis, float from, float to) =>
        new(camera.Project(pivot + axis * from), camera.Project(pivot + axis * to));

    static Vector3 Column(Transform3D place, int column) => column switch
    {
        0 => new Vector3(place.Row0.X, place.Row1.X, place.Row2.X),
        1 => new Vector3(place.Row0.Y, place.Row1.Y, place.Row2.Y),
        _ => new Vector3(place.Row0.Z, place.Row1.Z, place.Row2.Z),
    };

    void FrameCentresThePivot()
    {
        _editor.OpenScene("res://main.oscn");
        _host.Pump(FramesToSettle);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        var lift = new Vector3(0, 2, 0);
        var spawnAt = lift + new Vector3(3, 0, 0);
        _editor.MoveTo(root.Handle, lift);
        _editor.MoveTo(spawn, spawnAt - lift);
        _host.Pump(1);
        var frame = Frame();
        var middle = new Vector2(frame.Width / 2f, frame.Height / 2f);
        Assert.True(Vector2.Distance(camera.Project(spawnAt), middle) > 50f, "the start view already centres Spawn");

        camera.Frame(spawn);
        _host.Pump(1);

        var off = Vector2.Distance(camera.Project(spawnAt), middle);
        Assert.True(off < 1.5f, $"Spawn is {off} pixels from the middle");
    }

    // Last, because the sky takes the clear colour away from the checks above. The fresh camera starts 8 units
    // out, high and to the side, looking at the origin, so the top of the frame is sky and the bottom is ground.
    void EditorWorldIsOutsideTheScene()
    {
        EditorWorld.Build(_host);
        var camera = new EditorCamera(_host);
        _host.Pump(FramesToSettle);
        var frame = Frame();
        // The corners are off the vertical axis, which runs down the middle.
        var top = frame.At(4, 0);
        var bottom = frame.At(4, frame.Height - 1);
        var onX = camera.Project(new Vector3(2, 0, 0));
        var axis = Enumerable.Range(-2, 5).Select(dy => frame.At((int)onX.X, (int)onX.Y + dy)).MaxBy(pixel => pixel.R - pixel.G);

        Assert.False(IsClearColour(top), $"the top is still the clear colour: {top}");
        Assert.True(top.B >= top.R, $"the top is not sky: {top}");
        Assert.True(bottom.R > bottom.B, $"the bottom is not ground: {bottom}");
        Assert.True(axis.R > 200 && axis.G < 100, $"the X axis is not red at {onX}: {axis}");
        Assert.Equal(["Camera", "Light", "Box", "Spawn"], _editor.Outline()!.Children.Select(c => c.Name));
        _editor.Save();
        Assert.DoesNotContain("EditorEnvironment", SavedScene());
        Assert.DoesNotContain("EditorGrid", SavedScene());
    }

    // Opens the first scene again at the end, because the check after this one reads it.
    void OpenedSceneComesBeforeTheSky()
    {
        _editor.OpenScene("res://second.oscn");
        var behind = RootNames();
        EditorWorld.PutSkyLast(_host);
        var ahead = RootNames();
        _editor.OpenScene("res://main.oscn");

        Assert.True(behind.IndexOf("Second") > behind.IndexOf("EditorEnvironment"), $"the new scene is not after the sky: {string.Join(", ", behind)}");
        Assert.Equal("EditorEnvironment", ahead[^1]);
        Assert.True(ahead.IndexOf("Second") < ahead.IndexOf("EditorEnvironment"), $"the scene is not before the sky: {string.Join(", ", ahead)}");
    }

    // The names of what sits under the stage root, in tree order.
    List<string> RootNames()
    {
        var root = EditorWorld.StageRoot(_host);
        return [.. Enumerable.Range(0, (int)_host.EntityGetChildCount(root, false)).Select(i => _host.EntityGetName(_host.EntityGetChild(root, i, false)))];
    }

    // Last, because it adds boxes under the stage root and moves the Box. The sky, the ground, the grid and the
    // axes have no orange in them, so every orange pixel is a box edge. The scene's camera is not a visual
    // instance and has no bounds, so it gets the small stand-in box.
    void SelectionBoxDrawsTheEdges()
    {
        var box = new SelectionBox(_host);
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var mesh = root.Children.Single(c => c.Name == "Box").Handle;
        var eye = root.Children.Single(c => c.Name == "Camera").Handle;
        _editor.MoveTo(mesh, new Vector3(2, 0, 0));
        _host.Pump(FramesToSettle);
        var none = Orange(Frame());

        box.Show(mesh);
        _host.Pump(FramesToSettle);
        var round = Orange(Frame());
        box.Show(eye);
        _host.Pump(FramesToSettle);
        var small = Orange(Frame());
        box.Show(0);
        _host.Pump(FramesToSettle);
        var hidden = Orange(Frame());

        Assert.Equal(0, none.Count);
        Assert.Equal(0, hidden.Count);
        Assert.True(Vector2.Distance(round.Centre, camera.Project(Origin(mesh))) < 12, $"the box is at {round.Centre}, the Box at {camera.Project(Origin(mesh))}");
        Assert.True(round.Width > 20, $"the box is {round.Width} pixels wide");
        Assert.True(small.Count > 20, $"the stand-in box drew {small.Count} pixels");
        Assert.True(Vector2.Distance(small.Centre, camera.Project(Origin(eye))) < 8, $"the stand-in is at {small.Centre}, the camera at {camera.Project(Origin(eye))}");
        Assert.Equal(["Camera", "Light", "Box", "Spawn"], _editor.Outline()!.Children.Select(c => c.Name));
        _editor.Save();
        Assert.DoesNotContain("EditorSelection", SavedScene());
    }

    // Blue at half alpha, premultiplied, so the channels are 0, 0, 128 and the alpha is 128. The frame then reads
    // as that plus the old pixel at half strength. A straight blend would put half the blue in, which is 64.
    void UiSurfaceDrawsOverTheScene()
    {
        var before = Frame();
        // Half the window, as a frame is while the window is being resized. The rect stretches it over the window.
        var (width, height) = (before.Width / 2, before.Height / 2);
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 2] = 128;
            pixels[i + 3] = 128;
        }
        using var surface = new EngineUiSurface(_host);

        surface.Upload(pixels, width, height);
        _host.Pump(FramesToSettle);
        var covered = Frame();

        foreach (var (x, y) in new[] { (0, 0), (before.Width - 1, before.Height - 1) })
        {
            var (r, g, b, _) = before.At(x, y);
            var (cr, cg, cb, _) = covered.At(x, y);
            Assert.InRange(cr, r / 2 - Tolerance, r / 2 + Tolerance);
            Assert.InRange(cg, g / 2 - Tolerance, g / 2 + Tolerance);
            Assert.InRange(cb, 128 + b / 2 - Tolerance, 128 + b / 2 + Tolerance);
        }

        surface.Dispose();
        _host.Pump(FramesToSettle);
        Assert.Equal(before.Pixels, Frame().Pixels);
    }

    // The menu is a translucent panel in the middle of an otherwise clear frame. Over the old pixel it leaves the
    // share of that pixel the panel's alpha does not cover. A corner has alpha 0, so it must read as before.
    void GameUiDrawsItsMenuOverTheScene()
    {
        var before = Frame();
        var ui = GameUiFrame.Draw(before.Width, before.Height);
        using var surface = new EngineUiSurface(_host);

        surface.Upload(ui.Pixels, ui.Width, ui.Height);
        _host.Pump(FramesToSettle);
        var covered = Frame();

        var (x, y) = (before.Width / 2 - 110 + 12, before.Height / 2);
        var (sr, sg, sb, sa) = ui.At(x, y);
        Assert.InRange(sa, 0xC8, 0xCC);
        var (r, g, b, _) = before.At(x, y);
        var (cr, cg, cb, _) = covered.At(x, y);
        Assert.InRange(cr, sr + r * (255 - sa) / 255 - Tolerance, sr + r * (255 - sa) / 255 + Tolerance);
        Assert.InRange(cg, sg + g * (255 - sa) / 255 - Tolerance, sg + g * (255 - sa) / 255 + Tolerance);
        Assert.InRange(cb, sb + b * (255 - sa) / 255 - Tolerance, sb + b * (255 - sa) / 255 + Tolerance);
        Assert.NotEqual(before.At(x, y), covered.At(x, y));
        var last = (before.Width - 1, before.Height - 1);
        Assert.Equal(before.At(last.Item1, last.Item2), covered.At(last.Item1, last.Item2));
    }

    Vector3 Origin(nint entity) => _host.Entity3DGetGlobalTransform(entity).Origin;

    // How many orange pixels the frame has, where their middle is and how wide they spread.
    static (int Count, Vector2 Centre, int Width) Orange(Snapshot frame)
    {
        var hits = Enumerable.Range(0, frame.Width * frame.Height)
            .Where(i => frame.Pixels[i * 4] > 200 && frame.Pixels[i * 4 + 1] is > 90 and < 170 && frame.Pixels[i * 4 + 2] < 60)
            .Select(i => (X: i % frame.Width, Y: i / frame.Width))
            .ToList();
        return hits.Count == 0
            ? (0, Vector2.Zero, 0)
            : (hits.Count, new Vector2((float)hits.Average(h => h.X), (float)hits.Average(h => h.Y)), hits.Max(h => h.X) - hits.Min(h => h.X));
    }

    // One pointer move on a flat parent of many entities: the drag, the four reads the inspector makes after it and the
    // selection box moving to the new place.
    // The viewport also asks for the move handles and the rings each tick, and the engine draws a frame. This is last
    // because it fills the scene. A step measures about 5 microseconds at every size. The bound is 1 ms, well under the
    // 8 ms the plan allows, because one engine call per entity would cost about 5 ms at 10,000 and must fail it.
    void StepCostOnALargeScene()
    {
        _editor.OpenScene("res://main.oscn");
        var camera = new EditorCamera(_host);
        var root = _editor.Outline()!;
        var spawn = root.Children.Single(c => c.Name == "Spawn").Handle;
        _editor.MoveTo(root.Handle, new Vector3(0, 2, 0));
        _editor.MoveTo(spawn, new Vector3(3, 0, 0));
        _host.Pump(1);
        var handles = _editor.HandlesOf(camera, spawn, GizmoSpace.Global)!.Value;
        var x = new GizmoDirection(GizmoAxis.X, GizmoSpace.Global);
        // Each step goes a tenth of the handle forward or back, so the pivot stays in view for the whole run.
        var tenth = handles.Origin + (handles.X - handles.Origin) / 10;
        var box = new SelectionBox(_host);
        var step = 0;
        var added = 0;
        FrameMove Wiggle() => step++ % 2 == 0 ? new FrameMove(handles.Origin, tenth) : new FrameMove(tenth, handles.Origin);
        foreach (var count in new[] { 1_000, 10_000, 100_000 })
        {
            for (; added < count; added++)
            {
                _editor.AddChild(root.Handle, "Marker3D", $"Bench{added}");
            }

            var before = _editor.PositionOf(spawn);
            _editor.Drag(camera, spawn, x, Wiggle());
            Assert.NotEqual(before, _editor.PositionOf(spawn));
            var drag = Times(50, () => _editor.Drag(camera, spawn, x, Wiggle()));
            var inspected = Times(50, () =>
            {
                _editor.Drag(camera, spawn, x, Wiggle());
                _ = (_editor.IsEntity3D(spawn), _editor.PositionOf(spawn), _editor.RotationOf(spawn), _editor.ScaleOf(spawn));
                box.Show(spawn);
            });
            Assert.NotNull(_editor.RingsOf(camera, spawn, GizmoSpace.Global));
            var tick = Times(50, () => _ = (_editor.HandlesOf(camera, spawn, GizmoSpace.Global), _editor.RingsOf(camera, spawn, GizmoSpace.Global)));
            var frame = Times(10, () => _host.Pump(1));
            var outline = Times(3, () => _editor.Outline());
            Console.WriteLine($"  {count,7} entities, median/p95 ms: drag {drag}, drag and inspector {inspected}, handles and rings {tick}, engine frame {frame}, outline read {outline}");
            Assert.True(inspected.Median < 1, $"a drag step with the inspector costs {inspected.Median:F3} ms at {count} entities");
        }
    }

    static Timing Times(int runs, Action action)
    {
        var ms = new double[runs];
        for (var i = 0; i < runs; i++)
        {
            var start = Stopwatch.GetTimestamp();
            action();
            ms[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        Array.Sort(ms);
        return new Timing(ms[runs / 2], ms[Math.Min(runs - 1, (int)(runs * 0.95))]);
    }

    readonly record struct Timing(double Median, double P95)
    {
        public override string ToString() => $"{Median:F3}/{P95:F3}";
    }

    Snapshot Frame()
    {
        using var readback = new ViewportReadback(_host);
        var rgba = Array.Empty<byte>();
        Assert.True(readback.TryRead(out var width, out var height, ref rgba));
        return new Snapshot(width, height, rgba);
    }

    static bool IsClearColour((byte R, byte G, byte B, byte A) pixel) => IsClearColour(pixel.R, pixel.G, pixel.B);

    static bool IsClearColour(byte r, byte g, byte b) =>
        r >= 255 - Tolerance && Math.Abs(g - 128) <= Tolerance && b <= Tolerance;

    void SceneFilesWorkInTheRunningScene()
    {
        _editor.CreateScene("res://made.oscn", "Entity3D", "Made");
        _editor.OpenScene("res://made.oscn");
        _editor.AddChild(_editor.Outline()!.Handle, "Marker3D", "Inner");
        _editor.Save();
        _editor.OpenScene("res://main.oscn");

        _editor.Instance(_editor.Outline()!.Handle, "res://made.oscn");
        _editor.Save();
        var saved = SavedScene();
        _editor.CloseScene();
        var closed = _editor.Outline();
        _editor.OpenScene("res://main.oscn");
        var made = _editor.Outline()!.Children.Single(c => c.Name == "Made");

        Assert.Contains("path=\"res://made.oscn\"", saved);
        Assert.Contains("instance=ExtResource", saved);
        Assert.DoesNotContain("Inner", saved);
        Assert.Null(closed);
        Assert.Equal(["Inner"], made.Children.Select(c => c.Name));
    }

    // Two points on one line of sight land on one pixel with no perspective, and the nearer one lands further out with it.
    void OrthogonalCameraDropsThePerspective()
    {
        var camera = new EditorCamera(_host);
        var side = Vector3.Normalize(Vector3.Cross(camera.ViewAxis, Vector3.UnitY));
        var far = side + camera.ViewAxis * 5f;
        float Offset() => Vector2.Distance(camera.Project(side), camera.Project(Vector3.Zero));
        float Gap() => Vector2.Distance(camera.Project(side), camera.Project(far));
        float ReachAt(Vector3 origin) =>
            camera.HandlesAt(new Transform3D(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, origin), GizmoSpace.Global) is { } handles
                ? Vector2.Distance(handles.Origin, handles.X)
                : float.NaN;
        float Reach() => ReachAt(Vector3.Zero);
        _host.Pump(FramesToSettle);
        var drawn = Frame();
        var (perspectiveOffset, perspectiveGap, perspectiveReach) = (Offset(), Gap(), Reach());
        Assert.True(perspectiveGap > 5f, $"the perspective view gives a gap of {perspectiveGap} pixels");

        camera.SetOrthogonal(true);
        _host.Pump(FramesToSettle);
        var flat = Frame();

        Assert.True(camera.Orthogonal);
        Assert.True(Gap() < 0.5f, $"the orthogonal view gives a gap of {Gap()} pixels");
        Assert.True(Math.Abs(Offset() - perspectiveOffset) < 1.5f, $"the offset is {Offset()} and the perspective one was {perspectiveOffset}");
        Assert.InRange(Reach() / perspectiveReach, 0.9f, 1.1f);
        Assert.InRange(ReachAt(camera.ViewAxis * 5f) / Reach(), 0.95f, 1.05f);
        Assert.NotEqual(drawn.Pixels, flat.Pixels);

        // 1.1 to the third power is 1.331, and the view that shows less shows each unit larger.
        camera.Zoom(3);
        _host.Pump(1);
        Assert.InRange(Offset() / perspectiveOffset, 1.25f, 1.42f);

        camera.SetOrthogonal(false);
        _host.Pump(1);
        Assert.False(camera.Orthogonal);
        Assert.True(Gap() > 5f, $"the perspective view came back with a gap of {Gap()} pixels");
    }

    // The engine prints an error for a mesh instance with no mesh, and the bridge turns the missing child into the refusal.
    void CollisionShapesAreSaved()
    {
        _editor.OpenScene("res://main.oscn");
        var root = _editor.Outline()!;
        var box = root.Children.Single(c => c.Name == "Box");
        var bare = _editor.AddChild(root.Handle, "MeshInstance3D", "Bare");
        Assert.Empty(box.Children);

        _editor.AddCollision(box.Handle, false);
        var trimesh = _editor.Outline()!.Children.Single(c => c.Name == "Box").Children;
        _editor.AddCollision(box.Handle, true);
        var both = _editor.Outline()!.Children.Single(c => c.Name == "Box").Children;
        var refusals = (
            Assert.Throws<InvalidOperationException>(() => _editor.AddCollision(bare, false)).Message,
            Assert.Throws<InvalidOperationException>(() => _editor.AddCollision(bare, true)).Message,
            Assert.Throws<InvalidOperationException>(() => _editor.AddCollision(root.Children.Single(c => c.Name == "Light").Handle, false)).Message);
        var bareChildren = _editor.Outline()!.Children.Single(c => c.Name == "Bare").Children;
        _editor.Save();
        var saved = SavedScene();

        Assert.Equal(["StaticBody3D"], trimesh.Select(c => c.ClassName));
        Assert.Equal(["CollisionShape3D"], trimesh.Single().Children.Select(c => c.ClassName));
        Assert.Equal(["StaticBody3D", "StaticBody3D"], both.Select(c => c.ClassName));
        Assert.Contains("has no mesh", refusals.Item1);
        Assert.Contains("has no mesh", refusals.Item2);
        Assert.Contains("is not a mesh instance", refusals.Item3);
        Assert.Empty(bareChildren);
        Assert.Equal(2, Count(saved, "type=\"StaticBody3D\" parent=\"Box\""));
        Assert.Equal(2, Count(saved, "type=\"CollisionShape3D\""));
        Assert.Contains("ConcavePolygonShape3D", saved);
        Assert.Contains("ConvexPolygonShape3D", saved);
    }

    // The engine owns a new shape to the mesh's owner, and the scene root has none.
    void CollisionUnderTheRootIsSaved()
    {
        File.WriteAllText(Path.Combine(_folder, "solid.oscn"), SolidScene);
        _editor.OpenScene("res://solid.oscn");

        _editor.AddCollision(_editor.Outline()!.Handle, true);
        _editor.Save();
        var saved = File.ReadAllText(Path.Combine(_folder, "solid.oscn"));

        Assert.Equal(1, Count(saved, "type=\"StaticBody3D\" parent=\".\""));
        Assert.Equal(1, Count(saved, "type=\"CollisionShape3D\" parent=\"Solid_col\""));
    }

    // Left and Right sit 80 units to either side of the middle of the window at the start view. Each pivot is
    // the middle of a coloured box 20 units square. Child hangs 40 units below Right.
    const string PlanarScene = """
        [orb_scene format=3]

        [node name="Level" type="Entity2D"]

        [node name="Left" type="Entity2D" parent="."]
        position = Vector2(-80, 0)

        [node name="LeftBox" type="ColorRect" parent="Left"]
        offset_left = -10.0
        offset_top = -10.0
        offset_right = 10.0
        offset_bottom = 10.0
        color = Color(0, 1, 0, 1)

        [node name="Right" type="Entity2D" parent="."]
        position = Vector2(80, 0)

        [node name="RightBox" type="ColorRect" parent="Right"]
        offset_left = -10.0
        offset_top = -10.0
        offset_right = 10.0
        offset_bottom = 10.0
        color = Color(0, 0, 1, 1)

        [node name="Child" type="Entity2D" parent="Right"]
        position = Vector2(0, 40)
        """;

    sealed record PlanarLevel(nint Level, nint Left, nint Right, nint Child);

    // A second open reads the file again, so each check starts from the saved level.
    PlanarLevel OpenPlanarLevel()
    {
        File.WriteAllText(Path.Combine(_folder, "planar.oscn"), PlanarScene);
        _editor.OpenScene("res://planar.oscn");
        _host.Pump(FramesToSettle);
        var level = _editor.Outline()!;
        var right = level.Children.Single(c => c.Name == "Right");
        return new PlanarLevel(level.Handle, level.Children.Single(c => c.Name == "Left").Handle, right.Handle, right.Children.Single(c => c.Name == "Child").Handle);
    }

    PlanarCamera PlanarOn()
    {
        _planar ??= new PlanarCamera(_host);
        _planar.SetOn(true);
        _host.Pump(FramesToSettle);
        return _planar;
    }

    static bool IsGreen((byte R, byte G, byte B, byte A) pixel) => pixel.G > 200 && pixel.R < 80 && pixel.B < 80;

    static bool IsBlue((byte R, byte G, byte B, byte A) pixel) => pixel.B > 200 && pixel.R < 80 && pixel.G < 80;

    // The box round the pixels that pass the test, as its middle and its size, or null when none passes.
    static (Vector2 Middle, Vector2 Size)? BoxOf(Snapshot frame, Func<(byte R, byte G, byte B, byte A), bool> test)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                if (test(frame.At(x, y)))
                {
                    (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
                }
            }
        }
        return maxX < 0 ? null : (new Vector2(minX + maxX + 1, minY + maxY + 1) / 2, new Vector2(maxX - minX + 1, maxY - minY + 1));
    }

    // The edge pixels blend with the backdrop and miss the colour test, so the box reads up to a pixel small.
    static void AssertBox(Vector2 middle, Vector2 size, (Vector2 Middle, Vector2 Size)? drawn, string what)
    {
        Assert.True(drawn is not null, $"{what} is not in the frame");
        Assert.True(Vector2.Distance(middle, drawn.Value.Middle) < 1f, $"{what} is centred at {drawn.Value.Middle}, not {middle}");
        Assert.True(Vector2.Distance(size, drawn.Value.Size) < 2f, $"{what} is {drawn.Value.Size} pixels, not {size}");
    }

    // The camera starts at the origin with a zoom of one. The backdrop hides the world behind the level while it
    // is on, and the frame is what it was before when it goes off.
    void PlanarCameraDrawsTheLevelOverABackdrop()
    {
        var level = OpenPlanarLevel();
        var world = Frame();
        var planar = PlanarOn();
        var frame = Frame();
        var middle = planar.Size / 2;

        Assert.Equal((frame.Width, frame.Height), ((int)planar.Size.X, (int)planar.Size.Y));
        Assert.NotEqual(world.At(0, 0), frame.At(0, 0));
        Assert.Equal(frame.At(0, 0), frame.At((int)middle.X, (int)middle.Y));
        Assert.True(Vector2.Distance(middle + new Vector2(-80, 0), planar.PivotOf(level.Left)) < 0.01f, $"Left lands at {planar.PivotOf(level.Left)}");
        AssertBox(planar.PivotOf(level.Left), new Vector2(20, 20), BoxOf(frame, IsGreen), "the green box");
        AssertBox(planar.PivotOf(level.Right), new Vector2(20, 20), BoxOf(frame, IsBlue), "the blue box");

        planar.Pan(new Vector2(30, 10));
        _host.Pump(FramesToSettle);
        Assert.True(Vector2.Distance(middle + new Vector2(-50, 10), planar.PivotOf(level.Left)) < 0.01f, $"Left lands at {planar.PivotOf(level.Left)} after a pan");
        AssertBox(planar.PivotOf(level.Left), new Vector2(20, 20), BoxOf(Frame(), IsGreen), "the green box after a pan");

        planar.Zoom(10);
        _host.Pump(FramesToSettle);
        var scale = planar.Project(Vector2.UnitX).X - planar.Project(Vector2.Zero).X;
        Assert.True(scale > 2.5f && scale < 2.7f, $"ten steps zoomed by {scale}");
        AssertBox(planar.PivotOf(level.Left), new Vector2(20, 20) * scale, BoxOf(Frame(), IsGreen), "the green box after a zoom");

        planar.Zoom(-10);
        planar.Pan(new Vector2(-30, -10));
        planar.SetOn(false);
        _host.Pump(FramesToSettle);
        Assert.Equal(world.Pixels, Frame().Pixels);
    }

    // The root's pivot is the middle of the window, Left is 80 to its left and Child is 40 under Right.
    void PlanarPickChoosesTheNearestPivot()
    {
        var level = OpenPlanarLevel();
        var planar = PlanarOn();
        var left = planar.PivotOf(level.Left);

        Assert.Equal(level.Left, _editor.PickPlanar(planar, left));
        Assert.Equal(level.Left, _editor.PickPlanar(planar, left + new Vector2(20, 0)));
        Assert.Equal(0, _editor.PickPlanar(planar, left + new Vector2(30, 0)));
        Assert.Equal(level.Level, _editor.PickPlanar(planar, planar.Size / 2));
        Assert.Equal(0, _editor.PickPlanar(planar, Vector2.Zero));

        planar.Pan(new Vector2(30, 0));
        Assert.Equal(level.Left, _editor.PickPlanar(planar, planar.PivotOf(level.Left)));
        planar.Pan(new Vector2(-30, 0));

        _editor.SetLocked(level.Left, true);
        Assert.Equal(0, _editor.PickPlanar(planar, left));
        _editor.SetLocked(level.Left, false);

        var child = planar.PivotOf(level.Child);
        Assert.Equal(level.Child, _editor.PickPlanar(planar, child));
        _editor.SetGrouped(level.Right, true);
        Assert.Equal(level.Right, _editor.PickPlanar(planar, child));
        _editor.SetLocked(level.Right, true);
        Assert.Equal(0, _editor.PickPlanar(planar, child));
        _editor.SetLocked(level.Right, false);
        _editor.SetGrouped(level.Right, false);
        planar.SetOn(false);
    }

    // Each pointer move goes on its own, a few pixels at a time. The pivot ends where the pointer ends.
    void PlanarMoveFollowsThePointerAtAnyZoom()
    {
        var level = OpenPlanarLevel();
        var planar = PlanarOn();
        var by = new Vector2(10, 5);
        foreach (var zoom in new[] { 0f, 10f, -20f })
        {
            planar.Zoom(zoom);
            _host.Pump(1);
            var from = planar.PivotOf(level.Left);
            for (var step = 0; step < 5; step++)
            {
                _editor.MovePlanar(planar, level.Left, Step(from, from + by, step, 5));
            }

            var to = planar.PivotOf(level.Left);
            Assert.True(Vector2.Distance(from + by, to) < 0.01f, $"the pivot went from {from} to {to} for a pointer move of {by}");
        }

        planar.Zoom(10);
        _host.Pump(FramesToSettle);
        AssertBox(planar.PivotOf(level.Left), new Vector2(20, 20), BoxOf(Frame(), IsGreen), "the green box after the moves");
        planar.SetOn(false);
    }

    void PlanarCameraStaysOutOfTheScene()
    {
        var level = OpenPlanarLevel();
        var planar = PlanarOn();
        var from = planar.PivotOf(level.Left);

        _editor.MovePlanar(planar, level.Left, new FrameMove(from, from + new Vector2(10, 5)));
        _editor.Save();
        var saved = File.ReadAllText(Path.Combine(_folder, "planar.oscn"));

        Assert.Equal(["Left", "Right"], _editor.Outline()!.Children.Select(c => c.Name));
        // The zoom of the earlier checks left float noise in the offset.
        var left = Regex.Match(saved, @"name=""Left"".*\nposition = Vector2\(([-\d.]+), ([-\d.]+)\)");
        Assert.True(left.Success, saved);
        var at = new Vector2(float.Parse(left.Groups[1].Value, CultureInfo.InvariantCulture), float.Parse(left.Groups[2].Value, CultureInfo.InvariantCulture));
        Assert.True(Vector2.Distance(at, new Vector2(-70, 5)) < 0.001f, $"Left was saved at {at}");
        Assert.DoesNotContain("EditorPlanar", saved);
        Assert.DoesNotContain("Camera2D", saved);
        Assert.DoesNotContain("CanvasLayer", saved);
        planar.SetOn(false);
    }

    // Loading a file that is not there makes the engine print an error that names the path.
    void EngineLogReachesTheOutputLog()
    {
        _host.ResourceLoaderLoad(_host.Singleton("ResourceLoader"), "res://nope_for_the_log.tres", "", 1);
        _host.Pump(FramesToSettle);

        _log.Poll();

        Assert.Contains(_log.Lines, line => line.Contains("nope_for_the_log.tres"));
        _log.Clear();
        _log.Poll();
        Assert.DoesNotContain(_log.Lines, line => line.Contains("nope_for_the_log.tres"));
    }

    const string SolidScene = """
        [orb_scene load_steps=2 format=3]

        [sub_resource type="BoxMesh" id="BoxMesh_solid"]

        [node name="Solid" type="MeshInstance3D"]
        mesh = SubResource("BoxMesh_solid")
        """;

    static int Count(string text, string part) => text.Split(part).Length - 1;

    string SavedScene() => File.ReadAllText(Path.Combine(_folder, "main.oscn"));

    public void Dispose()
    {
        _host.Dispose();
        _log.Dispose();
        Directory.Delete(_folder, true);
    }

    sealed record Snapshot(int Width, int Height, byte[] Pixels)
    {
        public (byte R, byte G, byte B, byte A) At(int x, int y)
        {
            var i = (y * Width + x) * 4;
            return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
        }
    }
}

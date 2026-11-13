using System.Numerics;

namespace Orblit.Editor.Bridge.Checks;

/// <summary>The bridge against a headless editor engine: scene reads and writes, and input.</summary>
internal sealed class EditorChecks : IChecks
{
    const long KeyA = 65;
    const long MouseLeft = 1;

    readonly string _library = EngineLibrary.Find();
    readonly string _folder = EngineProject.CopyProject("cube_field");
    readonly EngineHost _host;
    readonly EditorSession _editor;

    public EditorChecks()
    {
        _host = EngineHost.Create(_library, ["--headless", "--editor", "--path", _folder]);
        _host.Start();
        var scene = new EditorInterfaceScene(_host);
        scene.PumpUntilScanned();
        _editor = new EditorSession(_host, scene);
    }

    public IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("a second host throws and the first keeps running", SecondHostThrows),
        ("a missing library names the build command", MissingLibrary),
        ("a signature hash the engine does not know is refused", UnknownHash),
        ("the open scene is read as a tree", OpenSceneIsATree),
        ("a moved entity is saved to the scene file", MovedEntityIsSaved),
        ("a rotated and scaled entity keeps both after a save and a reopen", TurnedEntityIsSaved),
        ("the class check separates entities from other objects", ClassCheck),
        ("a forwarded key is pressed until it is released", ForwardedKey),
        ("a forwarded mouse button is pressed until it is released", ForwardedMouseButton),
        ("an added entity is saved with its name and class", AddedEntityIsSaved),
        ("a renamed entity is saved under its new name", RenamedEntityIsSaved),
        ("a removed entity leaves the saved scene with its children", RemovedEntityIsGone),
        ("a duplicated entity and its children are saved right after the original", DuplicatedEntityIsSaved),
        ("a reordered entity keeps its place, and a moved one keeps where it is drawn and its owner", MovedEntityKeepsItsPlace),
        ("lock, group and hide are read from the outline and kept by a save and a reopen", FlagsAreSaved),
        ("an entity with no place cannot be locked, grouped or hidden", NoPlaceNoFlag),
        ("a parent change leaves its children where they stand while keep children is on", KeepChildrenHoldsChildren),
        ("the scene root, a class that is not an entity and an empty name are refused", Refusals),
        ("the UI surface takes a frame of the right size and refuses any other", UiSurfaceChecksTheFrameSize),
        ("a new scene file is written with its root, and an instance of it is saved as a reference and not a copy", NewSceneIsSavedAndInstanced),
        ("an instance reads the scene file as last saved, not as it was first loaded", InstanceReadsTheSavedFile),
        ("a closed scene has no outline, and the next one opens", ClosedSceneHasNoOutline),
        ("a scene path outside res://, an empty name and a class that is no entity are refused, and a file that is no scene does not instance", SceneFileRefusals),
        ("metadata keeps its type through a save and a reopen, an engine name is not listed and a bad name is refused", MetadataIsSaved),
        ("an object value reads back as the same object, and a Variant of another kind reads as nothing", ObjectVariantRoundTrips),
        ("a script that cannot load is refused and leaves the entity as it was, and clearing no script changes nothing", ScriptRefusals),
        ("a graph script is edited through the session, and the engine's document and the saved file agree", GraphScriptEditsAreSaved),
        ("a refused graph edit gives the engine's reason and changes nothing, and a bad path or file is refused", GraphScriptRefusals),
        ("the palette lists what fits a dragged wire", GraphScriptPalette),
        ("a frame is set, replaced, removed and saved, and a frame with no area is refused", GraphScriptFrames),
        ("variables and signals are declared, retyped and renamed, steps that name them follow, and the file keeps them", GraphScriptDeclarations),
        ("the text a pin is reset to is the zero of its type, and compose and decompose take the parts the editor gives them", GraphScriptPinEdits),
        ("a dialogue message holds up to ten choices, each with an input and an output, and the palette offers both dialogue steps", GraphScriptDialogue),
        ("a snapshot takes a graph script back in one edit, and a copy of steps pastes into another script and asks before a clash", GraphScriptSnapshotsAndPaste),
        ("steps collapse into a Call of a new function and expand back with the same wires, and a selection with a value across its edge is refused", GraphScriptCollapseAndExpand),
        ("every virtual the engine lists for a script's base type makes a function with the signature it names and no error", GraphScriptOverrides),
    ];

    void SecondHostThrows()
    {
        Assert.Throws<InvalidOperationException>(() => EngineHost.Create(_library, ["--headless"]));

        Assert.True(_host.IsStarted());
        _host.Pump(2);
    }

    static void MissingLibrary()
    {
        var error = Assert.Throws<FileNotFoundException>(() => EngineHost.Create("/nowhere/liborblit.dylib", []));

        Assert.Contains("library_type=shared_library", error.Message);
    }

    static void UnknownHash()
    {
        var method = new EngineMethod("Object", "get_class", 1);

        var error = Assert.Throws<InvalidOperationException>(() => method.Bind());

        Assert.Contains("signature hash 1", error.Message);
    }

    void OpenSceneIsATree()
    {
        _editor.OpenScene("res://main.oscn");

        var root = _editor.Outline();

        Assert.NotNull(root);
        Assert.Equal("CubeField", root.Name);
        Assert.Equal("Entity3D", root.ClassName);
        Assert.Equal(["Lamp", "Cubes"], root.Children.Select(c => c.Name));
        Assert.Equal("Cube", root.Children[1].Children.Single().Name);
    }

    void MovedEntityIsSaved()
    {
        _editor.OpenScene("res://main.oscn");
        var cubes = _editor.Outline()!.Children.Single(c => c.Name == "Cubes");

        _editor.MoveTo(cubes.Handle, new Vector3(1.5f, 2.5f, 3.5f));
        _editor.Save();

        Assert.Equal(new Vector3(1.5f, 2.5f, 3.5f), _editor.PositionOf(cubes.Handle));
        var saved = File.ReadAllText(Path.Combine(_folder, "main.oscn"));
        var block = saved[saved.IndexOf("[node name=\"Cubes\"", StringComparison.Ordinal)..];
        Assert.Contains("1.5, 2.5, 3.5)", block[..block.IndexOf("\n\n", StringComparison.Ordinal)]);
    }

    void TurnedEntityIsSaved()
    {
        _editor.OpenScene("res://main.oscn");
        var cubes = _editor.Outline()!.Children.Single(c => c.Name == "Cubes");

        _editor.RotateTo(cubes.Handle, new Vector3(10, 20, 30));
        _editor.ScaleTo(cubes.Handle, new Vector3(2, 3, 4));
        _editor.Save();
        _editor.OpenScene("res://main.oscn");

        var reopened = _editor.Outline()!.Children.Single(c => c.Name == "Cubes");
        AssertNear(new Vector3(10, 20, 30), _editor.RotationOf(reopened.Handle));
        AssertNear(new Vector3(2, 3, 4), _editor.ScaleOf(reopened.Handle));
    }

    // Rotation is stored as a basis, so the angles come back with float error.
    static void AssertNear(Vector3 expected, Vector3 actual) =>
        Assert.True((expected - actual).Length() < 1e-3f, $"expected {expected}, got {actual}");

    void ClassCheck()
    {
        _editor.OpenScene("res://main.oscn");
        var input = _host.Singleton("Input");

        Assert.True(_editor.IsEntity3D(_editor.Outline()!.Handle));
        Assert.False(_editor.IsEntity3D(input));
        Assert.Equal("Input", _host.ObjectGetClass(input));
    }

    void ForwardedKey()
    {
        var forwarder = new InputForwarder(_host);
        var input = _host.Singleton("Input");

        // The engine holds events until its next frame.
        forwarder.Key(KeyA, true);
        _host.Pump(1);
        var whileDown = _host.InputIsKeyPressed(input, KeyA);
        forwarder.Key(KeyA, false);
        _host.Pump(1);

        Assert.True(whileDown);
        Assert.False(_host.InputIsKeyPressed(input, KeyA));
    }

    void ForwardedMouseButton()
    {
        var forwarder = new InputForwarder(_host);
        var input = _host.Singleton("Input");

        forwarder.MouseButton(MouseLeft, new Vector2(10, 20), true);
        _host.Pump(1);
        var whileDown = _host.InputIsMouseButtonPressed(input, MouseLeft);
        forwarder.MouseButton(MouseLeft, new Vector2(10, 20), false);
        _host.Pump(1);

        Assert.True(whileDown);
        Assert.False(_host.InputIsMouseButtonPressed(input, MouseLeft));
    }

    void AddedEntityIsSaved()
    {
        var root = _editor.Outline()!;

        var made = _editor.AddChild(root.Handle, "Marker3D", "Spawn");
        _editor.Save();

        Assert.Contains(_editor.Outline()!.Children, c => c.Handle == made && c.Name == "Spawn" && c.ClassName == "Marker3D");
        Assert.Contains("[node name=\"Spawn\" type=\"Marker3D\" parent=\".\"", SavedScene());
    }

    void RenamedEntityIsSaved()
    {
        var lamp = _editor.Outline()!.Children.Single(c => c.Name == "Lamp");

        _editor.Rename(lamp.Handle, "  Sun ");
        _editor.Save();

        Assert.Contains(_editor.Outline()!.Children, c => c.Handle == lamp.Handle && c.Name == "Sun");
        Assert.Contains("[node name=\"Sun\"", SavedScene());
        Assert.DoesNotContain("name=\"Lamp\"", SavedScene());
    }

    void RemovedEntityIsGone()
    {
        var cubes = _editor.Outline()!.Children.Single(c => c.Name == "Cubes");

        _editor.Remove(cubes.Handle);
        _editor.Save();

        Assert.DoesNotContain(_editor.Outline()!.Children, c => c.Name == "Cubes");
        Assert.DoesNotContain("name=\"Cubes\"", SavedScene());
        Assert.DoesNotContain("name=\"Cube\"", SavedScene());
    }

    void DuplicatedEntityIsSaved()
    {
        var root = _editor.Outline()!;
        var group = _editor.AddChild(root.Handle, "Entity3D", "Group");
        _editor.AddChild(group, "Marker3D", "Leaf");
        _editor.AddChild(root.Handle, "Marker3D", "Tail");
        _editor.MoveTo(group, new Vector3(1, 2, 3));

        var copy = _editor.Duplicate(group);
        _editor.Save();

        var siblings = _editor.Outline()!.Children;
        var at = siblings.ToList().FindIndex(c => c.Name == "Group");
        var original = siblings[at];
        var made = siblings[at + 1];
        Assert.Equal("Tail", siblings[at + 2].Name);
        Assert.True(made.Handle == copy && made.Name == "Group2" && made.ClassName == "Entity3D");
        Assert.Equal(1, made.Children.Count);
        Assert.True(made.Children[0].Name == "Leaf" && made.Children[0].Handle != original.Children[0].Handle);
        Assert.Equal(new Vector3(1, 2, 3), _editor.PositionOf(copy));
        var saved = SavedScene();
        Assert.Contains("[node name=\"Group2\" type=\"Entity3D\" parent=\".\"", saved);
        Assert.Contains("[node name=\"Leaf\" type=\"Marker3D\" parent=\"Group2\"", saved);
        Assert.Contains("[node name=\"Leaf\" type=\"Marker3D\" parent=\"Group\"", saved);
        Assert.True(saved.IndexOf("name=\"Group2\"", StringComparison.Ordinal) < saved.IndexOf("name=\"Tail\"", StringComparison.Ordinal),
            "the saved scene lists the copy before Tail");
    }

    void MovedEntityKeepsItsPlace()
    {
        var root = _editor.Outline()!.Handle;
        var a = _editor.AddChild(root, "Entity3D", "MoveA");
        var b = _editor.AddChild(root, "Entity3D", "MoveB");
        var c = _editor.AddChild(root, "Marker3D", "MoveC");
        var leaf = _editor.AddChild(b, "Marker3D", "MoveLeaf");

        _editor.MoveWithinParent(c, -1);
        Assert.Equal(["MoveA", "MoveC", "MoveB"], RootNames("Move"));
        _editor.MoveWithinParent(a, 1);
        Assert.Equal(["MoveC", "MoveA", "MoveB"], RootNames("Move"));
        _editor.MoveWithinParent(a, -999);
        Assert.Equal(a, _editor.Outline()!.Children[0].Handle);
        _editor.MoveWithinParent(a, 999);
        Assert.Equal(a, _editor.Outline()!.Children[^1].Handle);

        _editor.MoveTo(b, new Vector3(10, 0, 0));
        _editor.MoveTo(c, new Vector3(1, 2, 3));
        var drawnAt = _host.Entity3DGetGlobalPosition(c);
        _editor.Reparent(c, b);
        _editor.Save();

        var moved = _editor.Outline()!.Find(b)!;
        Assert.Equal(["MoveLeaf", "MoveC"], moved.Children.Select(child => child.Name));
        Assert.DoesNotContain(_editor.Outline()!.Children, child => child.Handle == c);
        Assert.Equal(drawnAt, _host.Entity3DGetGlobalPosition(c));
        Assert.Contains("[node name=\"MoveC\" type=\"Marker3D\" parent=\"MoveB\"", SavedScene());

        _editor.AddChild(a, "Marker3D", "MoveLeaf");
        Assert.Equal("MoveB is already under CubeField.", Assert.Throws<InvalidOperationException>(() => _editor.Reparent(b, root)).Message);
        Assert.Equal("An entity cannot move under itself or its own children.", Assert.Throws<InvalidOperationException>(() => _editor.Reparent(b, leaf)).Message);
        Assert.Throws<InvalidOperationException>(() => _editor.Reparent(b, b));
        Assert.Equal("MoveA already has a child named MoveLeaf.", Assert.Throws<InvalidOperationException>(() => _editor.Reparent(leaf, a)).Message);
        Assert.Equal("The scene root cannot be moved.", Assert.Throws<InvalidOperationException>(() => _editor.Reparent(root, a)).Message);
        Assert.Equal("The scene root cannot be moved.", Assert.Throws<InvalidOperationException>(() => _editor.MoveWithinParent(root, 1)).Message);
        Assert.Equal(["MoveLeaf", "MoveC"], _editor.Outline()!.Find(b)!.Children.Select(child => child.Name));
    }

    void FlagsAreSaved()
    {
        var root = _editor.Outline()!.Handle;
        var flagged = _editor.AddChild(root, "Marker3D", "Flagged");
        var plain = _editor.AddChild(root, "Marker3D", "Unflagged");
        Assert.Equal(OutlineFlags.None, FlagsOfNamed("Flagged"));

        _editor.SetLocked(flagged, true);
        Assert.Equal(OutlineFlags.Locked, FlagsOfNamed("Flagged"));
        _editor.SetGrouped(flagged, true);
        _editor.SetHidden(flagged, true);
        Assert.Equal(OutlineFlags.Locked | OutlineFlags.Grouped | OutlineFlags.Hidden, FlagsOfNamed("Flagged"));
        Assert.Equal(OutlineFlags.None, FlagsOfNamed("Unflagged"));
        _editor.Save();
        // A reopen may keep the open scene, so the file itself is read as well.
        var block = SavedScene().Split("\n\n").Single(part => part.Contains("name=\"Flagged\""));
        Assert.Contains("_edit_lock_", block);
        Assert.Contains("_edit_group_", block);
        Assert.Contains("visible = false", block);
        Assert.DoesNotContain("_edit_", SavedScene().Split("\n\n").Single(part => part.Contains("name=\"Unflagged\"")));
        _editor.OpenScene("res://main.oscn");

        Assert.Equal(OutlineFlags.Locked | OutlineFlags.Grouped | OutlineFlags.Hidden, FlagsOfNamed("Flagged"));
        Assert.Equal(OutlineFlags.None, FlagsOfNamed("Unflagged"));
        var reopened = _editor.Outline()!.Children.Single(c => c.Name == "Flagged").Handle;
        _editor.SetLocked(reopened, false);
        _editor.SetGrouped(reopened, false);
        _editor.SetHidden(reopened, false);
        Assert.Equal(OutlineFlags.None, FlagsOfNamed("Flagged"));
        Assert.NotEqual(0, plain);
    }

    OutlineFlags FlagsOfNamed(string name) => _editor.Outline()!.Children.Single(c => c.Name == name).Flags;

    void NoPlaceNoFlag()
    {
        var root = _editor.Outline()!.Handle;
        var plain = _editor.AddChild(root, "Entity", "NoPlace");

        Assert.Equal("NoPlace has no place to lock.", Assert.Throws<InvalidOperationException>(() => _editor.SetLocked(plain, true)).Message);
        Assert.Equal("NoPlace has no place to group.", Assert.Throws<InvalidOperationException>(() => _editor.SetGrouped(plain, true)).Message);
        Assert.Equal("NoPlace has no place to hide.", Assert.Throws<InvalidOperationException>(() => _editor.SetHidden(plain, true)).Message);
        Assert.Equal(OutlineFlags.None, FlagsOfNamed("NoPlace"));

        // A control is a canvas item, but the outline reads no flags for it.
        var control = _editor.AddChild(root, "Control", "Panel");
        Assert.Equal("Panel has no place to lock.", Assert.Throws<InvalidOperationException>(() => _editor.SetLocked(control, true)).Message);
        Assert.Equal(OutlineFlags.None, FlagsOfNamed("Panel"));
    }

    void KeepChildrenHoldsChildren()
    {
        var root = _editor.Outline()!.Handle;
        var parent = _editor.AddChild(root, "Entity3D", "KeepParent");
        var child = _editor.AddChild(parent, "Marker3D", "KeepChild");
        _editor.MoveTo(parent, new Vector3(5, 0, 0));
        _editor.MoveTo(child, new Vector3(1, 2, 3));

        // Off: the child follows its parent.
        var before = _host.Entity3DGetGlobalTransform(child);
        _editor.MoveTo(parent, new Vector3(9, 0, 0));
        Assert.True((_host.Entity3DGetGlobalTransform(child).Origin - before.Origin).Length() > 3, "the child follows with keep children off");

        _editor.KeepChildren = true;
        try
        {
            before = _host.Entity3DGetGlobalTransform(child);
            _editor.MoveTo(parent, new Vector3(-4, 1, 0));
            AssertSamePlace(before, _host.Entity3DGetGlobalTransform(child));
            _editor.RotateTo(parent, new Vector3(0, 45, 0));
            AssertSamePlace(before, _host.Entity3DGetGlobalTransform(child));
            _editor.ScaleTo(parent, new Vector3(2, 3, 4));
            AssertSamePlace(before, _host.Entity3DGetGlobalTransform(child));
        }
        finally
        {
            _editor.KeepChildren = false;
        }
    }

    static void AssertSamePlace(Transform3D expected, Transform3D actual)
    {
        AssertNear(expected.Origin, actual.Origin);
        AssertNear(expected.Row0, actual.Row0);
        AssertNear(expected.Row1, actual.Row1);
        AssertNear(expected.Row2, actual.Row2);
    }

    IEnumerable<string> RootNames(string prefix) =>
        _editor.Outline()!.Children.Select(child => child.Name).Where(name => name.StartsWith(prefix, StringComparison.Ordinal));

    void Refusals()
    {
        var root = _editor.Outline()!;
        var before = root.Children.Count;

        Assert.Throws<InvalidOperationException>(() => _editor.Remove(root.Handle));
        Assert.Throws<InvalidOperationException>(() => _editor.Duplicate(root.Handle));
        Assert.Throws<ArgumentException>(() => _editor.AddChild(root.Handle, "Image", "Picture"));
        Assert.Throws<InvalidOperationException>(() => _editor.AddChild(root.Handle, "NoSuchClass", "Nothing"));
        Assert.Throws<ArgumentException>(() => _editor.AddChild(root.Handle, "Entity3D", "  "));
        Assert.Throws<ArgumentException>(() => _editor.Rename(root.Handle, ""));

        Assert.Equal(before, _editor.Outline()!.Children.Count);
        Assert.Equal("CubeField", _editor.Outline()!.Name);
    }

    void UiSurfaceChecksTheFrameSize()
    {
        using var surface = new EngineUiSurface(_host);

        Assert.Throws<ArgumentException>(() => surface.Upload(new byte[23], 2, 3));
        Assert.Throws<ArgumentException>(() => surface.Upload(new byte[16], 2, 3));
        Assert.Throws<ArgumentException>(() => surface.Upload([], 0, 0));
        surface.Upload(new byte[24], 2, 3);
        surface.Dispose();
        _host.Pump(2);
        Assert.Throws<ObjectDisposedException>(() => surface.Upload(new byte[16], 2, 2));
    }

    void NewSceneIsSavedAndInstanced()
    {
        _editor.CreateScene("res://made.oscn", "Entity3D", " Made ");
        _editor.OpenScene("res://made.oscn");
        var made = _editor.Outline()!;
        _editor.AddChild(made.Handle, "Marker3D", "Inner");
        _editor.Save();
        _editor.CreateScene("res://holder.oscn", "Entity3D", "Holder");
        _editor.OpenScene("res://holder.oscn");

        var instance = _editor.Instance(_editor.Outline()!.Handle, "res://made.oscn");
        _editor.Save();
        var saved = File.ReadAllText(Path.Combine(_folder, "holder.oscn"));
        _editor.OpenScene("res://holder.oscn");

        Assert.Equal(("Made", "Entity3D"), (made.Name, made.ClassName));
        Assert.Contains("path=\"res://made.oscn\"", saved);
        Assert.Contains("instance=ExtResource", saved);
        Assert.DoesNotContain("Inner", saved);
        var again = Assert.Single(_editor.Outline()!.Children);
        Assert.Equal("Made", again.Name);
        Assert.Equal(["Inner"], again.Children.Select(c => c.Name));
        Assert.NotEqual(0, instance);
    }

    void InstanceReadsTheSavedFile()
    {
        _editor.CreateScene("res://fresh.oscn", "Entity3D", "Fresh");
        _editor.CreateScene("res://shelf.oscn", "Entity3D", "Shelf");
        _editor.OpenScene("res://shelf.oscn");
        _editor.Instance(_editor.Outline()!.Handle, "res://fresh.oscn");
        _editor.OpenScene("res://fresh.oscn");
        _editor.AddChild(_editor.Outline()!.Handle, "Marker3D", "Later");
        _editor.Save();
        _editor.OpenScene("res://shelf.oscn");

        var second = _editor.Instance(_editor.Outline()!.Handle, "res://fresh.oscn");

        var instance = _editor.Outline()!.PathTo(second)[^1];
        Assert.Equal(["Later"], instance.Children.Select(c => c.Name));
    }

    void ClosedSceneHasNoOutline()
    {
        _editor.OpenScene("res://main.oscn");

        _editor.CloseScene();

        Assert.Null(_editor.Outline());
        _editor.OpenScene("res://main.oscn");
        Assert.NotNull(_editor.Outline());
    }

    void SceneFileRefusals()
    {
        File.WriteAllText(Path.Combine(_folder, "junk.oscn"), "not a scene");
        // A valid text resource of another type: the loader returns it, and it is not a scene.
        File.WriteAllText(Path.Combine(_folder, "other.oscn"), "[orb_resource type=\"Resource\" format=3]\n\n[resource]\n");
        var root = _editor.Outline()!.Handle;

        Assert.Throws<ArgumentException>(() => _editor.CreateScene("main.oscn", "Entity3D", "X"));
        Assert.Throws<ArgumentException>(() => _editor.CreateScene("res://../x.oscn", "Entity3D", "X"));
        Assert.Throws<ArgumentException>(() => _editor.CreateScene("res://sub\\x.oscn", "Entity3D", "X"));
        Assert.Throws<ArgumentException>(() => _editor.CreateScene("res://x.txt", "Entity3D", "X"));
        Assert.Throws<ArgumentException>(() => _editor.CreateScene("res://.oscn", "Entity3D", "X"));
        Assert.Throws<ArgumentException>(() => _editor.CreateScene("res://kept.oscn", "Resource", "X"));
        Assert.Throws<ArgumentException>(() => _editor.CreateScene("res://kept.oscn", "Entity3D", " "));
        Assert.Throws<InvalidOperationException>(() => _editor.CreateScene("res://main.oscn", "Entity3D", "X"));
        Assert.Throws<InvalidOperationException>(() => _editor.CreateScene("res://made.oscn", "Entity3D", "X"));
        Assert.False(File.Exists(Path.Combine(_folder, "kept.oscn")));
        Assert.Throws<IOException>(() => _editor.CreateScene("res://no_such_folder/x.oscn", "Entity3D", "X"));
        Assert.Throws<ArgumentException>(() => _editor.Instance(root, "res://picture.png"));
        Assert.Throws<IOException>(() => _editor.Instance(root, "res://missing.oscn"));
        Assert.Throws<IOException>(() => _editor.Instance(root, "res://junk.oscn"));
        Assert.Throws<IOException>(() => _editor.Instance(root, "res://other.oscn"));
    }

    void MetadataIsSaved()
    {
        var root = _editor.Outline()!.Handle;
        var tagged = _editor.AddChild(root, "Marker3D", "Tagged");
        Assert.Empty(_editor.MetadataOf(tagged));

        _editor.SetMetadata(tagged, "label", "crate \"A\" é");
        _editor.SetMetadata(tagged, "count", 3L);
        _editor.SetMetadata(tagged, "weight", 1.5);
        _editor.SetMetadata(tagged, "fragile", true);
        _host.ObjectSetMeta(tagged, "_engine_note", "kept");
        var expected = new MetaEntry[] { new("label", "crate \"A\" é"), new("count", 3L), new("weight", 1.5), new("fragile", true) };
        Assert.Equal(expected, _editor.MetadataOf(tagged));
        _editor.SetMetadata(tagged, "count", 4L);
        _editor.SetMetadata(tagged, "gone", "soon");
        _editor.RemoveMetadata(tagged, "gone");
        _editor.RemoveMetadata(tagged, "never_set");
        expected[1] = new("count", 4L);
        Assert.Equal(expected, _editor.MetadataOf(tagged));

        _editor.Save();
        var block = SavedScene().Split("\n\n").Single(part => part.Contains("name=\"Tagged\""));
        Assert.Contains("metadata/count = 4\n", block);
        Assert.Contains("metadata/weight = 1.5\n", block);
        Assert.Contains("metadata/fragile = true\n", block);
        Assert.Contains("metadata/_engine_note = \"kept\"", block);
        Assert.DoesNotContain("metadata/gone", block);
        _editor.OpenScene("res://main.oscn");
        var reopened = _editor.Outline()!.Children.Single(c => c.Name == "Tagged").Handle;
        Assert.Equal(expected, _editor.MetadataOf(reopened));
        Assert.Equal("kept", _host.ObjectGetMeta(reopened, "_engine_note", null));

        foreach (var bad in new[] { "", " ", "1st", "a b", "a=b", "a/b", "_hidden", "naïve" })
        {
            Assert.Throws<ArgumentException>(() => _editor.SetMetadata(reopened, bad, "x"));
            Assert.Throws<ArgumentException>(() => _editor.RemoveMetadata(reopened, bad));
        }
        Assert.Throws<ArgumentException>(() => _editor.SetMetadata(reopened, "ok", 5));
        Assert.Throws<ArgumentException>(() => _editor.SetMetadata(reopened, "ok", new Vector3(1, 2, 3)));
        Assert.Equal(expected, _editor.MetadataOf(reopened));
    }

    void ObjectVariantRoundTrips()
    {
        var root = _editor.Outline()!.Handle;
        var holder = _editor.AddChild(root, "Marker3D", "Holder");
        var other = _editor.AddChild(root, "Marker3D", "Other");

        _host.ObjectSetMeta(holder, "link", other);

        Assert.Equal(other, _host.ObjectGetMeta(holder, "link", null));
        // A Nil and a type the bridge does not read both come back as null.
        Assert.Null(_host.ObjectGetMeta(holder, "absent", null));
        Assert.Equal("fallback", _host.ObjectGetMeta(holder, "absent", "fallback"));
        Assert.Empty(_editor.MetadataOf(holder));
        Assert.Throws<ArgumentException>(() => _host.ObjectSetMeta(holder, "bad", 5));
        Assert.Equal(other, _host.ObjectGetMeta(holder, "link", null));
        // The marshaller takes an object handle, but the editor keeps only the four kinds it lists.
        Assert.Throws<ArgumentException>(() => _editor.SetMetadata(holder, "second", other));
        Assert.Null(_host.ObjectGetMeta(holder, "second", null));
    }

    void ScriptRefusals()
    {
        var root = _editor.Outline()!.Handle;
        var holder = _editor.AddChild(root, "Marker3D", "Scripted");
        File.WriteAllText(Path.Combine(_folder, "spin.cs"), "public class Spin {}\n");
        Assert.Null(_editor.ScriptOf(holder));

        // The engine under test has no C# module, so the file has no loader. The entity stays as it was.
        Assert.Throws<IOException>(() => _editor.AttachScript(holder, "res://spin.cs"));
        Assert.Throws<IOException>(() => _editor.AttachScript(holder, "res://missing.cs"));
        Assert.Throws<ArgumentException>(() => _editor.AttachScript(holder, "res://spin.txt"));
        Assert.Throws<ArgumentException>(() => _editor.AttachScript(holder, "spin.cs"));
        Assert.Throws<ArgumentException>(() => _editor.AttachScript(holder, "/tmp/outside.cs"));
        Assert.Throws<ArgumentException>(() => _editor.AttachScript(holder, "user://outside.cs"));
        Assert.Throws<ArgumentException>(() => _editor.AttachScript(holder, "res://../spin.cs"));
        Assert.Throws<ArgumentException>(() => _editor.AttachScript(holder, "res://main.oscn"));
        Assert.Null(_editor.ScriptOf(holder));

        // A script is kept only through an instance. This engine has no C# module but has graph scripts, so a graph file attaches.
        var graph = MakeGraphFile("attached");
        _editor.AttachScript(holder, graph);
        Assert.Equal(graph, _editor.ScriptOf(holder));
        _editor.ClearScript(holder);
        Assert.Null(_editor.ScriptOf(holder));
    }

    void GraphScriptEditsAreSaved()
    {
        var path = MakeGraphFile("edited");
        var changes = 0;
        var session = GraphScriptSession.Open(_host, path);
        session.Changed += () => changes++;
        var main = Assert.Single(session.Document.Functions);
        Assert.Equal(2, main.Steps.Count);

        var id = session.GetNextStepId("main");
        Assert.True(session.AddStep("main", id, "constant").Ok);
        Assert.True(session.SetStepParameter("main", id, "value", "\"hello\"").Ok);
        Assert.True(session.MoveStep("main", id, 120.5, -30).Ok);
        Assert.True(session.Connect("main", new GraphWire(1, 0, 2, 0)).Ok);
        Assert.True(session.Connect("main", new GraphWire(id, 0, 2, 1)).Ok);
        Assert.Equal(5, changes);

        var added = Assert.Single(session.Document.Functions[0].Steps, step => step.Id == id);
        Assert.Equal("\"hello\"", added.Parameters["value"]);
        Assert.Equal(120.5, added.X);
        Assert.Equal(-30, added.Y);
        Assert.Equal(2, session.Document.Functions[0].Wires.Count);

        Assert.True(session.Disconnect("main", new GraphWire(id, 0, 2, 1)).Ok);
        Assert.Single(session.Document.Functions[0].Wires);
        session.Save();
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.GetNextStepId("main"));

        // The first session freed the script, so the next one reads the file.
        using var reopened = GraphScriptSession.Open(_host, path);
        var kept = Assert.Single(reopened.Document.Functions);
        Assert.Equal(3, kept.Steps.Count);
        Assert.Equal(new GraphWire(1, 0, 2, 0), Assert.Single(kept.Wires));
        Assert.Equal(120.5, Assert.Single(kept.Steps, step => step.Id == id).X);
        Assert.Equal("main", reopened.Document.Functions[0].Name);
    }

    void GraphScriptRefusals()
    {
        var path = MakeGraphFile("refused");
        using var session = GraphScriptSession.Open(_host, path);
        var before = session.Document;
        var changes = 0;
        session.Changed += () => changes++;

        var toSelf = session.Connect("main", new GraphWire(1, 0, 1, 0));
        Assert.False(toSelf.Ok);
        Assert.Contains("does not fit", toSelf.Reason);
        Assert.Contains("not there", session.Connect("main", new GraphWire(1, 0, 99, 0)).Reason);
        Assert.Contains("not there", session.AddStep("missing", 7, "constant").Reason);
        Assert.False(session.AddStep("main", 5, "no_such_kind").Ok);
        Assert.Contains("already there", session.AddStep("main", 1, "constant").Reason);
        Assert.Contains("already there", session.AddFunction("main").Reason);
        Assert.Contains("Variant text", session.SetStepParameter("main", 2, "text", "not (valid").Reason);
        // A refused edit leaves the script alone, so the document is the one read before and nothing was raised.
        Assert.Same(before, session.Document);
        Assert.Equal(0, changes);

        // A data input takes one wire, so the second is refused for that.
        Assert.True(session.AddStep("main", 3, "constant").Ok);
        Assert.True(session.AddStep("main", 4, "constant").Ok);
        Assert.True(session.Connect("main", new GraphWire(3, 0, 2, 1)).Ok);
        Assert.Contains("already has a wire", session.Connect("main", new GraphWire(4, 0, 2, 1)).Reason);
        Assert.Equal(3, changes);

        Assert.Throws<ArgumentException>(() => GraphScriptSession.Open(_host, "res://refused.txt"));
        Assert.Throws<ArgumentException>(() => GraphScriptSession.Open(_host, "refused.ograph"));
        Assert.Throws<ArgumentException>(() => GraphScriptSession.Open(_host, "res://../refused.ograph"));
        Assert.Throws<IOException>(() => GraphScriptSession.Open(_host, "res://missing.ograph"));
        File.WriteAllText(Path.Combine(_folder, "broken.ograph"), "this is not a graph script\n");
        Assert.Throws<IOException>(() => GraphScriptSession.Open(_host, "res://broken.ograph"));
    }

    void GraphScriptPalette()
    {
        using var session = GraphScriptSession.Open(_host, MakeGraphFile("palette"));

        var fromFlow = session.FindSteps("", GraphDraggedSide.Output, 0, GraphPortSort.Flow);
        Assert.Contains(fromFlow, entry => entry.Kind == "branch");
        Assert.DoesNotContain(fromFlow, entry => entry.Kind == "constant");
        Assert.Contains(session.FindSteps("", GraphDraggedSide.None, 0, GraphPortSort.Flow), entry => entry.Kind == "constant");
        Assert.Throws<InvalidDataException>(() => session.FindSteps("", GraphDraggedSide.None, 999, GraphPortSort.Flow));
    }

    void GraphScriptFrames()
    {
        var path = MakeGraphFile("frames");
        var session = GraphScriptSession.Open(_host, path);
        var frame = new GraphFrame(1, "Set up \"café\"", 10, 20, 300, 150, false);
        Assert.True(session.SetFrame("main", frame).Ok);
        Assert.True(session.SetFrame("main", frame with { Title = "Moved", Collapsed = true }).Ok);
        Assert.True(session.SetFrame("main", new GraphFrame(2, "", 0, 0, 40, 40, false)).Ok);

        var before = session.Document;
        Assert.False(session.SetFrame("main", frame with { Id = 3, Width = 0 }).Ok);
        Assert.False(session.SetFrame("nowhere", frame).Ok);
        Assert.False(session.RemoveFrame("main", 9).Ok);
        Assert.Same(before, session.Document);

        Assert.True(session.RemoveFrame("main", 2).Ok);
        session.Save();
        session.Dispose();

        using var reopened = GraphScriptSession.Open(_host, path);
        var kept = Assert.Single(reopened.Document.Functions[0].Frames);
        Assert.Equal(frame with { Title = "Moved", Collapsed = true }, kept);
        Assert.True(reopened.SetFrame("main", frame).Ok);
        Assert.Equal(frame, Assert.Single(reopened.Document.Functions[0].Frames));
    }

    void GraphScriptDeclarations()
    {
        var path = MakeGraphFile("declared");
        var session = GraphScriptSession.Open(_host, path);
        Assert.True(session.AddVariable("total", 2).Ok);
        Assert.True(session.SetVariableDefault("total", "4").Ok);
        Assert.True(session.SetVariableExported("total", true).Ok);
        Assert.True(session.SetVariableInfo("total", "Score", "What was scored").Ok);
        Assert.True(session.AddStep("main", 3, "get").Ok);
        Assert.True(session.SetStepParameter("main", 3, "name", "\"total\"").Ok);

        Assert.True(session.RenameVariable("total", "sum").Ok);
        var sum = Assert.Single(session.Document.Variables);
        Assert.Equal(new GraphVariable("sum", 2, "4", true, 0, "", "Score", "What was scored"), sum);
        Assert.Equal("\"sum\"", Assert.Single(session.Document.Functions[0].Steps, step => step.Id == 3).Parameters["name"]);

        // A new type resets a default that no longer fits, and a hint is kept as set.
        Assert.True(session.SetVariableType("sum", 3).Ok);
        Assert.Equal("0.0", Assert.Single(session.Document.Variables).Default);
        Assert.True(session.SetVariableHint("sum", 1, "0,10").Ok);
        Assert.Equal("0,10", Assert.Single(session.Document.Variables).HintString);

        Assert.True(session.AddSignal("scored").Ok);
        Assert.True(session.SetSignalArguments("scored", [new GraphTyped("amount", 2)]).Ok);
        Assert.True(session.RenameSignal("scored", "points").Ok);
        var points = Assert.Single(session.Document.Signals);
        Assert.Equal("points", points.Name);
        Assert.Equal(new GraphTyped("amount", 2), Assert.Single(points.Arguments));

        Assert.True(session.RenameFunction("main", "start").Ok);
        Assert.Equal("start", Assert.Single(session.Document.Functions).Name);

        var before = session.Document;
        Assert.Contains("already", session.RenameVariable("sum", "sum").Reason);
        Assert.False(session.RenameVariable("nowhere", "other").Ok);
        Assert.False(session.RenameSignal("points", "not valid").Ok);
        Assert.False(session.SetVariableType("sum", -1).Ok);
        Assert.False(session.SetSignalArguments("nowhere", []).Ok);
        Assert.Same(before, session.Document);

        session.Save();
        session.Dispose();
        using var reopened = GraphScriptSession.Open(_host, path);
        Assert.Equal("sum", Assert.Single(reopened.Document.Variables).Name);
        Assert.Equal("points", Assert.Single(reopened.Document.Signals).Name);
        Assert.True(reopened.RemoveSignal("points").Ok);
        Assert.True(reopened.RemoveVariable("sum").Ok);
        Assert.Empty(reopened.Document.Signals);
        Assert.Empty(reopened.Document.Variables);
    }

    void GraphScriptPinEdits()
    {
        using var session = GraphScriptSession.Open(_host, MakeGraphFile("pinedits"));

        // The engine reads each zero text as its type and writes the same text back.
        for (var type = 0; type < 39; type++)
        {
            if (VariantText.ZeroOf(type) is not { } zero)
            {
                continue;
            }

            Assert.True(session.SetStepParameter("main", 2, "text", zero).Ok, $"type {type} refused {zero}");
            var held = session.Document.Functions[0].Steps.Single(step => step.Id == 2).Parameters["text"];
            Assert.True(held == zero, $"type {type} reads back as {held} and not {zero}");
        }

        // A decompose or compose step with one pin for each part compiles, so the editor's part counts are ones the engine takes.
        var id = 3;
        foreach (var type in Enumerable.Range(0, 39).Where(type => VariantTypes.PartsOf(type) > 0))
        {
            var name = VariantTypes.NameOf(type);
            foreach (var kind in new[] { "compose", "decompose" })
            {
                Assert.True(session.AddStep("main", id, kind).Ok);
                Assert.True(session.SetStepParameter("main", id, "type", VariantText.Quote(name)).Ok);
                Assert.True(session.SetStepPins("main", id, VariantTypes.PartsOf(type)).Ok);
                // The engine reports an input with no wire and no value before it reads the parts, so every input gets a value.
                foreach (var port in session.Document.Functions[0].Steps.Single(found => found.Id == id).Inputs.Where(port => port.Parameter != ""))
                {
                    Assert.True(session.SetStepParameter("main", id, port.Parameter, kind == "compose" ? "0.0" : VariantText.ZeroOf(type)!).Ok);
                }

                var function = session.Document.Functions[0];
                var problem = function.Steps.Single(found => found.Id == id).Problem + function.Error;
                Assert.True(problem == "", $"{kind} of {name} with {VariantTypes.PartsOf(type)} pins: {problem}");
                id++;
            }
        }
    }

    void GraphScriptDialogue()
    {
        using var session = GraphScriptSession.Open(_host, MakeGraphFile("dialogue"));

        Assert.Contains(session.FindSteps("", GraphDraggedSide.None, 0, GraphPortSort.Flow), entry => entry.Kind == "dialogue_message");
        Assert.Contains(session.FindSteps("", GraphDraggedSide.None, 0, GraphPortSort.Flow), entry => entry.Kind == "dialogue_choice");

        var id = session.GetNextStepId("main");
        Assert.True(session.AddStep("main", id, "dialogue_message").Ok);
        Assert.True(session.SetStepPins("main", id, 3).Ok);
        var message = session.Document.Functions[0].Steps.Single(step => step.Id == id);
        Assert.Equal(new GraphGrowth(GraphGrowthSide.Output, 0, 3, 10), message.Growth);
        Assert.Equal(["choice0", "choice1", "choice2"], message.Inputs.Where(port => port.Name.StartsWith("choice")).Select(port => port.Name));
        Assert.Equal(4, message.Outputs.Count);

        // A choice wired to the third input goes when the pins shrink below it, and no wire is left on a port that is gone.
        var choice = session.GetNextStepId("main");
        Assert.True(session.AddStep("main", choice, "dialogue_choice").Ok);
        Assert.True(session.Connect("main", new GraphWire(choice, 0, id, 6)).Ok);
        Assert.True(session.SetStepPins("main", id, 1).Ok);
        Assert.DoesNotContain(session.Document.Functions[0].Wires, wire => wire.ToStep == id && wire.ToPort >= 5);

        Assert.False(session.SetStepPins("main", id, 11).Ok);
    }

    // Builds a graph script in the engine and saves it to the project: an entry and a print step, not wired.
    void GraphScriptSnapshotsAndPaste()
    {
        using var source = GraphScriptSession.Open(_host, MakeGraphFile("copy_from"));
        Assert.True(source.AddVariable("total", 2).Ok);
        Assert.True(source.AddStep("main", 3, "constant").Ok);
        Assert.True(source.SetStepParameter("main", 3, "value", "7").Ok);
        Assert.True(source.AddStep("main", 4, "set").Ok);
        Assert.True(source.SetStepParameter("main", 4, "name", "\"total\"").Ok);
        Assert.True(source.Connect("main", new GraphWire(3, 0, 4, 1)).Ok);

        var before = source.GetSnapshot();
        var changes = 0;
        source.Changed += () => changes++;
        Assert.True(source.AddStep("main", 5, "constant").Ok);
        Assert.Equal(5, source.Document.Functions[0].Steps.Count);
        Assert.True(source.Restore(before).Ok);
        Assert.Equal(2, changes);
        Assert.Equal(4, source.Document.Functions[0].Steps.Count);
        Assert.Equal(before, source.GetSnapshot());
        Assert.Equal(30, source.Restore("not a snapshot").Code);
        Assert.Equal(before, source.GetSnapshot());

        var payload = source.GetPayload("main", [3, 4]);
        using var target = GraphScriptSession.Open(_host, MakeGraphFile("copy_to"));
        var pasted = target.PasteSteps("main", payload, 10, 20, GraphPasteChoice.Ask);
        Assert.True(pasted.Result.Ok);
        Assert.Equal(2, pasted.Steps.Count);
        Assert.Contains(target.Document.Variables, variable => variable.Name == "total" && variable.Type == 2);
        var main = target.Document.Functions[0];
        Assert.Equal(4, main.Steps.Count);
        Assert.Equal(10, main.Steps.Where(step => pasted.Steps.Contains(step.Id)).Min(step => step.X));
        Assert.Equal(20, main.Steps.Where(step => pasted.Steps.Contains(step.Id)).Min(step => step.Y));
        Assert.Single(main.Wires);

        // The target now declares total as an Int, so a payload that holds a String total clashes.
        using var other = GraphScriptSession.Open(_host, MakeGraphFile("copy_other"));
        Assert.True(other.AddVariable("total", 4).Ok);
        var clash = other.PasteSteps("main", payload, 0, 0, GraphPasteChoice.Ask);
        Assert.Equal(32, clash.Result.Code);
        Assert.Equal(new GraphPasteClash("variable", "total"), Assert.Single(clash.Clashes));
        Assert.Equal(2, other.Document.Functions[0].Steps.Count);
        Assert.True(other.PasteSteps("main", payload, 0, 0, GraphPasteChoice.KeepBoth).Result.Ok);
        Assert.Contains(other.Document.Variables, variable => variable.Name == "total_2" && variable.Type == 2);
        Assert.Equal(30, target.PasteSteps("main", "not a payload", 0, 0, GraphPasteChoice.Ask).Result.Code);
    }

    // entry 1, then prints 2 to 5 in a row. Steps 3 and 4 are the ones that collapse, so flow enters at 3 and leaves by 4 to 5.
    void GraphScriptCollapseAndExpand()
    {
        using var script = GraphScriptSession.Open(_host, MakeGraphFile("fold"));
        Assert.True(script.AddStep("main", 3, "print").Ok);
        Assert.True(script.AddStep("main", 4, "print").Ok);
        Assert.True(script.AddStep("main", 5, "print").Ok);
        foreach (var id in new[] { 2, 3, 4, 5 })
        {
            Assert.True(script.SetStepParameter("main", id, "text", "\"hi\"").Ok);
        }

        Assert.True(script.MoveStep("main", 3, 300, 40).Ok);
        Assert.True(script.MoveStep("main", 4, 560, 40).Ok);
        foreach (var (from, to) in new[] { (1, 2), (2, 3), (3, 4), (4, 5) })
        {
            Assert.True(script.Connect("main", new GraphWire(from, 0, to, 0)).Ok);
        }

        var folded = script.Collapse("main", [3, 4], "chunk");

        Assert.True(folded.Result.Ok);
        var call = Assert.Single(folded.Steps);
        var main = script.Document.Functions.Single(function => function.Name == "main");
        Assert.Equal([1, 2, 5, call], main.Steps.Select(step => step.Id).Order());
        Assert.Equal("\"chunk\"", main.Steps.Single(step => step.Id == call).Parameters["function"]);
        GraphWire[] collapsed = [new(1, 0, 2, 0), new(2, 0, call, 0), new(call, 0, 5, 0)];
        Assert.True(collapsed.ToHashSet().SetEquals(main.Wires));
        var chunk = script.Document.Functions.Single(function => function.Name == "chunk");
        Assert.Equal(3, chunk.Steps.Count);
        Assert.Equal([(300.0, 40.0), (560.0, 40.0)], chunk.Steps.Where(step => step.Kind == "print").Select(step => (step.X, step.Y)).Order());
        Assert.Equal(2, chunk.Wires.Count);
        Assert.Equal("", main.Error + chunk.Error);

        var opened = script.Expand("main", call);

        Assert.True(opened.Result.Ok);
        Assert.Equal(2, opened.Steps.Count);
        main = script.Document.Functions.Single(function => function.Name == "main");
        var back = main.Steps.Where(step => opened.Steps.Contains(step.Id)).OrderBy(step => step.X).ToList();
        Assert.Equal([(300.0, 40.0), (560.0, 40.0)], back.Select(step => (step.X, step.Y)));
        GraphWire[] expected = [new(1, 0, 2, 0), new(2, 0, back[0].Id, 0), new(back[0].Id, 0, back[1].Id, 0), new(back[1].Id, 0, 5, 0)];
        Assert.True(expected.ToHashSet().SetEquals(main.Wires));
        Assert.Contains(script.Document.Functions, function => function.Name == "chunk");

        var before = script.GetSnapshot();
        Assert.False(script.Collapse("main", [1], "other").Result.Ok);
        Assert.False(script.Collapse("main", [], "other").Result.Ok);
        Assert.Equal(32, script.Collapse("main", [back[0].Id], "main").Result.Code);
        Assert.False(script.Expand("main", 2).Result.Ok);
        Assert.Equal(before, script.GetSnapshot());

        Assert.True(script.AddStep("main", 9, "constant").Ok);
        Assert.True(script.Connect("main", new GraphWire(9, 0, back[0].Id, 1)).Ok);
        var crossing = script.Collapse("main", [back[0].Id], "other");
        Assert.False(crossing.Result.Ok);
        Assert.Contains("value crosses", crossing.Result.Reason);
    }

    void GraphScriptOverrides()
    {
        using var script = GraphScriptSession.Open(_host, MakeGraphFile("overrides"));

        var virtuals = script.FindOverrides();

        Assert.NotEmpty(virtuals);
        Assert.All(virtuals, entry => Assert.Equal(("event", "Object"), (entry.Kind, entry.Group)));
        foreach (var entry in virtuals)
        {
            Assert.True(script.AddFunction(entry.Label).Ok);
            Assert.True(script.AddStep(entry.Label, 1, "entry").Ok);
            foreach (var (parameter, text) in entry.Parameters)
            {
                Assert.True(script.SetStepParameter(entry.Label, 1, parameter, text).Ok);
            }

            // Each override can call the engine's own version, so every virtual of the base type must make a step that compiles.
            var parent = ParentCall.EntryFor(entry.Label, virtuals, "", GraphDraggedSide.None, GraphPortSort.Flow);
            Assert.NotNull(parent);
            Assert.True(script.AddStep(entry.Label, 2, parent.Kind).Ok);
            foreach (var (parameter, text) in parent.Parameters)
            {
                Assert.True(script.SetStepParameter(entry.Label, 2, parameter, text).Ok);
            }

            Assert.True(script.Connect(entry.Label, new GraphWire(1, 0, 2, 0)).Ok);
        }

        var made = script.Document.Functions.Where(function => virtuals.Any(entry => entry.Label == function.Name)).ToList();
        Assert.Equal(virtuals.Select(entry => entry.Label).Order(), made.Select(function => function.Name).Order());
        Assert.All(made, function => Assert.Equal("", function.Error));
    }

    string MakeGraphFile(string name)
    {
        var path = $"res://{name}.ograph";
        var script = _host.Construct("GraphScript");
        try
        {
            Assert.Equal(0, _host.GraphScriptAddFunction(script, "main"));
            Assert.Equal(0, _host.GraphScriptAddStep(script, "main", 1, "entry"));
            Assert.Equal(0, _host.GraphScriptAddStep(script, "main", 2, "print"));
            Assert.Equal(0, _host.ResourceSaverSave(_host.Singleton("ResourceSaver"), script, path, 0));
        }
        finally
        {
            _host.Release(script);
        }
        return path;
    }

    string SavedScene() => File.ReadAllText(Path.Combine(_folder, "main.oscn"));

    public void Dispose()
    {
        _host.Dispose();
        Directory.Delete(_folder, true);
    }
}

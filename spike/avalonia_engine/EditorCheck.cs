using System.Diagnostics;

namespace AvaloniaEngine;

/// <summary>
/// Drives the editor's own services with no window: open a scene, read a node, change it, save.
/// Proves the bridge an Avalonia editor would use for its panels. Hashes come from `--dump-extension-api`.
/// </summary>
public static unsafe class EditorCheck
{
    const long GetResourceFilesystem = 780151678;
    const long IsScanning = 36873697;
    const long OpenSceneFromPath = 1168363258;
    const long GetEditedSceneRoot = 3160264692;
    const long SaveScene = 166280745;
    const long GetChildCount = 894402480;
    const long GetChild = 541253412;
    const long GetClass = 201670096;
    const long SetPosition = 3460891852;
    const long GetPosition = 3360562783;

    public static int Run(string library, string sourceProject)
    {
        // The editor writes .orblit/ and may rewrite project files, so work on a copy.
        var project = Path.Combine(Path.GetTempPath(), $"orblit_editor_check_{Environment.ProcessId}");
        CopyProject(sourceProject, project);

        var baseline = Process.GetCurrentProcess().WorkingSet64 / 1048576;
        var engine = Engine.Create(library, ["--headless", "--editor", "--path", project]);
        if (engine is null)
        {
            Console.Error.WriteLine("create returned null");
            return 1;
        }

        var timer = Stopwatch.StartNew();
        Console.WriteLine($"start: {engine.Start()} in {timer.ElapsedMilliseconds} ms");

        var editor = engine.Singleton("EditorInterface");
        Console.WriteLine($"EditorInterface singleton: {(editor == 0 ? "missing" : "found")}");
        if (editor == 0)
        {
            return 1;
        }

        var scanning = WaitForScan(engine, editor, timer);
        Console.WriteLine($"scan finished: {!scanning} at {timer.ElapsedMilliseconds} ms");

        var path = Engine.NewString("res://main.oscn");
        byte inherited = 0;
        void** openArgs = stackalloc void*[2] { path, &inherited };
        engine.Call("EditorInterface", "open_scene_from_path", OpenSceneFromPath, editor, openArgs, null);
        Pump(engine, 30);

        nint root = 0;
        engine.Call("EditorInterface", "get_edited_scene_root", GetEditedSceneRoot, editor, null, &root);
        Console.WriteLine($"edited scene root: {(root == 0 ? "none" : ClassOf(engine, root))}");
        Console.WriteLine($"working set MB: {baseline} before the engine, {Process.GetCurrentProcess().WorkingSet64 / 1048576} with the editor and scene open");
        if (root == 0)
        {
            return 1;
        }

        long includeInternal = 0;
        long count = 0;
        void** countArgs = stackalloc void*[1] { &includeInternal };
        engine.Call("Node", "get_child_count", GetChildCount, root, countArgs, &count);
        Console.WriteLine($"root children: {count}");

        // Child 3 is "Cubes", a Node3D.
        long index = 3;
        nint cubes = 0;
        byte internalFlag = 0;
        void** childArgs = stackalloc void*[2] { &index, &internalFlag };
        engine.Call("Node", "get_child", GetChild, root, childArgs, &cubes);
        Console.WriteLine($"child 3: {ClassOf(engine, cubes)}");

        float* before = stackalloc float[3];
        engine.Call("Node3D", "get_position", GetPosition, cubes, null, before);
        float* wanted = stackalloc float[3] { 1.5f, 2.5f, 3.5f };
        void** setArgs = stackalloc void*[1] { wanted };
        engine.Call("Node3D", "set_position", SetPosition, cubes, setArgs, null);
        float* after = stackalloc float[3];
        engine.Call("Node3D", "get_position", GetPosition, cubes, null, after);
        Console.WriteLine($"position: ({before[0]}, {before[1]}, {before[2]}) -> ({after[0]}, {after[1]}, {after[2]})");

        long saved = -1;
        engine.Call("EditorInterface", "save_scene", SaveScene, editor, null, &saved);
        Pump(engine, 10);
        Console.WriteLine($"save_scene error code: {saved}");

        // Node3D saves its position inside `transform`, not as a `position` line.
        var text = File.ReadAllText(Path.Combine(project, "main.oscn"));
        var block = text[text.IndexOf("[node name=\"Cubes\"", StringComparison.Ordinal)..];
        var hit = block[..block.IndexOf("\n\n", StringComparison.Ordinal)].Contains("1.5, 2.5, 3.5)");
        Console.WriteLine($"saved file holds the new position: {hit}");

        engine.Dispose();
        Console.WriteLine("destroyed");
        Directory.Delete(project, true);
        return hit && saved == 0 ? 0 : 1;
    }

    static bool WaitForScan(Engine engine, nint editor, Stopwatch timer)
    {
        nint filesystem = 0;
        engine.Call("EditorInterface", "get_resource_filesystem", GetResourceFilesystem, editor, null, &filesystem);
        byte scanning = 1;
        for (var frame = 0; frame < 3000 && scanning != 0; frame++)
        {
            engine.Iteration();
            engine.Call("EditorFileSystem", "is_scanning", IsScanning, filesystem, null, &scanning);
        }
        return scanning != 0;
    }

    static void Pump(Engine engine, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            engine.Iteration();
        }
    }

    static string ClassOf(Engine engine, nint target)
    {
        // The engine assigns into the slot, so it must hold a valid empty String first.
        var slot = stackalloc byte[sizeof(void*)];
        *(nint*)slot = 0;
        engine.Call("Object", "get_class", GetClass, target, null, slot);
        return Engine.ReadString(slot);
    }

    static void CopyProject(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from))
        {
            File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
        }
    }
}

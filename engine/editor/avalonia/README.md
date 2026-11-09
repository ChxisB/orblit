# Orblit editor on Avalonia

This editor opens a project, draws its scene in a viewport and edits the
scene. It runs on macOS. Nothing else has run it.

## What works

- The outliner shows the open scene as a tree. The scenes list shows the
  project's `.oscn` files. A new scene, a closed scene tab and an instanced
  scene file are all done from the editor. Opening a scene saves the open one
  first, if it has edits.
- The viewport draws the scene through a windowed engine. A left drag orbits
  the editor camera and the wheel zooms. A click selects the entity whose
  pivot lands nearest. The editor camera is never saved. Frame keeps the
  angles and the distance.
- The gizmo has Transform, Move, Rotate and Scale modes. Move and Scale
  drag along an axis, and Scale has a centre handle for all three axes.
  Rotate has rings, an outer ring about the view and a trackball. Snap, local
  space, Lock, Group, hide and Keep child transforms are on the toolbar.
- The inspector edits the transform of an `Entity3D`, the metadata of any
  entity and the script attached to it. The Transform and Mesh menus act on
  the selection.
- The tools add, duplicate, rename, remove, reorder and move an entity.
- Save writes the scene file. Play saves edits, then runs the project in a
  second window. A star in the window title marks edits that no save holds.
  A scene with no edits is not saved by Play or by opening another scene,
  because its scripts run while it is open and a save would write what they
  changed.
- Closing the window with unsaved edits keeps it open and shows a bar: Save
  and close, Close without saving, or Keep editing.
- Command+S saves and Command+D duplicates. Command is Control on Windows and
  Linux. F frames the selected entity, except while a box has the typing
  focus. Q, W, E and R pick Select, Move, Rotate and Scale. A shortcut acts only when its
  button is on.
- The Output panel shows what the engine and the games it runs log, read from
  their `--log-file`.
- The workspace tabs are 3D, 2D, Script, Game and Asset Store. 2D pans,
  zooms, picks and moves an `Entity2D`. Script edits a project `.cs` file.
  Game shows whether the game runs. Asset Store lists the kits in the store
  index when Load is pressed.

## What is not built

- Undo and redo, and drag to reparent.
- A pick by mesh bounds. A big mesh is hard to hit away from its pivot.
- Docking changes are kept in memory only.
- 2D has no rotate, scale, snap or grid. The Preview sun, Preview
  environment, Ruler, Debugger, Audio, Animation and Shader Editor bottom
  panels are marked "not built yet".
- Windows and Linux runs. The engine builds for them and for iOS, visionOS,
  Android and Web, but only macOS has run. Play looks for macOS and Linux
  file names only.
- The C# module is built, but the editor finds its assemblies only in
  `OrblitSharp` beside the host executable. A packaged editor has no answer
  for that yet.

## Run it

1. Build the engine library once, from the repo root:

   ```
   scons platform=macos target=editor dev_build=yes library_type=shared_library
   ```

   It writes `bin/liborblit.macos.editor.dev.arm64.dylib`. Set
   `ORBLIT_ENGINE_LIBRARY` to use a library elsewhere. Play starts the
   program `bin/orblit.macos.editor.dev.arm64`, which sits beside the
   library.

2. Install the .NET 10 SDK.

3. Copy a fixture project, because Save writes into the project:

   ```
   cp -R Orblit.Editor.Bridge.Checks/Fixtures/view_demo ~/view_demo
   dotnet run --project Orblit.Editor.App -- --project ~/view_demo
   ```

   Run both commands from this folder. The options are `--project <folder>`,
   `--scene <res:// path>` and `--headless`. The scene defaults to
   `res://main.oscn`. `--headless` runs the engine with no window, so the
   viewport is empty.

The engine window opens beside the editor and takes the foreground when it
starts. One process holds one engine.

## Test it

From this folder:

```
dotnet test Orblit.Editor.App.Tests
dotnet run --project Orblit.Editor.Bridge.Checks
dotnet run --project Orblit.Editor.Bridge.Checks -- --windowed
dotnet run --project Orblit.Editor.Bridge.Checks -- --csharp
```

The window tests run on a scene held in memory, with no engine. The headless
checks run a real engine with no window and read the scene back from the
file. The windowed checks open their own window and read frames back.

The `--csharp` checks attach a script through the bridge with the C# module on.
They need a library built with `module_csharp_enabled=yes` and `dotnet` on the
path. The C# library has the same file name as the plain one, so building one
overwrites the other. The plain checks need the library without the module.
Build the checks project again after a mutant run, because the last mutant's
build stays in `bin`.

The headless engine prints `ERROR: Parameter "t" is null` on each save. The
message comes from thumbnail creation and is harmless.

## Live run

A live run copies the `view_demo` fixture twice, runs the editor with a real
engine window, then runs the sample game UI in a second engine window. Run it
with no other engine process open.

- The editor run has 17 steps. It selects, frames, drags the Move arrow,
  drags the Scale centre handle, turns a ring, drags with snap in local space,
  opens the other workspaces and saves with Command+S. Each step reads the
  result back from the engine.
- The game run has eight steps. The engine draws the UI layer over the scene.
  Each picture is the engine's own readback.
- The editor's input is injected at the window's platform input callback, the
  place a real mouse reaches Avalonia. The operating system sees no mouse and
  needs no Accessibility grant.
- The game's input is injected at `UiInputRouter`, the call the engine's input
  events make. The scene side is a recorder.
- The last step of each run takes a picture of the OS window by its window id.
  That step needs the Screen Recording grant and is skipped without it.

One process cannot hold both the editor and the game UI. The editor uses the
native platform and the game UI uses the headless one, so they run apart.

A headless `UiLayer` must make its first capture in the call that made the
window. A later first capture draws every glyph wrong. The `UiLayer`
constructor makes that capture, and `GameUiTests` holds a test for it.

The headless frame keeps the same pixels when the pointer is over a Fluent
button. `IsPointerOver` is true, so the run checks that and not the colour.
The cause is not known.

## Layout

- `Orblit.Editor.Bridge` is the C# side of the engine's call table. The file
  `calls.json` lists the engine calls the editor uses. `EngineCalls.g.cs`
  holds the matching call table.
- `Orblit.Editor.App` is the Avalonia window and its panels.
- `Orblit.Editor.App.Tests` and `Orblit.Editor.Bridge.Checks` hold the
  tests and the checks.
